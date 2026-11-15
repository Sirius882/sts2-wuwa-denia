"""Check frame identity, framing and adjacent-frame residuals against the aligned source."""
import argparse
import hashlib
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scratch', type=Path, default=ROOT / '.cache/execution-remaster-20261115')
    parser.add_argument('--partial', action='store_true')
    args = parser.parse_args()
    config = json.loads((HERE / 'execution_video_remaster.json').read_text(encoding='utf-8'))
    frames = ROOT / config['frames_directory']
    report = {'frames': {}, 'adjacent_pairs': {}, 'expected_frames': config['frame_count']}
    previous_source = previous_output = None
    previous_index = -2
    for index in range(config['frame_count']):
        path = frames / f'frame_{index:04d}.png'
        if not path.exists() and args.partial:
            continue
        with Image.open(path) as image:
            assert image.size == (3840, 1772), (path, image.size)
            output = np.asarray(image.convert('RGB').resize((960, 443), Image.Resampling.LANCZOS), dtype=np.float32) / 255
        source_path = args.scratch / 'input' / path.name
        with Image.open(source_path) as image:
            source = np.asarray(image.convert('RGB').resize((960, 443), Image.Resampling.LANCZOS), dtype=np.float32) / 255
        residual = output - source
        mse = float(np.mean(residual ** 2))
        assert mse < 0.008, f'Unexpected framing/color change at {index}: {mse}'
        report['frames'][str(index)] = {'mse': mse, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
        if previous_index == index - 1:
            source_delta = float(np.mean((source - previous_source) ** 2))
            output_delta = float(np.mean((output - previous_output) ** 2))
            # Measure enhancement residual after motion compensation; never average across cuts.
            previous_gray = cv2.cvtColor(np.uint8(previous_source * 255), cv2.COLOR_RGB2GRAY)
            gray = cv2.cvtColor(np.uint8(source * 255), cv2.COLOR_RGB2GRAY)
            flow = cv2.calcOpticalFlowFarneback(gray, previous_gray, None, .5, 3, 19, 3, 5, 1.2, 0)
            grid_x, grid_y = np.meshgrid(np.arange(960, dtype=np.float32), np.arange(443, dtype=np.float32))
            warped = cv2.remap(previous_output - previous_source, grid_x + flow[..., 0],
                grid_y + flow[..., 1], cv2.INTER_LINEAR, borderMode=cv2.BORDER_REFLECT)
            report['adjacent_pairs'][str(index)] = {'source_delta_mse': source_delta,
                'output_delta_mse': output_delta, 'motion_compensated_residual_mse': float(np.mean((residual - warped) ** 2))}
        previous_source, previous_output, previous_index = source, output, index
    selected = [i for i in [0, 15, 32, 49, 50, 51, 90, 115, 136] if str(i) in report['frames']]
    sheet = Image.new('RGB', (1440, 356 * len(selected)), '#202020')
    draw = ImageDraw.Draw(sheet)
    for row, index in enumerate(selected):
        for col, folder in enumerate([args.scratch / 'input', frames]):
            with Image.open(folder / f'frame_{index:04d}.png') as image:
                image = image.convert('RGB').resize((720, 332), Image.Resampling.LANCZOS)
            sheet.paste(image, (col * 720, row * 356 + 24))
            draw.text((col * 720 + 4, row * 356 + 5), f'{"SOURCE" if col == 0 else "ANIMAGINE 4K"} frame {index}', fill='white')
    args.scratch.mkdir(parents=True, exist_ok=True)
    sheet.save(args.scratch / 'remaster-contact.jpg', quality=95)
    report['checked_frames'] = len(report['frames'])
    (args.scratch / 'frame-quality.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'checked_frames': report['checked_frames'], 'max_source_mse': max(x['mse'] for x in report['frames'].values()),
        'max_motion_compensated_residual_mse': max((x['motion_compensated_residual_mse'] for x in report['adjacent_pairs'].values()), default=0)}))


if __name__ == '__main__':
    main()
