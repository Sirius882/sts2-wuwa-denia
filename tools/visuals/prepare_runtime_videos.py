"""Restore local runtime OGV assets before export; large videos are not stored in Git."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
MOD = ROOT / 'myModresources/denia'
EXECUTION_HASH = '8252dbd64aa37a16800f9ad50aede9e9d313142b7b9fa84cd2af9de7b9787e2d'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    subprocess.run([sys.executable, '-B', str(HERE / 'prepare_character_select_video.py'), '--ensure'], check=True)
    source = ROOT / 'artResources/Denia/选人界面背景视频.mp4'
    video_manifest = json.loads((HERE / 'character_select_video.json').read_text(encoding='utf-8'))
    destination = MOD / 'videos/denia_character_select_bg.mp4'
    if not destination.is_file() or sha(destination) != video_manifest['source_sha256']:
        if not source.is_file() or sha(source) != video_manifest['source_sha256']:
            raise RuntimeError(f'Missing original hardware-decoded character-select video: {source}')
        shutil.copyfile(source, destination)
    native_manifest = json.loads((HERE / 'native_video/native_video.json').read_text(encoding='utf-8'))
    native = MOD / 'native/denia_video.bin'
    if not native.is_file() or sha(native) != native_manifest['binary_sha256']:
        subprocess.run([sys.executable, '-B', str(HERE / 'native_video/build_native.py')], check=True)
    if hashlib.sha256((HERE / 'native_video/denia_video.cpp').read_text(encoding='utf-8').encode('utf-8')).hexdigest() != native_manifest['source_sha256']:
        raise RuntimeError('Native video source changed: run native_video/build_native.py before packing')
    print('Character-select MP4: original bytes verified, native decoder available')
    destination = MOD / 'videos/denia_execution_middle.ogv'
    if destination.is_file() and sha(destination) == EXECUTION_HASH:
        print(f'{destination.name}: verified local video')
        return
    master = ROOT / 'artResources/Denia/游戏视频/denia_execution_middle.ogv'
    if not master.is_file() or sha(master) != EXECUTION_HASH:
        raise RuntimeError(f'Missing or changed execution video master: {master}')
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(master, destination)
    print(f'{destination.name}: restored from {master}')


if __name__ == '__main__':
    main()
