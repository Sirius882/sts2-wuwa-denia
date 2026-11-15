"""Resume the aligned execution clip's frame-by-frame local ComfyUI remaster."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import time
import urllib.request

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
CONFIG = HERE / 'execution_video_remaster.json'
WORKFLOW = HERE / 'execution_upscale_workflow.json'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def api(base, route, data=None):
    body = json.dumps(data).encode() if data is not None else None
    request = urllib.request.Request(base + route, data=body, headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def extract(config, scratch):
    source = ROOT / config['source']
    assert sha(source) == config['source_sha256'], 'Source changed; align before extracting'
    folder = scratch / 'input'
    folder.mkdir(parents=True, exist_ok=True)
    start = config['first_source_frame']
    count = config['frame_count']
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(source), '-vf',
        f'select=between(n\\,{start}\\,{start + count - 1})', '-fps_mode', 'passthrough',
        '-start_number', '0', str(folder / 'frame_%04d.png')], check=True)
    assert all((folder / f'frame_{i:04d}.png').is_file() for i in range(count))


def upscale(config, scratch, base, indices):
    workflow = json.loads(WORKFLOW.read_text(encoding='utf-8'))
    fingerprint = hashlib.sha256(json.dumps([workflow, config], sort_keys=True,
        separators=(',', ':'), ensure_ascii=True).encode()).hexdigest()
    legacy_fingerprint = hashlib.sha256(WORKFLOW.read_bytes() + CONFIG.read_bytes()).hexdigest()
    output = ROOT / config['frames_directory']
    output.mkdir(parents=True, exist_ok=True)
    progress_path = output.parent / 'progress.json'
    progress = json.loads(progress_path.read_text()) if progress_path.is_file() else {}
    if progress and progress['fingerprint'] not in (fingerprint, legacy_fingerprint):
        raise RuntimeError('Workflow/source changed; use a separate output directory')
    progress = progress or {'fingerprint': fingerprint, 'completed': {}, 'model': config['checkpoint']}
    progress['fingerprint'] = fingerprint
    progress.pop('paused', None)

    def save_progress():
        temporary = scratch / 'progress-write.json'
        temporary.write_text(json.dumps(progress, indent=2), encoding='utf-8')
        temporary.replace(progress_path)

    save_progress()
    for index in indices:
        destination = output / f'frame_{index:04d}.png'
        recorded = progress['completed'].get(str(index))
        if recorded and destination.is_file() and sha(destination) == recorded['sha256']:
            print(f'{index:04d}: verified existing frame', flush=True)
            continue
        prompt = copy.deepcopy(workflow)
        prompt['4']['inputs']['image'] = f'frame_{index:04d}.png'
        prompt['7']['inputs']['filename_prefix'] = f'execution/frame_{index:04d}'
        started = time.monotonic()
        reply = api(base, '/prompt', {'prompt': prompt, 'client_id': 'denia-execution-remaster'})
        prompt_id = reply['prompt_id']
        progress['active'] = {'frame': index, 'prompt_id': prompt_id}
        save_progress()
        print(f'{index:04d}: queued {prompt_id}', flush=True)
        while True:
            memory_report = scratch / 'gpu-memory.json'
            if memory_report.is_file() and json.loads(memory_report.read_text(encoding='utf-8-sig')).get('stopped_for_shared_memory'):
                raise RuntimeError('Memory monitor stopped ComfyUI; restart with the verified memory profile before resuming')
            history = api(base, f'/history/{prompt_id}')
            if prompt_id in history:
                item = history[prompt_id]
                if item['status']['status_str'] != 'success':
                    raise RuntimeError(json.dumps(item['status'], ensure_ascii=False))
                image = item['outputs']['7']['images'][0]
                file = scratch / 'output' / image['subfolder'] / image['filename']
                with Image.open(file) as frame:
                    assert frame.size == (3840, 1772), frame.size
                    frame.verify()
                shutil.copyfile(file, destination)
                elapsed = time.monotonic() - started
                progress['completed'][str(index)] = {'sha256': sha(destination), 'seconds': round(elapsed, 3),
                    'prompt_id': prompt_id}
                progress.pop('active', None)
                save_progress()
                print(f'{index:04d}: saved {len(progress["completed"])}/{config["frame_count"]}, {elapsed:.1f}s', flush=True)
                break
            if time.monotonic() - started > 15:
                queue = api(base, '/queue')
                pending = queue['queue_running'] + queue['queue_pending']
                if not any(item[1] == prompt_id for item in pending):
                    # Check history once more to avoid a completion/queue-update race.
                    if prompt_id not in api(base, f'/history/{prompt_id}'):
                        raise RuntimeError(f'Prompt was removed from the ComfyUI queue: {prompt_id}')
            time.sleep(2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', choices=('extract', 'upscale'))
    parser.add_argument('--server', default='http://127.0.0.1:8189')
    parser.add_argument('--scratch', type=Path, default=ROOT / '.cache/execution-remaster-20261115')
    parser.add_argument('--frames', help='Comma-separated zero-based clip frames; default: all frames')
    args = parser.parse_args()
    scratch = args.scratch.resolve()
    if not scratch.is_relative_to((ROOT / '.cache').resolve()):
        parser.error('--scratch must be inside workspace .cache')
    config = json.loads(CONFIG.read_text(encoding='utf-8'))
    if args.stage == 'extract':
        extract(config, scratch)
    else:
        indices = [int(i) for i in args.frames.split(',')] if args.frames else range(config['frame_count'])
        upscale(config, scratch, args.server, indices)


if __name__ == '__main__':
    main()
