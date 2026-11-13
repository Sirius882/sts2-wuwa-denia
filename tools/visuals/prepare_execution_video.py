"""Rebuild the execution's character-only Theora clip from the original video."""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[4]
SOURCE = ROOT / 'artResources/Denia/人物形象与动画/三级集谐处决动画素材.mp4'
OUTPUT = ROOT / 'myModresources/denia/videos'


def main():
    OUTPUT.mkdir(exist_ok=True)
    destination = OUTPUT / 'denia_execution_middle.ogv'
    subprocess.run([
        'ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(SOURCE),
        '-vf', 'trim=start_frame=87:end_frame=191,setpts=PTS-STARTPTS',
        '-an', '-r', '30', '-fps_mode', 'cfr', '-c:v', 'libtheora', '-q:v', '8',
        str(destination),
    ], check=True)
    probe = json.loads(subprocess.check_output([
        'ffprobe', '-v', 'error', '-count_frames', '-show_streams', '-of', 'json', str(destination)
    ]))['streams'][0]
    assert probe['codec_name'] == 'theora'
    assert (probe['width'], probe['height']) == (1920, 886)
    assert int(probe['nb_read_frames']) == 104, probe
    print(f'{destination.name}: {probe["nb_read_frames"]} frames, {probe["duration"]} seconds')


if __name__ == '__main__':
    main()
