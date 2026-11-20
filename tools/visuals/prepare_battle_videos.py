"""Prepare seekable, transparent combat video frames from the local camera masters."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import io
import json
from pathlib import Path
import shutil
import struct
import subprocess

HERE = Path(__file__).resolve().parent
MOD = HERE.parents[1]
ROOT = MOD.parents[1]
ART = ROOT / 'artResources/Denia/人物形象与动画'
MASTERS = ART / '战斗视频运行时'
RECIPE = HERE / 'battle_videos.json'
STANDUP = MOD / 'images/characters/denia/realtime_pink_standup/frames.webp'
HEIGHT = 432
MAGIC = b'DBV2'


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def source_path(name, clip):
    return ART / f'{name}_video.{"mp4" if clip.get("alpha_atlas") else "mkv"}'


def sample_frames(clip):
    """Keep native time and retain single-frame impacts when sampling 60 fps sources at 30 fps."""
    count, fps = clip['frames'], clip['fps']
    impacts = clip.get('impact_frames', [])
    frame_indices = sorted(set(range(0, count, 2 if fps == 60 else 1)) | set(impacts) | {count - 1})
    return [(index / fps, index) for index in frame_indices]


def smooth_camera(camera, index):
    for left, right in zip(camera, camera[1:]):
        if index <= right[0]:
            weight = (index - left[0]) / (right[0] - left[0])
            weight = weight * weight * (3 - 2 * weight)
            return [a + (b - a) * weight for a, b in zip(left[1:], right[1:])]
    return camera[-1][1:]


def warp(pixels, scale, anchor_x, anchor_y, premultiplied=False):
    import cv2
    import numpy as np

    # Straight-alpha input is filtered in premultiplied space. Each frame keeps its own tight crop
    # and offset so a full-screen slash does not inflate every ordinary body frame.
    alpha = pixels[:, :, 3]
    bounds = cv2.boundingRect(np.uint8(alpha >= 3))
    x, y, w, h = bounds
    if not w or not h:
        return (0, 0, np.zeros((64, 64, 4), dtype=np.float32))
    x0 = int(np.floor((x - anchor_x) * scale)) - 4
    y0 = int(np.floor((y - anchor_y) * scale)) - 4
    width = int(np.ceil(((x + w - anchor_x) * scale + 4 - x0) / 64)) * 64
    height = int(np.ceil(((y + h - anchor_y) * scale + 4 - y0) / 64)) * 64
    left, top = max(0, x - 4), max(0, y - 4)
    rgba = pixels[top:y + h + 4, left:x + w + 4].astype(np.float32)
    if not premultiplied:
        rgba[:, :, :3] *= rgba[:, :, 3:4] / 255
    matrix = np.array([[scale, 0, (left - anchor_x) * scale - x0],
                       [0, scale, (top - anchor_y) * scale - y0]])
    result = cv2.warpAffine(rgba, matrix, (width, height), flags=cv2.INTER_LINEAR)
    return x0, y0, result


def mix(first, second, weight):
    import numpy as np

    x, y = min(first[0], second[0]), min(first[1], second[1])
    right = max(a + image.shape[1] for a, _, image in (first, second))
    bottom = max(b + image.shape[0] for _, b, image in (first, second))
    width = (right - x + 63) // 64 * 64
    height = (bottom - y + 63) // 64 * 64
    result = np.zeros((height, width, 4), dtype=np.float32)
    for (a, b, image), opacity in ((first, 1 - weight), (second, weight)):
        result[b - y:b - y + image.shape[0], a - x:a - x + image.shape[1]] += opacity * image
    return x, y, result


def standup_frame(frame, index, atlas):
    import cv2
    import numpy as np

    # Camera registration against the matching source frame from the animator atlas. The MP4
    # zooms out for its first half second. Restore the atlas coordinate system before keying.
    registered = [[0, 1.222, -61.84, -14.32], [4, 1.178, -54.62, -10.85],
                  [8, 1.142, -47.52, -7.0], [12, 1.112, -40.1, 0.2]]
    scale, x, y = [np.interp(index, [r[0] for r in registered], [r[k] for r in registered])
                   for k in (1, 2, 3)]
    source = atlas.crop((index % 8 * 504, index // 8 * 768,
                         (index % 8 + 1) * 504, (index // 8 + 1) * 768))
    rgba = np.array(source)
    # At the matte edge or outside the encoded crop, use the uncomposited source RGB to avoid
    # importing the baked checkerboard or chopping off the end of the staff.
    inverse = np.array([[scale, 0, x], [0, scale, y]])
    color = cv2.warpAffine(frame[:, :, :3], inverse, (504, 768),
                           flags=cv2.WARP_INVERSE_MAP | cv2.INTER_LINEAR)
    inside = cv2.warpAffine(np.full(frame.shape[:2], 255, dtype=np.uint8), inverse, (504, 768),
                            flags=cv2.WARP_INVERSE_MAP | cv2.INTER_LINEAR)
    weight = np.clip((rgba[:, :, 3:4].astype(np.float32) - 170) / 70, 0, 1) * (inside[:, :, None] / 255)
    rgba[:, :, :3] = np.round(color * weight + rgba[:, :, :3] * (1 - weight)).astype(np.uint8)
    return warp(rgba, HEIGHT / 635, 247.5, 750)


def encode_frame(pixels):
    import numpy as np
    from PIL import Image

    rgba = pixels.copy()
    alpha = rgba[:, :, 3:4] / 255
    np.divide(rgba[:, :, :3], alpha, out=rgba[:, :, :3], where=alpha > 0.01)
    rgba = np.uint8(np.round(np.clip(rgba, 0, 255)))
    image = Image.fromarray(rgba)
    stream = io.BytesIO()
    image.save(stream, format='WEBP', quality=94, method=3)
    return stream.getvalue()


def encode_audio(name, clip, reverse=False):
    duration = clip['frames'] / clip['fps']
    # Select the primary mix. Pink idle also contains a duplicate and silent stems.
    # Pad to picture length before reversing so the return starts at the standing endpoint.
    filters = ['aresample=48000:first_pts=0', 'apad', f'atrim=duration={duration:.9f}']
    if reverse:
        filters.append('areverse')
    fade = 0.08 if clip.get('loop') else 0.012
    filters.extend([f'afade=d={fade}', f'afade=t=out:st={duration - fade:.9f}:d={fade}'])
    return subprocess.check_output(['ffmpeg', '-v', 'error', '-i', str(source_path(name, clip)),
                                    '-map', '0:a:0', '-vn', '-af', ','.join(filters),
                                    '-c:a', 'libvorbis', '-q:a', '5', '-f', 'ogg', '-'])


def render(name, clip, output, idle=None):
    import numpy as np
    from PIL import Image

    use_atlas = clip.get('alpha_atlas')
    # All 4K camera measurements were made at 1/8 resolution. Decode at half HD, preserving
    # the original alpha and selecting by source index (no fps filter can drop a hit frame).
    width, height, camera_factor = (480, 854, 1) if use_atlas else (1920, 1080, 4)
    atlas = Image.open(STANDUP).convert('RGBA') if use_atlas else None
    samples = sample_frames(clip)
    frame_times = {}
    for time, index in samples:
        frame_times.setdefault(index, []).append(time)
    duration = clip['frames'] / clip['fps']
    impacts = [round(index / clip['fps'] * 1_000_000) for index in clip.get('impact_frames', [])]
    audio = encode_audio(name, clip)
    reversed_audio = encode_audio(name, clip, reverse=True) if use_atlas else b''
    command = ['ffmpeg', '-v', 'error', '-threads', '3', '-i', str(source_path(name, clip)),
               '-an', '-frames:v', str(clip['frames']), '-filter_threads', '1',
               '-vf', f'scale={width}:{height}:flags=area', '-pix_fmt', 'rgba', '-threads', '1', '-f', 'rawvideo', '-']
    table = []
    temp = output.with_suffix('.tmp')
    try:
        with subprocess.Popen(command, stdout=subprocess.PIPE) as decoder, temp.open('wb') as stream:
            stream.write(struct.pack('<4sIIIII', MAGIC, len(samples), round(duration * 1_000_000),
                                     len(impacts), len(audio), len(reversed_audio)))
            for impact in impacts:
                stream.write(struct.pack('<I', impact))
            table_offset = stream.tell()
            stream.write(bytes(24 * len(samples)))
            first = None
            for index in range(clip['frames']):
                raw = decoder.stdout.read(width * height * 4)
                if len(raw) != width * height * 4:
                    raise ValueError(f'{name}: source ended at frame {index}')
                if index not in frame_times:
                    continue
                pixels = np.frombuffer(raw, dtype=np.uint8).reshape(height, width, 4)
                if use_atlas:
                    frame = standup_frame(pixels, index, atlas)
                else:
                    x, y, reference = smooth_camera(clip['camera'], index)
                    frame = warp(pixels, HEIGHT / (reference * camera_factor), x * camera_factor, y * camera_factor)
                if first is None:
                    first = frame
                for time in frame_times[index]:
                    display = frame
                    if clip.get('loop') and time > duration - 0.35:
                        display = mix(frame, first, (time - duration + 0.35) / 0.35)
                    if idle is not None:
                        # Match the pose at the two idle joins, with a shorter entry on clips that
                        # already start seated. Keep all designated impact frames outside the blend.
                        entry = 0.18 if name == 'dark_1hit' else 0.12
                        exit = 0.36 if name == 'dark_3hit' else 0.20
                        weight = max(1 - time / entry, 1 - (duration - time) / exit, 0)
                        if weight > 0:
                            display = mix(frame, idle, weight * weight * (3 - 2 * weight))
                    x, y, image = display
                    encoded = encode_frame(image)
                    table.append((round(time * 1_000_000), x, y, len(encoded), stream.tell()))
                    stream.write(encoded)
            if decoder.wait():
                raise RuntimeError(f'{name}: FFmpeg failed')
            if len(table) != len(samples) or any(left[0] >= right[0] for left, right in zip(table, table[1:])):
                raise ValueError(f'{name}: output timeline is incomplete or out of order')
            stream.write(audio)
            stream.write(reversed_audio)
            stream.seek(table_offset)
            for row in table:
                stream.write(struct.pack('<IiiIQ', *row))
        temp.replace(output)
    except BaseException:
        temp.unlink(missing_ok=True)
        raise
    finally:
        if atlas is not None:
            atlas.close()
    print(f'{name}: {len(table)} frames, {output.stat().st_size / 1048576:.1f} MiB', flush=True)
    return first


def prepare():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rebuild', action='store_true', help='Re-encode the local runtime masters from the source clips.')
    args = parser.parse_args()
    clips = json.loads(RECIPE.read_text(encoding='utf-8-sig'))
    signature = {
        'recipe': digest(RECIPE), 'converter': hashlib.sha256(Path(__file__).read_text(encoding='utf-8').encode()).hexdigest(),
        'standup_atlas': digest(STANDUP),
        'sources': {name: digest(source_path(name, clip)) for name, clip in clips.items()},
    }
    MASTERS.mkdir(parents=True, exist_ok=True)
    manifest = MASTERS / 'manifest.json'
    previous = json.loads(manifest.read_text(encoding='utf-8')) if manifest.is_file() else {}
    masters = {name: MASTERS / f'{name}.dbv' for name in clips}
    if args.rebuild or previous.get('inputs') != signature or any(
            not p.is_file() or digest(p) != previous.get('outputs', {}).get(name) for name, p in masters.items()):
        import cv2

        cv2.setNumThreads(1)
        idle = render('dark_idle', clips['dark_idle'], masters['dark_idle'])
        with ThreadPoolExecutor(max_workers=3) as pool:
            jobs = [pool.submit(render, name, clip, masters[name], idle if clip.get('idle_bridge') else None)
                    for name, clip in clips.items() if name != 'dark_idle']
            for job in jobs:
                job.result()
        previous = {'inputs': signature, 'outputs': {name: digest(p) for name, p in masters.items()}}
        manifest.write_text(json.dumps(previous, indent=2) + '\n', encoding='utf-8')
    target = MOD / 'videos'
    target.mkdir(exist_ok=True)
    for name, master in masters.items():
        destination = target / master.name
        if not destination.is_file() or digest(destination) != previous['outputs'][name]:
            shutil.copyfile(master, destination)
    print(f'Combat video: {len(clips)} prepared alpha clips verified')


if __name__ == '__main__':
    prepare()
