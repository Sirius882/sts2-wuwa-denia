"""Composite the native map brush captures for a reusable visual preview."""
from pathlib import Path
import subprocess
import numpy as np
from PIL import Image


def make_preview(folder: Path, output: Path):
    frames = folder.parent / 'preview_frames';frames.mkdir(exist_ok=True)
    bg = np.array([37, 34, 51], dtype=float)
    for path in sorted(folder.glob('map_brush_[0-9][0-9][0-9][0-9].png')):
        raw = np.asarray(Image.open(path), dtype=float)
        Image.fromarray(np.clip(raw[:, :, :3] + bg * (1 - raw[:, :, 3:] / 255), 0, 255).astype('uint8')).save(frames / path.name)
    result = subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '60',
        '-i', str(frames / 'map_brush_%04d.png'), '-pix_fmt', 'yuv420p', '-c:v', 'libx264', '-crf', '18', str(output / 'map_brush.mp4')], capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode(errors='replace'))
    Image.open(frames / 'map_brush_0100.png').save(output / 'map_brush.jpg')
