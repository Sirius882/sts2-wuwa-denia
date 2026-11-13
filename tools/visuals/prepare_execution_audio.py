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
    video_end = alignment['fade_out_start_sample']
    fade_in = alignment['fade_in_samples']
    fade_out = alignment['fade_out_samples']
    delays = '|'.join(map(str, alignment['tail_echo_delays_ms']))
    gains = '|'.join(map(str, alignment['tail_echo_gains']))
    # 源片尾部不足三秒；仅在视频结束后的原声上追加衰减尾响，不循环台词或改变音画段。
    filters = (
        f'[0:a:0]atrim=start_sample={start},asetpts=PTS-STARTPTS,asplit=2[body][tail];'
        f'[body]atrim=end_sample={video_end}[head];'
        f'[tail]atrim=start_sample={video_end},asetpts=PTS-STARTPTS,'
        f'aecho=1:1:{delays}:{gains}[ring];'
        f'[head][ring]concat=n=2:v=0:a=1,apad,atrim=end_sample={count},'
        f'afade=t=in:ss=0:ns={fade_in}:curve=hsin,'
        f'afade=t=out:ss={video_end}:ns={fade_out}:curve=hsin[out]'
    )
    subprocess.run([
        'ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source),
        '-filter_complex', filters, '-map', '[out]',
        '-ar', '48000', '-ac', '2', '-c:a', 'pcm_s16le', str(destination),
    ], check=True)
    with wave.open(str(destination)) as audio:
        assert (audio.getframerate(), audio.getnchannels(), audio.getnframes()) == (48000, 2, count)
        assert audio.readframes(1) == b'\0\0\0\0', 'Fade-in must start silently'
        audio.setpos(count - 1)
        assert audio.readframes(1) == b'\0\0\0\0', 'Fade-out must end silently'
    print(f'{destination.name}: {count} stereo samples, begins at source {start / 48000:.3f}s')


if __name__ == '__main__':
    main()
