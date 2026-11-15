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


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    subprocess.run([sys.executable, '-B', str(HERE / 'prepare_character_select_video.py'), '--ensure'], check=True)
    manifest = json.loads((HERE / 'execution_video.json').read_text(encoding='utf-8'))
    expected_hash = manifest['video_sha256']
    destination = MOD / 'videos/denia_execution_middle.ogv'
    if destination.is_file() and sha(destination) == expected_hash:
        print(f'{destination.name}: verified local video')
        return
    master = ROOT / manifest['local_master']
    if not master.is_file() or sha(master) != expected_hash:
        raise RuntimeError(f'Missing or changed execution video master: {master}')
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(master, destination)
    print(f'{destination.name}: restored from {master}')


if __name__ == '__main__':
    main()
