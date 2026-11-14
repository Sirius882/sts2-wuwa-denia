"""Compare native GPU output with the original GLSL formula, including flow sign and atlas edges."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

RES = np.array([592, 600])


def bilinear(image, uv):
    height, width = image.shape[:2]
    p = uv * [width, height] - .5
    i = np.floor(p).astype(int)
    f = p - i
    x0, y0 = np.clip(i[:, 0], 0, width - 1), np.clip(i[:, 1], 0, height - 1)
    x1, y1 = np.clip(i[:, 0] + 1, 0, width - 1), np.clip(i[:, 1] + 1, 0, height - 1)
    a = image[y0, x0] * (1 - f[:, :1]) + image[y0, x1] * f[:, :1]
    b = image[y1, x0] * (1 - f[:, :1]) + image[y1, x1] * f[:, :1]
    return a * (1 - f[:, 1:]) + b * f[:, 1:]


def sharpen(image, uv, black=False, *, resolution=RES, blend=None, step=None, gain=None):
    p = uv * resolution - .5
    i = np.floor(p)
    f = p - i
    f2, f3 = f * f, f * f * f
    w0 = (1 - 3 * f + 3 * f2 - f3) / 6
    w1 = (4 - 6 * f2 + 3 * f3) / 6
    w2 = (1 + 3 * f + 3 * f2 - 3 * f3) / 6
    w3 = f3 / 6
    g0, g1 = w0 + w1, w2 + w3
    h0 = (i + w1 / np.maximum(g0, 1e-5) - .5) / resolution
    h1 = (i + w3 / np.maximum(g1, 1e-5) + 1.5) / resolution
    t00 = bilinear(image, h0)
    t10 = bilinear(image, np.column_stack([h1[:, 0], h0[:, 1]]))
    t01 = bilinear(image, np.column_stack([h0[:, 0], h1[:, 1]]))
    t11 = bilinear(image, h1)
    bic = (t00 * g0[:, :1] + t10 * g1[:, :1]) * g0[:, 1:] + (t01 * g0[:, :1] + t11 * g1[:, :1]) * g1[:, 1:]
    blend = (.55 if black else .58) if blend is None else blend
    c = np.clip(bic * (1 - blend) + bilinear(image, uv) * blend, 0, 1)
    lap = c[:, :3] * 4
    step = (.82 if black else .85) if step is None else step
    for shift in ([0, -step], [0, step], [step, 0], [-step, 0]):
        lap -= bilinear(image, uv + shift / resolution)[:, :3]
    c[:, :3] = np.clip(c[:, :3] + lap * ((.15 if black else .14) if gain is None else gain), 0, 1)
    return c


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def track(values, frame):
    i, f = int(frame), frame % 1
    a, b, c, d = (values[n % 48] for n in (i - 1, i, i + 1, i + 2))
    return .5 * (2 * b + (c - a) * f + (2 * a - 5 * b + 4 * c - d) * f * f
                 + (-a + 3 * b - 3 * c + d) * f * f * f)


def black_shading(rgb, alpha, uv, seconds, meta, frame):
    px = uv * RES
    center = np.array([265, track(meta['bubble_cy'], frame)])
    d = (px - center) / 76
    r2 = (d * d).sum(axis=1, keepdims=True)
    r = np.sqrt(r2)
    nz = np.sqrt(np.maximum(.01, 1 - np.minimum(r2, 1)))
    fresnel = (1 - nz) ** 2.8
    wave = np.sin(r * 28 - seconds * 3.4 + d[:, :1] * 6) * np.cos(d[:, 1:] * 18 + seconds * 2.1)
    mask = smoothstep(.92, .25, r) * smoothstep(-.2, .75, d[:, 1:]) * smoothstep(.45, -.35, d[:, :1])
    blend = .5 + .5 * wave
    caustic = (np.array([.18, .75, 1]) * (1 - blend) + np.array([.95, .88, .55]) * blend) * np.maximum(0, wave) * mask * .09
    rim_mask = smoothstep(.76, .99, r) * smoothstep(1.05, .96, r)
    rim = (np.array([.22, .68, 1]) * (1 - rim_mask) + np.array([.68, .88, 1]) * rim_mask) * (fresnel * .16 + rim_mask * .26)
    iridescence = np.where(r2 > 1.05, 0, caustic + rim)
    # rgb enters premultiplied; source applies iridescence to straight RGB before compositing halo.
    straight = np.divide(rgb, alpha, out=np.zeros_like(rgb), where=alpha > 1e-4)
    straight = np.clip(straight + iridescence * alpha, 0, 1)
    halo = np.array([track(meta['orb_x'], frame), track(meta['orb_y'], frame)])
    halo_r = np.linalg.norm((px - halo) * [.55, 1.25], axis=1, keepdims=True)
    glow = np.exp(-halo_r * .085) * (.14 + .05 * np.sin(seconds * 3.6))
    bloom_alpha = np.clip(glow * .35, 0, 1)
    combined_alpha = np.clip(alpha + bloom_alpha * (1 - alpha), 0, 1)
    premultiplied = straight * alpha + np.array([.32, .78, 1]) * glow * (1 - alpha * .4)
    # Source clamps combined straight RGB to 0..1 before the transparent framebuffer blend.
    straight = np.divide(premultiplied, combined_alpha, out=np.zeros_like(rgb), where=combined_alpha > 1e-4)
    return np.clip(straight, 0, 1) * combined_alpha, combined_alpha


def check(folder: Path, assets: Path, form='pink'):
    black = form == 'black'
    if black:
        import json
        import re
        source = Path(__file__).parent / 'source/black_idle_animator.js'
        meta = json.loads(re.search(r'^const BLACK_SYNC_F0_DATA = (.+);$', source.read_text(encoding='utf-8-sig'), re.M)[1])['meta']
    atlas = np.array(Image.open(assets / 'frames.webp').convert('RGBA'), dtype=float) / 255
    flow_atlas = np.array(Image.open(assets / 'flow.png').convert('RGB'), dtype=float) / 255
    yy, xx = np.mgrid[1:600:3, 1:592:3]
    uv = (np.column_stack([xx.ravel(), yy.ravel()]) + .5) / RES
    errors = {}
    for seconds in ((0, 2.125, 5.315) if black else (0, 4.125, 10.631)):
        frame = seconds / (5.316 if black else 10.632) * (48 if black else 56)
        index, frac = int(frame), frame % 1

        def tile_image(image, n, w, h):
            return image[n // 8 * h:(n // 8 + 1) * h, n % 8 * w:(n % 8 + 1) * w]

        flow_uv = (uv * RES - ([76, 64] if black else [24, 44])) / ([328, 536] if black else [536, 552])
        inside = ((flow_uv >= 0) & (flow_uv <= 1)).all(axis=1)
        fw, fh = (41, 67) if black else (67, 69)
        flow = (bilinear(tile_image(flow_atlas, index, fw, fh), flow_uv)[:, :2] * 255 - 128) * (.25 if black else .5)
        flow[~inside] = 0
        c0 = sharpen(tile_image(atlas, index, 592, 600), np.clip(uv - frac * flow / RES, .001, .999), black)
        c1 = sharpen(tile_image(atlas, (index + 1) % (48 if black else 56), 592, 600), np.clip(uv + (1 - frac) * flow / RES, .001, .999), black)
        a_diff = c1[:, 3:] - c0[:, 3:]
        weight = (np.where(np.abs(a_diff) > .35, np.clip(frac + a_diff * .18 * np.sin(frac * 3.14159), 0, 1), frac)
                  if black else np.where(np.abs(a_diff) > .22, np.clip((frac + a_diff * .42 - .16) / .68, 0, 1), frac))
        if not black:
            weight = frac * (1 - 4 * frac * (1 - frac)) + weight * (4 * frac * (1 - frac))
        alpha = c0[:, 3:] * (1 - weight) + c1[:, 3:] * weight
        # Transparent viewport readback is premultiplied, like a browser canvas framebuffer.
        rgb = c0[:, :3] * c0[:, 3:] * (1 - weight) + c1[:, :3] * c1[:, 3:] * weight
        if black:
            rgb, alpha = black_shading(rgb, alpha, uv, seconds, meta, frame)
        expected = np.column_stack([rgb, alpha])
        actual = np.array(Image.open(folder / f'pure_{seconds:.3f}.png'), dtype=float)[yy.ravel(), xx.ravel()] / 255
        error = np.abs(actual - expected)
        assert error.mean() < .004, (seconds, error.mean())
        assert np.quantile(error, .99) < .018, (seconds, np.quantile(error, .99))
        errors[str(seconds)] = round(float(error.mean()), 6)
    normal = np.array(Image.open(folder / 'interpolated.png'), dtype=float)
    flipped = np.array(Image.open(folder / 'flipped.png'), dtype=float)
    # Black's authored crop is centered at x=240, so its mirror axis differs from canvas x=296.
    mirror_width = 480 if black else 592
    normal_mirror = normal[:, :mirror_width]
    flipped = flipped[:, :mirror_width][:, ::-1]
    assert np.abs(normal_mirror - flipped).mean() < .5, 'body and procedural effects must mirror together'
    half = np.array(Image.open(folder / 'half_alpha.png'), dtype=float)
    # Effects may overlap one another, but isolated body pixels must inherit parent alpha.
    mask = (normal[:, :, 3] > 240) & (np.indices(normal.shape[:2])[1] > 270) & (np.indices(normal.shape[:2])[1] < 380)
    assert np.quantile(np.abs(half[:, :, 3][mask] - normal[:, :, 3][mask] * .5), .90) < 2
    if not black:
        seam = [np.array(Image.open(folder / f'seam_{time:.5f}.png'), dtype=float)
                for time in (-.001, 0, .001, -1 / 60, 1 / 60)]
        seam_error = np.abs(seam[0] - seam[1]).mean() / 255
        assert seam_error < .0015, ('loop discontinuity', seam_error)
        assert np.quantile(np.abs(seam[0] - seam[1]), .99) < 5, 'loop edge has a visible jump'
        print(f'PASS: full pink body/effects loop seam mean error {seam_error:.6f}')
    print(f'PASS: GLSL reference including loop changes at 3 timestamps ({len(uv)} pixels each), mirror and parent alpha; MAE={errors}')
    return errors


def make_preview(folder: Path, form='pink'):
    output = folder.parent / 'preview_frames'
    output.mkdir(exist_ok=True)
    background = np.array([35, 34, 49], dtype=float)
    for path in sorted(folder.glob('loop_*.png')) + sorted(folder.glob('bubble_*.png')):
        raw = np.array(Image.open(path), dtype=float)
        rgb = np.clip(raw[:, :, :3] + background * (1 - raw[:, :, 3:] / 255), 0, 255).astype('uint8')
        Image.fromarray(rgb).save(output / path.name)
    sheet = Image.new('RGB', (1184, 960), tuple(background.astype(int)))
    pen = ImageDraw.Draw(sheet)
    for i, frame in enumerate((0, 60, 120, 180, 240, 318) if form == 'black' else (0, 126, 247, 408, 492, 638)):
        picture = Image.open(output / f'loop_{frame:04d}.png')
        picture.thumbnail((380, 440))
        sheet.paste(picture, (i % 3 * 394 + (394 - picture.width) // 2, i // 3 * 480 + 30))
        pen.text((i % 3 * 394 + 12, i // 3 * 480 + 12), f'{frame / 60:.2f}s', fill='white')
    sheet.save(folder.parent / 'keyframes.png')
    return output


def check_attack(folder: Path, assets: Path):
    res = np.array([744, 540])
    atlas = np.array(Image.open(assets / 'frames.webp').convert('RGBA'), dtype=float) / 255
    fields = np.array(Image.open(assets / 'flow.png').convert('RGB'), dtype=float) / 255
    yy, xx = np.mgrid[1:540:3, 1:744:3]
    uv = (np.column_stack([xx.ravel(), yy.ravel()]) + .5) / res
    def tile(image, n, w, h):
        return image[n // 8 * h:(n // 8 + 1) * h, n % 8 * w:(n % 8 + 1) * w]
    errors = {}
    for seconds in (0, 1.873, 5.165):
        frame = seconds / 5.166 * 47
        i, frac = int(frame), frame % 1
        flow_uv = (uv * res - [24, 12]) / [712, 464]
        inside = ((flow_uv >= 0) & (flow_uv <= 1)).all(axis=1)
        flow = (bilinear(tile(fields, i, 89, 58), flow_uv)[:, :2] * 255 - 128) * .25
        flow[~inside] = 0
        c0 = sharpen(tile(atlas, i, 744, 540), np.clip(uv - frac * flow / res, .001, .999),
                     resolution=res, blend=.56, step=.82, gain=.14)
        c1 = sharpen(tile(atlas, min(i + 1, 47), 744, 540), np.clip(uv + (1 - frac) * flow / res, .001, .999),
                     resolution=res, blend=.56, step=.82, gain=.14)
        diff = c1[:, 3:] - c0[:, 3:]
        weight = np.where(abs(diff) > .35, np.clip(frac + diff * .16 * np.sin(frac * 3.14159), 0, 1), frac)
        alpha = c0[:, 3:] * (1 - weight) + c1[:, 3:] * weight
        rgb = c0[:, :3] * c0[:, 3:] * (1 - weight) + c1[:, :3] * c1[:, 3:] * weight
        actual = np.array(Image.open(folder / f'pure_{seconds:.3f}.png'), dtype=float)[yy.ravel(), xx.ravel()] / 255
        error = abs(actual - np.column_stack([rgb, alpha]))
        assert error.mean() < .004, (seconds, error.mean())
        assert np.quantile(error, .99) < .018, (seconds, np.quantile(error, .99))
        errors[str(seconds)] = round(float(error.mean()), 6)
    normal = np.array(Image.open(folder / 'attack_right.png'), dtype=float)
    flipped = np.array(Image.open(folder / 'attack_left.png'), dtype=float)
    assert abs(normal[:, ::-1] - flipped).mean() < .5, 'attack body and effects mirror together'
    half = np.array(Image.open(folder / 'attack_half_alpha.png'), dtype=float)
    mask = normal[:, :, 3] > 240
    assert np.quantile(abs(half[:, :, 3][mask] - normal[:, :, 3][mask] * .5), .90) < 2
    assert len(list(folder.glob('attack_[0-9][0-9][0-9][0-9].png'))) == 311
    print(f'PASS: pink attack original GLSL at three timestamps, 311 frames, mirror and inherited alpha: {errors}')
    return errors


def make_attack_preview(folder: Path):
    output = folder.parent / 'preview_frames'
    output.mkdir(exist_ok=True)
    background = np.array([35, 34, 49], dtype=float)
    for path in sorted(folder.glob('attack_[0-9][0-9][0-9][0-9].png')):
        raw = np.array(Image.open(path), dtype=float)
        rgb = np.clip(raw[:, :, :3] + background * (1 - raw[:, :, 3:] / 255), 0, 255).astype('uint8')
        Image.fromarray(rgb).save(output / path.name)
    sheet = Image.new('RGB', (1488, 1120), tuple(background.astype(int)))
    pen = ImageDraw.Draw(sheet)
    for i, frame in enumerate((0, 84, 114, 155, 192, 310)):
        picture = Image.open(output / f'attack_{frame:04d}.png')
        picture.thumbnail((744, 348))
        sheet.paste(picture, (i % 2 * 744 + (744 - picture.width) // 2, i // 2 * 372 + 24))
        pen.text((i % 2 * 744 + 12, i // 2 * 372 + 8),
                 f'{min(frame / 60, 5.166):.3f}s' + (' - midpoint' if frame == 155 else ''), fill='white')
    sheet.save(folder.parent / 'keyframes.jpg')
    return output
