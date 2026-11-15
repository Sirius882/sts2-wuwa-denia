"""Encode the aligned 3840px ComfyUI frames as a maximum-quality native Theora clip."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
CONFIG = HERE / 'execution_video_remaster.json'
OUTPUT = ROOT / 'myModresources/denia/videos'
MASTER = ROOT / 'artResources/Denia/人物形象与动画/denia_execution_middle.ogv'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    config = json.loads(CONFIG.read_text(encoding='utf-8'))
    frames = ROOT / config['frames_directory']
    hashes = {}
    for index in range(config['frame_count']):
        path = frames / f'frame_{index:04d}.png'
        if not path.is_file():
            raise RuntimeError(f'Missing ComfyUI frame: {path}')
        hashes[path.name] = sha(path)
    OUTPUT.mkdir(exist_ok=True)
    destination = OUTPUT / 'denia_execution_middle.ogv'
    scratch = ROOT / '.cache'
    scratch.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='execution-encode-', dir=scratch) as folder:
        encoded = Path(folder) / 'video.ogv'
        subprocess.run([
            'ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '30',
            '-start_number', '0', '-i', str(frames / 'frame_%04d.png'),
            '-frames:v', str(config['frame_count']), '-an', '-r', '30', '-fps_mode', 'cfr',
            '-c:v', 'libtheora', '-q:v', '10', '-pix_fmt', 'yuv420p', str(encoded),
        ], check=True)
        shutil.copyfile(encoded, destination)
    probe = json.loads(subprocess.check_output([
        'ffprobe', '-v', 'error', '-count_frames', '-show_streams', '-of', 'json', str(destination)
    ]))['streams'][0]
    assert probe['codec_name'] == 'theora'
    assert (probe['width'], probe['height']) == (3840, 1772)
    # Theora可用空包表示重复帧，按固定30 FPS的时间格检查完整时长。
    assert probe['time_base'] == '1/30' and int(probe['duration_ts']) == 137, probe
    MASTER.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(destination, MASTER)
    manifest = {'video_sha256': sha(destination), 'video_bytes': destination.stat().st_size,
        'theora_quality': 10, 'width': 3840, 'height': 1772, 'fps': 30, 'frame_intervals': 137,
        'duration': float(probe['duration']), 'local_master': MASTER.relative_to(ROOT).as_posix(),
        'source_sha256': config['source_sha256'], 'frames_sha256': hashes}
    (HERE / 'execution_video.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print(f'{destination.name}: 137 frame intervals, {probe["nb_read_frames"]} decoded pictures, {probe["duration"]} seconds')


if __name__ == '__main__':
    main()
