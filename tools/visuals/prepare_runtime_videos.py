"""Restore local runtime OGV assets before export; large videos are not stored in Git."""
import hashlib
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
