"""Run the real character-select background scene and class in a native GPU preview."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

HERE = Path(__file__).resolve().parent
MOD = HERE.parents[2]
ROOT = MOD.parents[1]


def run(command, cwd, log, timeout=180):
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True, encoding='utf-8', errors='replace',
        timeout=timeout, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    log.write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode or 'ERROR:' in result.stdout + result.stderr:
        raise RuntimeError(f'Native preview failed: {log}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / '.cache/denia-character-select-video')
    parser.add_argument('--game', action='store_true', help='Probe installed DLL/PCK through real game mod initialization (no run started)')
    parser.add_argument('--game-gpu', action='store_true', help='Use native offscreen D3D12 rendering for installed-game playback timing')
    args = parser.parse_args()
    runtime = args.output.resolve()
    if not runtime.is_relative_to((ROOT / '.cache').resolve()):
        parser.error('output must be within workspace .cache')
    runtime.mkdir(parents=True, exist_ok=True)
    if args.game:
        game = Path(os.environ.get('STS2_GAME_DIR', 'F:/SteamLibrary/steamapps/common/Slay the Spire 2'))
        display = ['--display-driver', 'windows', '--position', '-16000,-16000', '--resolution', '1920x1080',
            '--rendering-method', 'forward_plus', '--rendering-driver', 'd3d12'] if args.game_gpu else ['--headless']
        result = subprocess.run([str(game / 'SlayTheSpire2.exe'), *display, '--audio-driver', 'Dummy',
            '--max-fps', '60', '--script', str(HERE / 'game_probe.gd'),
            '--log-file', str(runtime / 'game.log')], cwd=game, capture_output=True, text=True,
            encoding='utf-8', errors='replace', timeout=65,
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0),
            env=dict(os.environ, SteamAppId='2868840', SteamGameId='2868840'))
        output = result.stdout + result.stderr
        (runtime / 'game-process.log').write_text(output, encoding='utf-8')
        if result.returncode or 'DENIA_BG_GAME PASS:' not in output or 'Cannot instantiate C# script' in output:
            raise RuntimeError(f'Installed game probe failed: {runtime / "game.log"}')
        print(next(line for line in output.splitlines() if line.startswith('DENIA_BG_GAME PASS:')))
        return
    for relative in ('src/Visuals/DeniaCharacterSelectBackground.cs', 'src/Visuals/DeniaHardwareVideo.cs',
                     'shaders/denia_video_nv12.gdshader', 'native/denia_video.bin', 'videos/denia_character_select_bg.mp4', 'scenes/screens/char_select/denia_bg.tscn',
                     'videos/denia_character_select_bg.ogv', 'images/char_select/denia_bg_poster.jpg'):
        target = runtime / relative;target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(MOD / relative, target)
    shutil.copyfile(HERE / 'check.gd', runtime / 'check.gd')
    (runtime / 'project.godot').write_text('''config_version=5
[application]
config/name="Denia Character Select Video Preview"
run/main_scene="res://main.tscn"
config/features=PackedStringArray("4.5", "C#")
[display]
window/size/viewport_width=960
window/size/viewport_height=540
[dotnet]
project/assembly_name="CharacterSelectPreview"
''', encoding='utf-8')
    (runtime / 'main.tscn').write_text('''[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://check.gd" id="1"]
[node name="Preview" type="Node"]
script=ExtResource("1")
''', encoding='utf-8')
    (runtime / 'CharacterSelectPreview.csproj').write_text('''<Project Sdk="Godot.NET.Sdk/4.5.1">
<PropertyGroup><TargetFramework>net9.0</TargetFramework><Nullable>enable</Nullable>
<EnableDynamicLoading>true</EnableDynamicLoading></PropertyGroup></Project>
''', encoding='utf-8')
    run(['dotnet', 'build', '-c', 'Debug'], runtime, runtime / 'build.log')
    godot = ROOT / 'Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe'
    run([str(godot), '--headless', '--path', str(runtime), '--editor', '--import'], ROOT, runtime / 'import.log')
    run([str(godot), '--path', str(runtime), '--audio-driver', 'Dummy', '--max-fps', '60',
        '--display-driver', 'windows', '--position', '-16000,-16000', '--rendering-method', 'forward_plus',
        '--rendering-driver', 'd3d12', '--log-file', str(runtime / 'godot.log')], ROOT, runtime / 'process.log')
    print(json.dumps(json.loads((runtime / 'captures/checks.json').read_text()), ensure_ascii=False))


if __name__ == '__main__':
    main()
