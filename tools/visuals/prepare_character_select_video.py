"""Convert the authored character-select MP4 to native Theora without resizing or retiming."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
MOD = ROOT / 'myModresources/denia'
SOURCE = ROOT / 'artResources/Denia/选人界面背景视频.mp4'
VIDEO = MOD / 'videos/denia_character_select_bg.ogv'
POSTER = MOD / 'images/char_select/denia_bg_poster.jpg'


def probe(path):
    return json.loads(subprocess.check_output(['ffprobe', '-v', 'error', '-show_streams',
        '-show_format', '-of', 'json', str(path)], encoding='utf-8'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    source = probe(SOURCE)
    stream = next(s for s in source['streams'] if s['codec_type'] == 'video')
    assert (stream['width'], stream['height'], stream['r_frame_rate']) == (3840, 2160, '60/1')
    VIDEO.parent.mkdir(exist_ok=True);POSTER.parent.mkdir(exist_ok=True)
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(SOURCE),
        '-map', '0:v:0', '-an', '-c:v', 'libtheora', '-q:v', '8', '-pix_fmt', 'yuv420p', str(VIDEO)], check=True)
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(SOURCE),
        '-frames:v', '1', '-q:v', '2', str(POSTER)], check=True)
    output = probe(VIDEO)
    encoded = output['streams'][0]
    assert encoded['codec_name'] == 'theora' and len(output['streams']) == 1
    assert (encoded['width'], encoded['height'], encoded['r_frame_rate']) == (3840, 2160, '60/1')
    assert abs(float(output['format']['duration']) - float(source['format']['duration'])) < 1 / 60
    manifest = {'source': str(SOURCE.relative_to(ROOT)), 'source_sha256': sha(SOURCE),
        'video_sha256': sha(VIDEO), 'poster_sha256': sha(POSTER), 'width': 3840, 'height': 2160,
        'fps': 60, 'duration': float(output['format']['duration']), 'audio': False, 'theora_quality': 8}
    (Path(__file__).parent / 'character_select_video.json').write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(json.dumps(manifest, ensure_ascii=False))


if __name__ == '__main__':
    main()
