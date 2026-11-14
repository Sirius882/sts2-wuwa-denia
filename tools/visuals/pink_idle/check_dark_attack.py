"""Independent original-GLSL comparison and wide-canvas/body alignment captures."""
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from check_captures import bilinear, sharpen, smoothstep


def check_dark_attack(folder: Path, mod: Path):
    source = (mod / 'tools/visuals/pink_idle/source/dark_1hit_animator.js').read_text('utf-8-sig')
    meta = json.loads(re.search(r'^const BLACK_SYNC_F0_DATA = (.+);', source, re.M)[1])['meta']
    atlas = Image.open(mod / 'images/characters/denia/realtime_dark_1hit/frames.webp')
    fields = Image.open(mod / 'images/characters/denia/realtime_dark_1hit/flow.png')
    res = np.array([960, 480])
    yy, xx = np.mgrid[1:480:3, 1:960:3]
    uv = (np.column_stack([xx.ravel(), yy.ravel()]) + .5) / res

    def tile(image, n, w, h):
        x, y = n % 8 * w, n // 8 * h
        return np.array(image.crop((x, y, x + w, y + h)), dtype=float) / 255

    def track(key, frame):
        i, f = int(frame), frame % 1
        a, b, c, d = (meta[key][n % 96] for n in (i - 1, i, i + 1, i + 2))
        return .5 * (2*b + (c-a)*f + (2*a-5*b+4*c-d)*f*f + (-a+3*b-3*c+d)*f*f*f)

    errors = {}
    for seconds in (0, .8263541667, 6.9):
        frame = seconds / meta['duration'] * 96
        i, frac = int(frame), frame % 1
        f0, f1 = tile(atlas, i, 960, 480), tile(atlas, (i+1) % 96, 960, 480)
        base0, base1 = bilinear(f0, uv), bilinear(f1, uv)
        motion = np.maximum(abs(base1[:, 3:] - base0[:, 3:]),
            np.linalg.norm(base1[:, :3]*base1[:, 3:] - base0[:, :3]*base0[:, 3:], axis=1, keepdims=True)*1.15)
        flow = (bilinear(tile(fields, i, 120, 60), uv)[:, :2]*255 - 128)*.25
        shift = flow * (1-smoothstep(.08, .22, motion)) / res
        c0 = sharpen(f0, np.clip(uv-frac*shift, .001, .999), resolution=res, blend=.55, step=.82, gain=.16)
        c1 = sharpen(f1, np.clip(uv+(1-frac)*shift, .001, .999), resolution=res, blend=.55, step=.82, gain=.16)
        fast = smoothstep(.10, .24, motion)
        weight = frac*(1-fast) + smoothstep(.42, .58, frac)*fast
        alpha = c0[:, 3:]*(1-weight) + c1[:, 3:]*weight
        pm = c0[:, :3]*c0[:, 3:]*(1-weight) + c1[:, :3]*c1[:, 3:]*weight
        rgb = np.divide(pm, alpha, out=np.zeros_like(pm), where=alpha > 1e-4)
        px = uv*res
        sphere_alpha = np.clip(track('bubble_alpha', frame), 0, 1)
        d = (px-[meta['bubble_cx'], track('bubble_cy', frame)]) / max(track('bubble_r_dyn', frame), 1)
        r2 = (d*d).sum(axis=1, keepdims=True);r = np.sqrt(r2)
        nz = np.sqrt(np.maximum(.01, 1-np.minimum(r2, 1)))
        wave = np.sin(r*28-seconds*3.4+d[:, :1]*6)*np.cos(d[:, 1:]*18+seconds*2.1)
        mask = smoothstep(.92,.25,r)*smoothstep(-.2,.75,d[:, 1:])*smoothstep(.45,-.35,d[:, :1])
        mix = .5+.5*wave
        caustic = (np.array([.18,.75,1])*(1-mix)+np.array([.95,.88,.55])*mix)*np.maximum(0,wave)*mask*.09
        rim_mask = smoothstep(.76,.99,r)*smoothstep(1.05,.96,r)
        rim = (np.array([.22,.68,1])*(1-rim_mask)+np.array([.68,.88,1])*rim_mask)*((1-nz)**2.8*.16+rim_mask*.26)
        iri = np.where((r2 <= 1.05) & (sphere_alpha > .01), (caustic+rim)*sphere_alpha, 0)
        rgb = np.clip(rgb+iri*alpha, 0, 1)
        slash = np.clip(track('slash_alpha', frame), 0, 1)
        edge = smoothstep(.95,.25,alpha)*smoothstep(.02,.35,alpha)
        mix = .5+.5*np.sin(px[:, 1:]*.06-seconds*12)
        rim = np.array([.35,.85,1])*(1-mix)+np.array([.92,.32,.88])*mix
        rgb = np.clip(rgb + np.where((slash > .01) & (alpha > .05), rim*edge*slash*.38, 0), 0, 1)
        halo = [track('orb_x',frame), track('orb_y',frame)]
        dist = np.linalg.norm((px-halo)*[.95,2.15],axis=1,keepdims=True)
        glow = np.exp(-dist*.14)*(.14+.05*np.sin(seconds*3.6))
        combined_alpha = np.clip(alpha+np.clip(glow*.35,0,1)*(1-alpha),0,1)
        pm = rgb*alpha+np.array([.32,.78,1])*glow*(1-alpha*.4)
        straight = np.divide(pm,combined_alpha,out=np.zeros_like(pm),where=combined_alpha > 1e-4)
        expected = np.column_stack([np.clip(straight,0,1)*combined_alpha,combined_alpha])
        actual = np.array(Image.open(folder/f'dark_glsl_{seconds:.3f}.png'),dtype=float)[yy.ravel(),xx.ravel()]/255
        error = abs(actual-expected)
        assert error.mean() < .004, (seconds,error.mean())
        assert np.quantile(error,.99) < .018, (seconds,np.quantile(error,.99))
        errors[str(seconds)] = round(float(error.mean()),6)
        standalone = np.array(Image.open(folder/f'dark_isolated_{seconds:.3f}.png'),dtype=float)
        controlled = np.array(Image.open(folder/f'dark_controller_{seconds:.3f}.png'),dtype=float)
        assert abs(standalone-controlled).mean() < .05, ('extra body or canvas clipping',seconds)
    normal = np.array(Image.open(folder/'dark_right.png'),dtype=float)
    flipped = np.array(Image.open(folder/'dark_left.png'),dtype=float)
    assert abs(normal[:, ::-1]-flipped).mean() < .5, 'giant claw and all effects mirror together'
    half = np.array(Image.open(folder/'dark_half_alpha.png'),dtype=float)
    mask = normal[:, :, 3] > 240
    assert np.quantile(abs(half[:, :, 3][mask]-normal[:, :, 3][mask]*.5),.9) < 2
    assert len(list(folder.glob('dark_[0-9][0-9][0-9][0-9].png'))) == 477
    # Capture must retain the giant claw well beyond the body Bounds (847..1073).
    strike = np.array(Image.open(folder/'dark_controller_0.826.png'))
    assert (strike[:, 1150:, 3] > 100).sum() > 1000, 'oversized right claw must remain visible'
    print(f'PASS: original dark GLSL, 477 frames, full giant claw, mirror, alpha, and production controller: {errors}')
    return errors


def make_preview(folder: Path):
    times = (0, 30, 50, 70, 180, 420, 476)
    sheet = Image.new('RGB', (960, len(times)*260), '#121924')
    draw = ImageDraw.Draw(sheet)
    for row, frame in enumerate(times):
        picture = Image.open(folder/f'dark_{frame:04d}.png').convert('RGBA')
        picture.thumbnail((960,240))
        sheet.paste(picture,(0,row*260),picture)
        draw.text((12,row*260+242),f'{frame/60:.3f}s',fill='white')
    sheet.save(folder/'keyframes.jpg',quality=94)
    # Same controller Bounds/anchor and scale in both captures; no effect-based normalization.
    pairs = Image.new('RGB',(1920,1080),'#121924')
    for x, name in ((0,'dark_idle_body_alignment'),(960,'dark_controller_0.000')):
        picture = Image.open(folder/f'{name}.png').crop((550,320,1350,1000))
        pairs.paste(picture,(x+80,130),picture)
    ImageDraw.Draw(pairs).text((80,40),'Black idle / Dark single hit: identical body scale and anchor',fill='white')
    pairs.save(folder/'body_alignment.jpg',quality=95)
