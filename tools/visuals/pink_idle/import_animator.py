"""Extract the supplied animator's existing images/flow; never render new animation frames."""
import base64
import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import re

from PIL import Image

HERE = Path(__file__).resolve().parent
MOD = HERE.parents[2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--form', choices=('pink', 'black', 'black_rise', 'pink_1hit', 'dark_1hit'), default='pink')
    args = parser.parse_args()
    attack = args.form in ('pink_1hit', 'dark_1hit')
    dark_attack = args.form == 'dark_1hit'
    rise = args.form == 'black_rise'
    black = args.form in ('black', 'black_rise', 'dark_1hit')
    output = MOD / f'images/characters/denia/realtime_{args.form}'
    prefix = 'RISE_' if rise else 'BLACK_' if black else ''
    source = HERE / (f'source/{args.form}_animator.js' if rise or attack else f'source/{args.form}_idle_animator.js')
    text = source.read_text(encoding='utf-8-sig')

    def constant(name):
        if attack and not dark_attack:
            name = {'SYNC_F0_DATA': 'SYNC_ATK_F0_DATA', 'HD_CROP_CFG': 'ATK_CROP_CFG',
                    'HD_WEBP_SIZES': 'ATK_WEBP_SIZES', 'HD_WEBP_PACK_B64': 'ATK_WEBP_PACK_B64',
                    'F0_WEBP_B64': 'ATK_F0_WEBP_B64', 'FLOW48_GZ_B64': 'ATK_FLOW48_GZ_B64'}[name]
        match = re.search(r'^const ' + prefix + name + r' = (.+);\s*$', text, re.MULTILINE)
        if not match:
            raise ValueError(f'Missing animator constant: {name}')
        return json.loads(match[1])

    data = constant('SYNC_F0_DATA')
    meta = data['meta']
    crop = constant('HD_CROP_CFG')
    sizes = constant('HD_WEBP_SIZES')
    pack = base64.b64decode(constant('HD_WEBP_PACK_B64'), validate=True)
    flow = gzip.decompress(base64.b64decode(constant('FLOW48_GZ_B64' if black or attack else 'FLOW56_GZ_B64'), validate=True))
    count, w, h = meta['frames'], meta['w'], meta['h']
    fw, fh = crop['flowW'], crop['flowH']
    assert count == (96 if dark_attack else 48 if black or attack else 56)
    assert (w, h) == ((960, 480) if dark_attack else (744, 540) if attack else (592, 600))
    assert len(sizes) == count and sum(sizes[1:]) == len(pack)
    assert len(flow) == count * fw * fh * 2
    rows = (count + 7) // 8
    atlas = Image.new('RGBA', (w * 8, h * rows))
    flow_atlas = Image.new('RGB', (fw * 8, fh * rows), (128, 128, 128))
    offset = 0
    for index in range(count):
        if index == 0:
            encoded = base64.b64decode(constant('F0_WEBP_B64'), validate=True)
        else:
            encoded = pack[offset:offset + sizes[index]]
            offset += sizes[index]
        frame = Image.open(io.BytesIO(encoded)).convert('RGBA')
        x, y, cw, ch = meta['frameRects'][index] if attack and not dark_attack else (crop['xOff'], crop['yOff'], crop['cw'], crop['ch'])
        assert frame.size == (cw, ch) and 0 <= x <= w - cw and 0 <= y <= h - ch
        atlas.paste(frame, (index % 8 * w + x, index // 8 * h + y))
        raw = flow[index * fw * fh * 2:(index + 1) * fw * fh * 2]
        pixels = bytearray(fw * fh * 3)
        for i in range(fw * fh):
            pixels[i * 3] = (raw[i * 2] + 128) % 256
            pixels[i * 3 + 1] = (raw[i * 2 + 1] + 128) % 256
            pixels[i * 3 + 2] = 128
        flow_atlas.paste(Image.frombytes('RGB', (fw, fh), bytes(pixels)), (index % 8 * fw, index // 8 * fh))
    output.mkdir(parents=True, exist_ok=True)
    atlas.save(output / 'frames.webp', lossless=True, exact=True, method=6)
    flow_atlas.save(output / 'flow.png')
    # Existing attacks/dodges use the old idle's first pose as their endpoint.
    if not rise and not attack:
        legacy_name = 'dark' if black else 'pink'
        legacy = Image.open(MOD / f'images/characters/denia/sprite_sheets/denia_{legacy_name}_idle.png')
        legacy.crop((0, 0, 264, 528)).save(output / 'action_rest.png')
    # A native Resource keeps the metadata in the PCK without an export include filter.
    lines = ['[gd_resource type="Resource" format=3]', '', '[resource]']
    tracking = dict(meta)
    if rise:
        tracking.update(orb_x=meta['halo_x'], orb_y=meta['halo_y'])
    keys = ('bubble_cy', 'orb_x', 'orb_y', 'bubble_r_dyn', 'bubble_alpha', 'slash_alpha', 'comp_x', 'comp_y') if dark_attack else () if attack else ('bubble_cy', 'orb_x', 'orb_y') + (('bubble_cx', 'bubble_rx', 'bubble_ry') if rise else ())
    for key in keys:
        assert len(tracking[key]) == count
        lines.append(f'metadata/{key} = PackedFloat32Array({", ".join(map(str, tracking[key]))})')
    lines.append(f'metadata/source_sha256 = "{hashlib.sha256(source.read_bytes()).hexdigest()}"')
    (output / 'tracking.tres').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f'Extracted {count} original WebP frames and flow fields to {output}')


if __name__ == '__main__':
    main()
