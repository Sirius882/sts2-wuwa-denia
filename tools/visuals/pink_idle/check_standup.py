"""Original standup GLSL, seam endpoints and mirrored production-controller captures."""
from pathlib import Path
import subprocess

import numpy as np
from PIL import Image, ImageDraw
from check_captures import bilinear, sharpen, smoothstep


def check_standup(folder: Path, mod: Path):
    assets = mod / 'images/characters/denia/realtime_pink_standup'
    atlas = np.asarray(Image.open(assets / 'frames.webp').convert('RGBA'), dtype=float) / 255
    fields = np.asarray(Image.open(assets / 'flow.png').convert('RGB'), dtype=float) / 255
    res = np.array([504, 768])
    yy, xx = np.mgrid[1:768:3, 1:504:3]
    uv = (np.column_stack([xx.ravel(), yy.ravel()]) + .5) / res
    px = uv * res
    def tile(image, n, w, h):
        return image[n // 8 * h:(n // 8 + 1) * h, n % 8 * w:(n % 8 + 1) * w]
    errors = {}
    for seconds in (0.0, .413, 1.7, 3.083):
        frame = seconds / 3.083 * 73
        i, frac = int(frame), frame % 1
        flow_uv = (px - [0, 16]) / [504, 744]
        inside = ((flow_uv >= 0) & (flow_uv <= 1)).all(axis=1)
        flow = (bilinear(tile(fields, i, 63, 93), flow_uv)[:, :2] * 255 - 128) * .25
        flow[~inside] = 0
        c0 = sharpen(tile(atlas, i, 504, 768), np.clip(uv - frac * flow / res, .001, .999),
                     resolution=res, blend=.56, step=.82, gain=.14)
        c1 = sharpen(tile(atlas, min(i + 1, 73), 504, 768), np.clip(uv + (1 - frac) * flow / res, .001, .999),
                     resolution=res, blend=.56, step=.82, gain=.14)
        diff = c1[:, 3:] - c0[:, 3:]
        weight = np.where(abs(diff) > .35, np.clip(frac + diff * .16 * np.sin(frac * 3.14159), 0, 1), frac)
        alpha = c0[:, 3:] * (1 - weight) + c1[:, 3:] * weight
        rgb = c0[:, :3] * c0[:, 3:] * (1 - weight) + c1[:, :3] * c1[:, 3:] * weight
        straight = np.divide(rgb, alpha, out=np.zeros_like(rgb), where=alpha > 1e-4)
        if seconds <= .70:
            d = (px - [257.22, 539.82]) / 145.5
            r2 = (d * d).sum(axis=1, keepdims=True)
            nz = np.sqrt(np.maximum(.01, 1 - np.minimum(r2, 1)))
            fresnel = (1 - nz) ** 2.6
            phase = 6.28318 * (1.8 * nz + .14 * d[:, :1] - .11 * d[:, 1:]) + seconds * 1.6
            spec = .5 + .5 * np.cos(phase + [0, 2.094, 4.188])
            pastel = np.array([1, .74, .95]) * .48 + spec * .52
            rim = smoothstep(.58, .99, np.sqrt(r2)) * smoothstep(1.03, .95, np.sqrt(r2))
            iri = pastel * (fresnel * .10 + rim * .15) * (1 - smoothstep(.56, .70, seconds))
            straight = np.clip(straight + np.where(r2 > 1.03, 0, iri) * alpha, 0, 1)
        expected = np.column_stack([straight * alpha, alpha])
        actual = np.asarray(Image.open(folder / f'standup_glsl_{seconds:.3f}.png'), dtype=float)[yy.ravel(), xx.ravel()] / 255
        error = abs(actual - expected)
        assert error.mean() < .004, (seconds, error.mean())
        assert np.quantile(error, .99) < .018, (seconds, np.quantile(error, .99))
        errors[str(seconds)] = round(float(error.mean()), 6)
    def pixels(name):
        return np.asarray(Image.open(folder / (name + '.png')), dtype=float)
    for tag in ('single', 'combo'):
        assert abs(pixels(tag + '_idle_before') - pixels(tag + '_entry_zero')).max() <= 1, 'entry preserves current idle exactly'
        assert abs(pixels(tag + '_attack_start') - pixels(tag + '_attack_original')).max() <= 1, 'exit preserves original attack exactly'
        outgoing, incoming, middle = (pixels(f'{tag}_join_{t:.3f}') for t in (2.923, 3.083, 3.003))
        opaque = (outgoing[:, :, 3] > 250) & (incoming[:, :, 3] > 250)
        assert np.quantile(middle[:, :, 3][opaque], .1) > 249, 'crossfade must retain opaque body brightness'
        yy_body, xx_body = np.indices(outgoing.shape[:2])
        for low, high, tolerance in ((405, 445, 5), (700, 750, 15)):
            centers = []
            for image in (outgoing, incoming):
                body = (image[:, :, 3] > 180) & (yy_body > low) & (yy_body < high) & (xx_body > 570) & (xx_body < 715)
                centers.append(np.array([xx_body[body].mean(), yy_body[body].mean()]))
            assert abs(centers[0] - centers[1]).max() < tolerance, (tag, 'head/feet endpoint alignment', centers)
        for t in (0, .07, .14, .7, 1.6, 2.7, 2.923, 3.003, 3.083, 3.183):
            normal, mirror = pixels(f'{tag}_join_{t:.3f}'), pixels(f'{tag}_mirror_{t:.3f}')
            assert abs(normal[:, ::-1] - mirror).mean() < .15, (tag, t, 'all three bodies mirror around same anchor')
        count = len(list(folder.glob(f'standup_{tag}_[0-9][0-9][0-9][0-9].png')))
        assert count == (249 if tag == 'single' else 534), (tag, count)
    print(f'PASS: original standup GLSL {errors}; exact idle/attack endpoints and both mirrored joins')
    return {'original_glsl_mean_error': errors, 'exact_seam_endpoints': True,
            'opaque_handoff': True, 'head_and_feet_aligned': True}


def make_preview(folder: Path, output: Path):
    background = np.array([35, 34, 49], dtype=float)
    target = folder.parent / 'preview_frames';target.mkdir(exist_ok=True)
    def composite(path):
        raw = np.asarray(Image.open(path), dtype=float)
        return Image.fromarray(np.clip(raw[:, :, :3] + background * (1 - raw[:, :, 3:] / 255), 0, 255).astype('uint8'))
    for tag in ('single', 'combo'):
        for path in sorted(folder.glob(f'standup_{tag}_[0-9][0-9][0-9][0-9].png')):
            composite(path).save(target / path.name)
        result = subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '30',
            '-i', str(target / f'standup_{tag}_%04d.png'), '-pix_fmt', 'yuv420p',
            '-c:v', 'libx264', '-crf', '18', str(output / f'pink_standup_{tag}.mp4')], capture_output=True)
        if result.returncode:
            raise RuntimeError(result.stderr.decode(errors='replace'))
    times = (0, .07, .14, .7, 1.6, 2.7, 2.923, 3.003, 3.083, 3.183)
    sheet = Image.new('RGB', (1600, 1000), tuple(background.astype(int)));pen = ImageDraw.Draw(sheet)
    for row, tag in enumerate(('single', 'combo')):
        for i, t in enumerate(times):
            picture = composite(folder / f'{tag}_join_{t:.3f}.png').crop((400, 300, 880, 810))
            picture.thumbnail((320, 220));x, y = i % 5 * 320, (row * 2 + i // 5) * 250
            sheet.paste(picture, (x + (320 - picture.width) // 2, y + 24))
            pen.text((x + 8, y + 7), f'{tag} {t:.3f}s', fill='white')
    sheet.save(output / 'pink_standup_joins.jpg')
