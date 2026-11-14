"""Check the original bidirectional-flow formula and the production combo controller."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from check_captures import bilinear, smoothstep

DURATION = 14.663333
TIMES = (0, DURATION * 14 / 132, DURATION * 20 / 132, 10.2)


def check_pink_multi(folder: Path, mod: Path):
    assets = mod / 'images/characters/denia/realtime_pink_2hit'
    atlas, fields = Image.open(assets / 'frames.webp'), Image.open(assets / 'flow.png')
    yy, xx = np.mgrid[1:504:3, 1:896:3]
    uv = (np.column_stack([xx.ravel(), yy.ravel()]) + .5) / [896, 504]

    def tile(image, i, w, h):
        x, y = i % 8 * w, i // 8 * h
        return np.array(image.crop((x, y, x+w, y+h)), dtype=float) / 255

    errors = {}
    for t in TIMES:
        frame = t / DURATION * 132
        i, frac = int(frame), frame % 1
        f0, f1 = tile(atlas, i, 896, 504), tile(atlas, (i+1) % 132, 896, 504)
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
        actual = np.array(Image.open(folder/f'multi_glsl_{t:.3f}.png'),dtype=float)[yy.ravel(),xx.ravel()]/255
        error = abs(actual-np.column_stack([pm,alpha]))
        assert error.mean()<.004,(t,error.mean())
        assert np.quantile(error,.99)<.018,(t,np.quantile(error,.99))
        errors[str(round(t,6))]=round(float(error.mean()),6)
        if t < 3:
            isolated=np.array(Image.open(folder/f'multi_isolated_{t:.3f}.png'),dtype=float)
            controlled=np.array(Image.open(folder/f'multi_controller_{t:.3f}.png'),dtype=float)
            assert abs(isolated-controlled).mean()<.05,('extra body or bad layout',t)
    normal=np.array(Image.open(folder/'multi_right.png'),dtype=float)
    mirrored=np.array(Image.open(folder/'multi_left.png'),dtype=float)
    assert abs(normal[:,::-1]-mirrored).mean()<.5,'combo and particles mirror together'
    half=np.array(Image.open(folder/'multi_half_alpha.png'),dtype=float)
    mask=normal[:,:,3]>240
    assert np.quantile(abs(half[:,:,3][mask]-normal[:,:,3][mask]*.5),.9)<2
    assert len(list(folder.glob('multi_[0-9][0-9][0-9][0-9].png')))==881
    print(f'PASS: 132 frames, original RGBA bidirectional flow, 881 captures, mirror, alpha and controller: {errors}')
    return errors


def make_preview(folder: Path):
    frames=(0,80,93,120,133,300,610,880)
    sheet=Image.new('RGB',(896*2,272*4),'#121924');draw=ImageDraw.Draw(sheet)
    for j,frame in enumerate(frames):
        im=Image.open(folder/f'multi_{frame:04d}.png').convert('RGBA');im.thumbnail((896,252))
        x,y=j%2*896,j//2*272
        sheet.paste(im,(x,y),im);draw.text((x+8,y+254),f'{frame/60:.3f}s',fill='white')
    sheet.save(folder/'keyframes.jpg',quality=95)
    sheet=Image.new('RGB',(1600,680),'#121924')
    for j,name in enumerate(('multi_idle_alignment','multi_controller_0.000')):
        im=Image.open(folder/f'{name}.png').crop((560,300,1360,980))
        sheet.paste(im,(j*800,0),im)
    sheet.save(folder/'body_alignment.jpg',quality=95)
