"""Build/check the production idle class and scene; generated output stays in root .cache."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys

sys.dont_write_bytecode = True

HERE = Path(__file__).resolve().parent
MOD = HERE.parents[2]
ROOT = MOD.parents[1]


def run(command, cwd, log, timeout=180):
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True, encoding='utf-8',
                            errors='replace', timeout=timeout,
                            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    log.write_text(result.stdout + result.stderr, encoding='utf-8')
    if result.returncode or 'ERROR:' in result.stdout + result.stderr:
        raise RuntimeError(f'Process failed: {log}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--render', action='store_true')
    parser.add_argument('--form', choices=('pink', 'black'), default='pink')
    parser.add_argument('--output', type=Path, default=ROOT / '.cache/denia-pink-idle')
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to((ROOT / '.cache').resolve()):
        parser.error('output must stay inside workspace .cache')
    runtime = output / args.form / ('render' if args.render else 'checks')
    runtime.mkdir(parents=True, exist_ok=True)
    scene = MOD / 'scenes/creature_visuals/denia_pink.tscn'
    paths = [scene, MOD / 'shaders/denia_pink_idle.gdshader', MOD / 'shaders/denia_black_idle.gdshader']
    # Include the actual action textures referenced by the production scene.
    import re
    paths += [MOD / p for p in re.findall(r'path="res://([^"]+)"', scene.read_text())]
    for form in ('pink', 'black'):
        paths += list((MOD / f'images/characters/denia/realtime_{form}').glob('*'))
    for source in set(paths):
        target = runtime / source.relative_to(MOD)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    shutil.copyfile(HERE / 'Preview.cs', runtime / 'Preview.cs')
    (runtime / 'project.godot').write_text('''config_version=5
[application]
config/name="Denia Pink Idle Preview"
run/main_scene="res://main.tscn"
config/features=PackedStringArray("4.5", "C#")
[display]
window/size/viewport_width=960
window/size/viewport_height=720
[dotnet]
project/assembly_name="PinkIdlePreview"
''', encoding='utf-8')
    (runtime / 'main.tscn').write_text('''[gd_scene load_steps=2 format=3]
[ext_resource type="Script" path="res://Preview.cs" id="1"]
[node name="Preview" type="Node"]
script=ExtResource("1")
''', encoding='utf-8')
    links = ''.join(f'<Compile Include="{MOD / "src/Visuals" / (name + ".cs")}" Link="{name}.cs" />'
                    for name in ('DeniaFlowIdleVisual', 'DeniaPinkIdleVisual', 'DeniaBlackIdleVisual'))
    (runtime / 'PinkIdlePreview.csproj').write_text(f'''<Project Sdk="Godot.NET.Sdk/4.5.1">
<PropertyGroup><TargetFramework>net9.0</TargetFramework><Nullable>enable</Nullable>
<EnableDynamicLoading>true</EnableDynamicLoading></PropertyGroup>
<ItemGroup>{links}</ItemGroup>
</Project>''', encoding='utf-8')
    run(['dotnet', 'build', '-c', 'Debug'], runtime, runtime / 'build.log')
    godot = ROOT / 'Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe'
    run([str(godot), '--headless', '--path', str(runtime), '--editor', '--import'], ROOT, runtime / 'import.log')
    command = [str(godot), '--path', str(runtime), '--audio-driver', 'Dummy', '--fixed-fps', '60',
               '--log-file', str(runtime / 'godot.log')]
    command += ['--display-driver', 'windows', '--position', '-16000,-16000',
                '--rendering-method', 'forward_plus', '--rendering-driver', 'd3d12'] if args.render else ['--headless']
    command += ['--', args.form]
    run(command, ROOT, runtime / 'process.log')
    report = json.loads((runtime / 'captures/checks.json').read_text())
    if args.render:
        from check_captures import check, make_preview
        report['original_glsl_mean_error'] = check(runtime / 'captures', MOD / f'images/characters/denia/realtime_{args.form}', args.form)
        (runtime / 'captures/checks.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        pictures = make_preview(runtime / 'captures', args.form)
        run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '60',
             '-i', str(pictures / 'loop_%04d.png'), '-c:v', 'libx264', '-crf', '18',
             '-pix_fmt', 'yuv420p', str(output / f'{args.form}_idle.mp4')], ROOT, runtime / 'encode.log')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    main()
