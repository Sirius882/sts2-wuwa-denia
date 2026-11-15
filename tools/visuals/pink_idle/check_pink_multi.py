"""Independent original GLSL reference for remade multi-hit atlases and sync captures."""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from check_captures import bilinear, smoothstep
from multi_animator import read_animator

# Known locally generated atlases are up to 180,486,144 pixels; retain a finite bound.
Image.MAX_IMAGE_PIXELS = 200_000_000

SPECS = {
    'pink_2hit': (14.663333,132,[14,20],'multi',10.2),
    'dark_2hit': (8.783,72,[6,12],'dark_multi',6.2),
    'dark_4hit': (11.466,116,[10,19,26,33],'dark_four',8.4),
}

def premul(image, uv):
    c=bilinear(image,uv);return np.column_stack([c[:,:3]*c[:,3:],c[:,3:]])

def cubic(image,uv,res):
    texel=uv*res-.5;f=texel%1;base=(np.floor(texel)+.5)/res
    weights=[f*(-.5+f*(1-.5*f)),1+f*f*(-2.5+1.5*f),f*(.5+f*(2-1.5*f)),f*f*(-.5+.5*f)]
    total=np.zeros((len(uv),4))
    for j in range(-1,3):
        for i in range(-1,3):total+=premul(image,base+[i,j]/res)*weights[i+1][:,:1]*weights[j+1][:,1:]
    total=np.maximum(total,0);a=np.clip(total[:,3:],0,1)
    rgb=np.divide(total[:,:3],a,out=np.zeros_like(total[:,:3]),where=a>.002)
    return np.column_stack([np.clip(rgb,0,1)*a,a])

def formula(f0,f1,fields,uv,frac,res,dark_two):
    if dark_two:
        flow=(bilinear(fields,uv)[:,:2]*255-128)*.5/[216,122]
        uv0,uv1=uv-frac*flow,uv+(1-frac)*flow
        c0,c1=cubic(f0,uv0,res),cubic(f1,uv1,res)
        rgb0=np.divide(c0[:,:3],c0[:,3:],out=np.zeros_like(c0[:,:3]),where=c0[:,3:]>.01)
        rgb1=np.divide(c1[:,:3],c1[:,3:],out=np.zeros_like(c1[:,:3]),where=c1[:,3:]>.01)
        consistency=np.exp(-(np.linalg.norm(rgb0-rgb1,axis=1,keepdims=True)*3.5+abs(c0[:,3:]-c1[:,3:])*5.5))
        weight=smoothstep(.36,.64,frac)*(1-consistency)+frac*consistency
        c=c0*(1-weight)+c1*weight;alpha=c[:,3:]
        center=np.divide(c[:,:3],alpha,out=np.zeros_like(c[:,:3]),where=alpha>0)
        neighbors=[premul(f0,uv0+shift/res)*(1-weight)+premul(f1,uv1+shift/res)*weight for shift in (np.array([0,-.72]),np.array([0,.72]),np.array([.72,0]),np.array([-.72,0]))]
        unit=[np.divide(v[:,:3],v[:,3:],out=center.copy(),where=v[:,3:]>.01) for v in neighbors]
        minimum=np.minimum.reduce([center]+unit);maximum=np.maximum.reduce([center]+unit)
        contrast=np.linalg.norm(maximum-minimum,axis=1,keepdims=True)
        rgb=np.clip(np.clip(center+(center*4-sum(unit))*(.28+.16*smoothstep(.04,.35,contrast)),minimum*.92,maximum*1.06),0,1)
        a=smoothstep(.03,.97,np.clip(alpha+(alpha*4-sum(n[:,3:] for n in neighbors))*.14,0,1))
        return np.where(alpha<.003,0,np.column_stack([rgb*a,a]))
    raw0,raw1=bilinear(f0,uv),bilinear(f1,uv)
    motion=np.linalg.norm(raw0[:,:3]-raw1[:,:3],axis=1,keepdims=True)*.75+abs(raw0[:,3:]-raw1[:,3:])*1.25
    gate=1-smoothstep(.035,.12,motion)
    flow=(bilinear(fields,uv)*255-128)/127*16
    c0=bilinear(f0,np.clip(uv-frac*gate*flow[:,:2]/res,0,1))
    c1=bilinear(f1,np.clip(uv-(1-frac)*gate*flow[:,2:]/res,0,1))
    bad=abs(c0[:,3:]-raw0[:,3:])+abs(c1[:,3:]-raw1[:,3:])>.18
    c0,c1=np.where(bad,raw0,c0),np.where(bad,raw1,c1)
    fast=smoothstep(.04,.14,motion);weight=frac*(1-fast)+smoothstep(.42,.58,frac)*fast
    a=c0[:,3:]*(1-weight)+c1[:,3:]*weight
    pm=c0[:,:3]*c0[:,3:]*(1-weight)+c1[:,:3]*c1[:,3:]*weight
    rgb=np.divide(pm,a,out=np.zeros_like(pm),where=a>.004)
    dominant=f0 if frac<.5 else f1;center=raw0 if frac<.5 else raw1
    high=center[:,:3]-.25*sum(bilinear(dominant,uv+shift/res)[:,:3] for shift in (np.array([0,-1]),np.array([0,1]),np.array([-1,0]),np.array([1,0])))
    rgb=np.where(a>.05,np.clip(rgb+high*(.58*smoothstep(.05,.55,a)),0,1),rgb)
    return np.column_stack([rgb*a,a])

