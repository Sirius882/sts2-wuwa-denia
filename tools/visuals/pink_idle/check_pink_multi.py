"""Check the original bidirectional-flow formula and the production combo controller."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from check_captures import bilinear, smoothstep

DURATION = 14.663333
TIMES = (0, DURATION * 14 / 132, DURATION * 20 / 132, 10.2)


def check_pink_multi(folder: Path, mod: Path, dark=False):
    duration = 8.782 if dark else DURATION
    count = 108 if dark else 132
    times = (0, duration * 10 / count, duration * 18 / count, 6.2) if dark else TIMES
    prefix = 'dark_multi' if dark else 'multi'
    form = 'dark_2hit' if dark else 'pink_2hit'
    assets = mod / f'images/characters/denia/realtime_{form}'
    atlas, fields = Image.open(assets / 'frames.webp'), Image.open(assets / 'flow.png')
    yy, xx = np.mgrid[1:504:3, 1:896:3]
    uv = (np.column_stack([xx.ravel(), yy.ravel()]) + .5) / [896, 504]

    def tile(image, i, w, h):
        x, y = i % 8 * w, i // 8 * h
        return np.array(image.crop((x, y, x+w, y+h)), dtype=float) / 255

    errors = {}
    for t in times:
        frame = t / duration * count
        i, frac = int(frame), frame % 1
        f0, f1 = tile(atlas, i, 896, 504), tile(atlas, (i+1) % count, 896, 504)
        raw0, raw1 = bilinear(f0, uv), bilinear(f1, uv)
        motion = np.linalg.norm(raw0[:, :3]-raw1[:, :3], axis=1, keepdims=True)*.65 + abs(raw0[:, 3:]-raw1[:, 3:])*1.15
        gate = 1-smoothstep(.10,.28,motion)
        flow = (bilinear(tile(fields, i, 224, 126), uv)*255-128)/127*20
        c0 = bilinear(f0, np.clip(uv-frac*gate*flow[:, :2]/[1792,1008],0,1))
        c1 = bilinear(f1, np.clip(uv-(1-frac)*gate*flow[:, 2:]/[1792,1008],0,1))
        bad = abs(c0[:, 3:]-raw0[:, 3:])+abs(c1[:, 3:]-raw1[:, 3:]) > .32
        c0, c1 = np.where(bad, raw0, c0), np.where(bad, raw1, c1)
        fast = smoothstep(.14,.34,motion)
        weight = frac*(1-fast)+smoothstep(.42,.58,frac)*fast
        alpha = c0[:, 3:]*(1-weight)+c1[:, 3:]*weight
        pm = c0[:, :3]*c0[:, 3:]*(1-weight)+c1[:, :3]*c1[:, 3:]*weight
        pm = np.where(alpha > .004,pm,0)
        actual = np.array(Image.open(folder/f'{prefix}_glsl_{t:.3f}.png'),dtype=float)[yy.ravel(),xx.ravel()]/255
        error = abs(actual-np.column_stack([pm,alpha]))
        assert error.mean()<.004,(t,error.mean())
        assert np.quantile(error,.99)<.018,(t,np.quantile(error,.99))
        errors[str(round(t,6))]=round(float(error.mean()),6)
        # No canvas-wide ambient overlay: composite must contain only the original body/attack layer.
        pure=np.array(Image.open(folder/f'{prefix}_glsl_{t:.3f}.png'),dtype=float)
        composite=np.array(Image.open(folder/f'{prefix}_composite_{t:.3f}.png'),dtype=float)
        assert abs(pure-composite).max()==0, ('background overlay remains',t)

        if t in times[:3]:
            isolated=np.array(Image.open(folder/f'{prefix}_isolated_{t:.3f}.png'),dtype=float)
            controlled=np.array(Image.open(folder/f'{prefix}_controller_{t:.3f}.png'),dtype=float)
            assert abs(isolated-controlled).mean()<.05,('extra body or bad layout',t)
    normal=np.array(Image.open(folder/f'{prefix}_right.png'),dtype=float)
    mirrored=np.array(Image.open(folder/f'{prefix}_left.png'),dtype=float)
    assert abs(normal[:,::-1]-mirrored).mean()<.5,'combo and particles mirror together'
    half=np.array(Image.open(folder/f'{prefix}_half_alpha.png'),dtype=float)
    mask=normal[:,:,3]>240
    assert np.quantile(abs(half[:,:,3][mask]-normal[:,:,3][mask]*.5),.9)<2
    assert len(list(folder.glob(f'{prefix}_[0-9][0-9][0-9][0-9].png')))==int(np.ceil(duration*60))+1
    print(f'PASS: {count} frames, original RGBA bidirectional flow, {int(np.ceil(duration*60))+1} captures, mirror, alpha and controller: {errors}')
    return errors


def make_preview(folder: Path, dark=False):
    prefix = 'dark_multi' if dark else 'multi'
    frames=(0,30,49,65,88,180,370,527) if dark else (0,80,93,120,133,300,610,880)
    sheet=Image.new('RGB',(896*2,272*4),'#121924');draw=ImageDraw.Draw(sheet)
    for j,frame in enumerate(frames):
        im=Image.open(folder/f'{prefix}_{frame:04d}.png').convert('RGBA');im.thumbnail((896,252))
        x,y=j%2*896,j//2*272
        sheet.paste(im,(x,y),im);draw.text((x+8,y+254),f'{frame/60:.3f}s',fill='white')
    sheet.save(folder/'keyframes.jpg',quality=95)
    sheet=Image.new('RGB',(1600,680),'#121924')
    for j,name in enumerate((f'{prefix}_idle_alignment',f'{prefix}_controller_0.000')):
        im=Image.open(folder/f'{name}.png').crop((560,300,1360,980))
        sheet.paste(im,(j*800,0),im)
    sheet.save(folder/'body_alignment.jpg',quality=95)
