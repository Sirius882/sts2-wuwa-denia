"""Extract the aligned soundtrack; keep source PTS, including the 106 ms video lead-in."""
import hashlib
import json
from pathlib import Path
import subprocess
import wave

ROOT = Path(__file__).resolve().parents[4]
ALIGNMENT = Path(__file__).with_name('execution_audio_alignment.json')


def main():
    alignment = json.loads(ALIGNMENT.read_text(encoding='utf-8'))
    source = ROOT / 'artResources/Denia/人物形象与动画' / alignment['origin_file']
    assert hashlib.sha256(source.read_bytes()).hexdigest() == alignment['origin_sha256'], 'Audio source changed; realign first'
    destination = ROOT / 'myModresources/denia/sounds/denia_execution.wav'
    start = alignment['audio_start_sample']
    count = alignment['audio_sample_count']
    subprocess.run([
        'ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
        '-map', '0:a:0', '-af', f'atrim=start_sample={start}:end_sample={start + count},asetpts=PTS-STARTPTS',
        '-ar', '48000', '-ac', '2', '-c:a', 'pcm_s16le', str(destination),
    ], check=True)
    with wave.open(str(destination)) as audio:
        assert (audio.getframerate(), audio.getnchannels(), audio.getnframes()) == (48000, 2, count)
    print(f'{destination.name}: {count} stereo samples, begins at source {start / 48000:.3f}s')


if __name__ == '__main__':
    main()
