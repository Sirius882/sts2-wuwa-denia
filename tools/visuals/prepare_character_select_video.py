"""Convert the authored character-select MP4 to native Theora without resizing or retiming."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import shutil
import tempfile

ROOT = Path(__file__).resolve().parents[4]
MOD = ROOT / 'myModresources/denia'
SOURCE = ROOT / 'artResources/Denia/选人界面背景视频.mp4'
VIDEO = MOD / 'videos/denia_character_select_bg.ogv'
POSTER = MOD / 'images/char_select/denia_bg_poster.jpg'
MASTER = ROOT / 'artResources/Denia/人物形象与动画/denia_character_select_bg.ogv'
MANIFEST = Path(__file__).parent / 'character_select_video.json'


def probe(path):
    return json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams',
        '-show_format', '-of', 'json', str(path)], encoding='utf-8'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def record_manifest(source, quality):
    output = probe(VIDEO)
    encoded = output['streams'][0]
    assert encoded['codec_name'] == 'theora' and len(output['streams']) == 1
    assert (encoded['width'], encoded['height'], encoded['r_frame_rate']) == (3840, 2160, '60/1')
    assert abs(float(output['format']['duration']) - float(source['format']['duration'])) < 1 / 60
    manifest = {'source': SOURCE.relative_to(ROOT).as_posix(), 'source_sha256': sha(SOURCE),
        'video_sha256': sha(VIDEO), 'poster_sha256': sha(POSTER), 'width': 3840, 'height': 2160,
        'fps': 60, 'duration': float(output['format']['duration']), 'audio': False,
        'theora_quality': quality, 'video_bytes': VIDEO.stat().st_size,
        'average_bitrate': int(output['format']['bit_rate']),
        'local_master': MASTER.relative_to(ROOT).as_posix()}
    MANIFEST.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(json.dumps(manifest, ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--quality', type=float, help='Theora quality, 0–10 (default: 10; --ensure retains recorded quality)')
    parser.add_argument('--ensure', action='store_true', help='Reuse matching local video instead of re-encoding')
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8')) if MANIFEST.is_file() else None
    if args.quality is None:
        args.quality = manifest['theora_quality'] if args.ensure and manifest else 10
    if not 0 <= args.quality <= 10:
        parser.error('--quality must be between 0 and 10')
    if args.ensure and manifest:
        if (manifest['theora_quality'] == args.quality
                and (not SOURCE.exists() or sha(SOURCE) == manifest['source_sha256'])
                and POSTER.is_file() and sha(POSTER) == manifest['poster_sha256']):
            if not VIDEO.is_file() or sha(VIDEO) != manifest['video_sha256']:
                if MASTER.is_file() and sha(MASTER) == manifest['video_sha256']:
                    VIDEO.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(MASTER, VIDEO)
            if VIDEO.is_file() and sha(VIDEO) == manifest['video_sha256']:
                print(f'{VIDEO.name}: verified local quality {args.quality:g} video')
                return
    if not SOURCE.is_file():
        parser.error(f'Missing source MP4 and matching local master: {SOURCE}; {MASTER}')
    source = probe(SOURCE)
    stream = next(s for s in source['streams'] if s['codec_type'] == 'video')
    assert (stream['width'], stream['height'], stream['r_frame_rate']) == (3840, 2160, '60/1')
    for folder in (VIDEO.parent, POSTER.parent, MASTER.parent, ROOT / '.cache'):
        folder.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='denia-select-video-', dir=ROOT / '.cache') as work:
        encoded_video = Path(work) / 'video.ogv'
        poster = Path(work) / 'poster.jpg'
        subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(SOURCE),
            '-map', '0:v:0', '-an', '-c:v', 'libtheora', '-q:v', str(args.quality),
            '-pix_fmt', 'yuv420p', str(encoded_video)], check=True)
        subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(SOURCE),
            '-frames:v', '1', '-q:v', '2', str(poster)], check=True)
        shutil.copyfile(encoded_video, VIDEO)
        shutil.copyfile(encoded_video, MASTER)
        shutil.copyfile(poster, POSTER)
    record_manifest(source, args.quality)


if __name__ == '__main__':
    main()
