"""Run the production execution overlay in Godot; generated files stay in root .cache."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

SOURCE = Path(__file__).resolve().parent
MOD = SOURCE.parents[2]
ROOT = MOD.parents[1]
RITSU = Path(os.environ.get('RITSULIB_DIR', 'F:/SteamLibrary/steamapps/workshop/content/2868840/3747602295'))
GAME = Path('F:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64')


def run(command, cwd, log, timeout=180):
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True, encoding='utf-8', errors='replace',
                            timeout=timeout, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    log.write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(f'Process failed ({result.returncode}): {log}')
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--render', action='store_true')
    parser.add_argument('--output', type=Path, default=ROOT / '.cache/denia-execution-preview')
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to((ROOT / '.cache').resolve()):
        parser.error('output must stay inside the workspace .cache')
    runtime = output / ('render' if args.render else 'checks')
    runtime.mkdir(parents=True, exist_ok=True)
    for folder in ('videos', 'shaders'):
        (runtime / folder).mkdir(exist_ok=True)
    shutil.copyfile(MOD / 'videos/denia_execution_middle.ogv', runtime / 'videos/denia_execution_middle.ogv')
    for path in (MOD / 'shaders').glob('denia_execution_*.gdshader'):
        shutil.copyfile(path, runtime / 'shaders' / path.name)
    (runtime / 'project.godot').write_text('''config_version=5
[application]
config/name="Denia Execution Preview"
run/main_scene="res://main.tscn"
config/features=PackedStringArray("4.5", "C#")
[display]
window/size/viewport_width=1280
window/size/viewport_height=720
[dotnet]
project/assembly_name="ExecutionPreview"
[rendering]
renderer/rendering_method="gl_compatibility"
''', encoding='utf-8')
    (runtime / 'main.tscn').write_text(f'''[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://Preview.cs" id="1"]
[node name="Preview" type="Control"]
layout_mode=3
anchors_preset=15
anchor_right=1.0
anchor_bottom=1.0
script=ExtResource("1")
''', encoding='utf-8')
    shutil.copyfile(SOURCE / 'Preview.cs', runtime / 'Preview.cs')
    links = ''.join(f'<Compile Include="{MOD / "src/Visuals" / name}" Link="{name}" />' for name in
                    ('DeniaExecutionTimeline.cs', 'DeniaExecutionOverlay.cs'))
    (runtime / 'ExecutionPreview.csproj').write_text(f'''<Project Sdk="Godot.NET.Sdk/4.5.1">
<PropertyGroup><TargetFramework>net9.0</TargetFramework><Nullable>enable</Nullable>
<EnableDynamicLoading>true</EnableDynamicLoading><RitsuLibReferenceTarget>0.113.0</RitsuLibReferenceTarget></PropertyGroup>
<Import Project="{RITSU / 'RitsuLib.References.props'}" />
<ItemGroup>{links}<Reference Include="sts2" HintPath="{GAME / 'sts2.dll'}" Private="false" /></ItemGroup>
</Project>''', encoding='utf-8')
    run(['dotnet', 'build', '-c', 'Debug'], runtime, runtime / 'build.log')
    binary = runtime / '.godot/mono/temp/bin/Debug'
    for folder in (RITSU / 'shared', RITSU / 'compat/0.113.0'):
        for path in folder.glob('*.dll'):
            shutil.copyfile(path, binary / path.name)
    for name in ('sts2.dll', 'Sentry.dll', 'Sentry.Godot.dll', '0Harmony.dll'):
        shutil.copyfile(GAME / name, binary / name)
    godot = ROOT / 'Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe'
    run([str(godot), '--headless', '--path', str(runtime), '--editor', '--import'], ROOT, runtime / 'import.log')
    command = [str(godot), '--path', str(runtime), '--audio-driver', 'Dummy', '--fixed-fps', '30',
               '--log-file', str(runtime / 'godot.log')]
    command += ['--display-driver', 'windows', '--position', '-16000,-16000',
                '--rendering-method', 'forward_plus', '--rendering-driver', 'd3d12'] if args.render else ['--headless']
    result = run(command, ROOT, runtime / 'process.log')
    report = runtime / 'captures/checks.json'
    if not report.exists() or 'ERROR:' in result.stdout + result.stderr:
        raise RuntimeError(f'Validation failed: {runtime / "process.log"}')
    print(json.dumps(json.loads(report.read_text()), ensure_ascii=False))
    if args.render:
        run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '30',
             '-i', str(runtime / 'captures/normal/frame_%04d.png'), '-c:v', 'libx264',
             '-crf', '18', '-pix_fmt', 'yuv420p', str(output / 'normal.mp4')], ROOT, runtime / 'encode.log')


if __name__ == '__main__':
    main()