def check_pink_multi(folder: Path, mod: Path, form='pink_2hit'):
    duration,count,impacts,prefix,last=SPECS[form]
    source=read_animator(mod/f'tools/visuals/pink_idle/source/{form}_animator.js')
    w,h,fw,fh=(source[k] for k in ('width','height','fw','fh'))
    assets=mod/f'images/characters/denia/realtime_{form}'
    # Memory-map only individual tiles instead of decoding the large atlas for each sample.
    atlas,fields=Image.open(assets/'frames.webp'),Image.open(assets/'flow.png')
    def tile(image,i,w,h):return np.array(image.crop((i%8*w,i//8*h,i%8*w+w,i//8*h+h)),dtype=float)/255
    yy,xx=np.mgrid[2:h:5,2:w:5];uv=(np.column_stack([xx.ravel(),yy.ravel()])+.5)/[w,h]
    times=[0]+[duration*i/count for i in impacts]+[last]
    errors={}
    for t in times:
        frame=t/duration*count;i,frac=int(frame),frame%1
        f0,f1=tile(atlas,i,w,h),tile(atlas,(i+1)%count,w,h)
        expected=formula(f0,f1,tile(fields,i,fw,fh),uv,frac,np.array([w,h]),form=='dark_2hit')
        actual=np.array(Image.open(folder/f'{prefix}_glsl_{t:.3f}.png'),dtype=float)[yy.ravel(),xx.ravel()]/255
        error=abs(actual-expected)
        assert error.mean()<.004,(form,t,error.mean())
        assert np.quantile(error,.99)<.018,(form,t,np.quantile(error,.99))
        errors[str(round(t,6))]=round(float(error.mean()),6)
        pure=np.array(Image.open(folder/f'{prefix}_glsl_{t:.3f}.png'),dtype=float)
        composite=np.array(Image.open(folder/f'{prefix}_composite_{t:.3f}.png'),dtype=float)
        assert abs(pure-composite).max()==0,('background overlay remains',t)
        if t!=last:
            isolated=np.array(Image.open(folder/f'{prefix}_isolated_{t:.3f}.png'),dtype=float)
            controlled=np.array(Image.open(folder/f'{prefix}_controller_{t:.3f}.png'),dtype=float)
            assert abs(isolated-controlled).mean()<.05,('extra body or bad layout',t)
    normal=np.array(Image.open(folder/f'{prefix}_right.png'),dtype=float);mirrored=np.array(Image.open(folder/f'{prefix}_left.png'),dtype=float)
    assert abs(normal[:,::-1]-mirrored).mean()<.5,'mirroring around calibrated body anchor'
    half=np.array(Image.open(folder/f'{prefix}_half_alpha.png'),dtype=float);mask=normal[:,:,3]>240
    assert np.quantile(abs(half[:,:,3][mask]-normal[:,:,3][mask]*.5),.9)<2
    assert len(list(folder.glob(f'{prefix}_[0-9][0-9][0-9][0-9].png')))==int(np.ceil(duration*60))+1
    print(f'PASS: {form} original shader/flow, no ambient overlay, full captures, mirror/alpha/controller: {errors}')
    return errors

def make_preview(folder: Path, form='pink_2hit'):
    duration,count,impacts,prefix,last=SPECS[form]
    times=[0]+[duration*i/count for i in impacts]+[last,duration]
    sheet=Image.new('RGB',(896*2,275*((len(times)+1)//2)),'#121924');draw=ImageDraw.Draw(sheet)
    for j,t in enumerate(times):
        i=min(int(np.ceil(duration*60)),round(t*60));im=Image.open(folder/f'{prefix}_{i:04d}.png').convert('RGBA');im.thumbnail((896,252))
        x,y=j%2*896,j//2*275;sheet.paste(im,(x,y),im);draw.text((x+8,y+254),f'{t:.3f}s'+(f'  HIT {j}' if 0<j<=len(impacts) else ''),fill='white')
    sheet.save(folder/'keyframes.jpg',quality=95)
    sheet=Image.new('RGB',(1600,680),'#121924')
    for j,name in enumerate((f'{prefix}_idle_alignment',f'{prefix}_controller_0.000')):
        im=Image.open(folder/f'{name}.png').crop((560,300,1360,980));sheet.paste(im,(j*800,0),im)
    sheet.save(folder/'body_alignment.jpg',quality=95)
