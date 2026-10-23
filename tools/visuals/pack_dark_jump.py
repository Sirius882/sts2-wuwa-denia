"""Pack author_dark_jump's 768x960 renders on a fixed 516x704 canvas.

Run with ordinary Python/Pillow: --renders ROOT/.cache/.../renders --project ROOT/myModresources/denia.
The historical projection mapping is fixed; never recenter individual frames.
"""
import argparse
from pathlib import Path
import re
from PIL import Image


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--renders', type=Path, required=True)
    p.add_argument('--project', type=Path, required=True)
    a = p.parse_args()
    atlas = Image.new('RGBA', (516 * 8, 704 * 9))
    for f in range(1, 73):
        raw = Image.open(a.renders / f'frame_{f:04}.png').convert('RGBA')
        assert raw.size == (768, 960)
        # Recovered from the unchanged first pose against the accepted atlas.
        scale = .746
        cell = raw.transform((516, 704), Image.Transform.AFFINE,
            (1 / scale, 0, -46.5, 0, 1 / scale, 91.5 - 64 / scale), Image.Resampling.BICUBIC)
        bounds = cell.getbbox()
        assert bounds and bounds[0] >= 4 and bounds[1] >= 4 and bounds[2] <= 512 and bounds[3] <= 700, (f, bounds)
        atlas.paste(cell, (((f - 1) % 8) * 516, ((f - 1) // 8) * 704))
    atlas.save(a.project / 'images/characters/denia/sprite_sheets/denia_dark_attack.png')
    scene = a.project / 'scenes/creature_visuals/denia_pink.tscn'
    text = scene.read_text(encoding='utf8')
    pattern = r'(\[sub_resource type="AtlasTexture" id="dark_attack_(\d+)"\]\natlas = ExtResource\("dark_attack"\)\n)region = Rect2\([^\n]+\)'
    def region(m):
        index = int(m[2])
        return m[1] + f'region = Rect2({index % 8 * 516}, {index // 8 * 704}, 516, 704)'
    text, count = re.subn(pattern, region, text)
    assert count == 70, count  # First/last use the unchanged idle AtlasTexture.
    scene.write_text(text, encoding='utf8', newline='\n')
    print('PACK_PASS: 72 frames; fixed scale/centre; 70 regions; transparent borders')


if __name__ == '__main__':
    main()
