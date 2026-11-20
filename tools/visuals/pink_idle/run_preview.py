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
    parser.add_argument('--video', action='store_true', help='Use prepared video assets for the catalog checks')
    parser.add_argument('--form', choices=('pink', 'black', 'black_rise', 'pink_1hit', 'dark_1hit', 'pink_2hit', 'dark_2hit', 'dark_4hit', 'pink_standup', 'bubbles', 'map_brush', 'catalog'), default='pink')
    parser.add_argument('--output', type=Path, default=ROOT / '.cache/denia-pink-idle')
    args = parser.parse_args()
    if args.video and args.form != 'catalog':
        parser.error('--video requires --form catalog')
    output = args.output.resolve()
    if not output.is_relative_to((ROOT / '.cache').resolve()):
        parser.error('output must stay inside workspace .cache')
    runtime = output / (args.form + ('-video' if args.video else '')) / ('render' if args.render else 'checks')
    runtime.mkdir(parents=True, exist_ok=True)
    for name in ('captures', 'preview_frames'):
        folder = runtime / name
        folder.mkdir(exist_ok=True)
        (folder / '.gdignore').touch()
    scene = MOD / 'scenes/creature_visuals/denia_pink.tscn'
    paths = [scene, MOD / 'shaders/denia_pink_idle.gdshader', MOD / 'shaders/denia_black_idle.gdshader', MOD / 'shaders/denia_black_rise.gdshader', MOD / 'shaders/denia_pink_1hit.gdshader', MOD / 'shaders/denia_dark_1hit.gdshader', MOD / 'shaders/denia_pink_2hit.gdshader', MOD / 'shaders/denia_dark_2hit.gdshader', MOD / 'shaders/denia_dark_4hit.gdshader']
    paths += [MOD / 'shaders/denia_pink_standup.gdshader', MOD / 'shaders/denia_pink_handoff.gdshader']
    paths += [MOD / 'images/ui/combat/denia_energy_icon_big.png']
    # Include the actual action textures referenced by the production scene.
    import re
    paths += [MOD / p for p in re.findall(r'path="res://([^"]+)"', scene.read_text())]
    for form in ('pink', 'black', 'black_rise', 'pink_1hit', 'dark_1hit', 'pink_2hit', 'dark_2hit', 'dark_4hit', 'pink_standup'):
        paths += list((MOD / f'images/characters/denia/realtime_{form}').glob('*'))
    if args.video:
        videos = sorted((MOD / 'videos').glob('*.dbv'))
        if len(videos) != 10:
            raise RuntimeError('Run tools/visuals/prepare_battle_videos.py before testing the video catalog')
        paths.extend(videos)
    if args.form in ('bubbles', 'map_brush'):
        paths = []  # The particle regression needs no character textures or scenes.
    for source in set(paths):
        target = runtime / source.relative_to(MOD)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    shutil.copyfile(HERE / 'Preview.cs', runtime / 'Preview.cs')
    shutil.copyfile(HERE / 'PinkStandupChecks.cs', runtime / 'PinkStandupChecks.cs')
    shutil.copyfile(HERE / 'MapBrushChecks.cs', runtime / 'MapBrushChecks.cs')
    shutil.copyfile(HERE / 'AnimationPreviewChecks.cs', runtime / 'AnimationPreviewChecks.cs')
    shutil.copyfile(HERE / 'ControllerFixture.cs', runtime / 'ControllerFixture.cs')
    # Link the exact production controller body; no copied controller implementation in fixtures.
    controller = (MOD / 'src/Cards/DeniaFormVisuals.cs').read_text(encoding='utf-8-sig')
    start = controller.index('public partial class DeniaFormOverlaySync')
    end = controller.index('\n/// <summary>\n/// Death/revive', start)
    (runtime / 'DeniaFormOverlaySync.cs').write_text(
        'using System;\nusing System.Threading.Tasks;\nusing Godot;\nnamespace Denia;\n' + controller[start:end], encoding='utf-8')

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
                    for name in ('DeniaAnimationPreviewKind', 'DeniaAnimationPreview', 'DeniaBattleVideo', 'DeniaPinkCollapseVisual', 'DeniaFlowIdleVisual', 'DeniaPinkIdleVisual', 'DeniaBlackIdleVisual',
                                 'DeniaBubbleBurst', 'DeniaMapBrushVisual', 'DeniaBlankClick', 'DeniaBlackRiseVisual', 'DeniaPinkStandupVisual', 'DeniaSingleHitVisual', 'DeniaPinkAttackVisual', 'DeniaDarkAttackVisual', 'DeniaPinkMultiAttackVisual', 'DeniaDarkMultiAttackVisual', 'DeniaDarkFourAttackVisual'))
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
    if args.video:
        command.append('video')
    run(command, ROOT, runtime / 'process.log', timeout=420)
    report = json.loads((runtime / 'captures/checks.json').read_text())
    if args.form == 'catalog':
        print(json.dumps(report, ensure_ascii=False))
        return
    if args.form == 'map_brush':
        if args.render:
            from check_brush import make_preview
            make_preview(runtime / 'captures', output)
        print(json.dumps(report, ensure_ascii=False))
        return
    if args.form == 'bubbles':
        print(json.dumps(report, ensure_ascii=False))
        return
    if args.form == 'pink_standup':
        if args.render:
            from check_standup import check_standup, make_preview
            report.update(check_standup(runtime / 'captures', MOD))
            (runtime / 'captures/checks.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            make_preview(runtime / 'captures', output)
        print(json.dumps(report, ensure_ascii=False))
        return
    if args.form in ('pink_2hit', 'dark_2hit', 'dark_4hit'):
        if args.render:
            from check_pink_multi import check_pink_multi, make_preview
            report['original_glsl_mean_error'] = check_pink_multi(runtime / 'captures', MOD, form=args.form)
            (runtime / 'captures/checks.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            make_preview(runtime / 'captures', form=args.form)
            run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '60',
                 '-i', str(runtime / ('captures/dark_four_%04d.png' if args.form == 'dark_4hit' else 'captures/dark_multi_%04d.png' if args.form == 'dark_2hit' else 'captures/multi_%04d.png')), '-vf', 'format=yuv420p',
                 '-c:v', 'libx264', '-crf', '18', str(output / f'{args.form}.mp4')], ROOT, runtime / 'encode.log')
        print(json.dumps(report, ensure_ascii=False))
        return
    if args.form == 'dark_1hit':
        if args.render:
            from check_dark_attack import check_dark_attack, make_preview
            report['original_glsl_mean_error'] = check_dark_attack(runtime / 'captures', MOD)
            (runtime / 'captures/checks.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            make_preview(runtime / 'captures')
            run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '60',
                 '-i', str(runtime / 'captures/dark_%04d.png'), '-vf', 'format=yuv420p',
                 '-c:v', 'libx264', '-crf', '18', str(output / 'dark_1hit.mp4')], ROOT, runtime / 'encode.log')
        print(json.dumps(report, ensure_ascii=False))
        return
    if args.form == 'pink_1hit':
        if args.render:
            from check_captures import check_attack, make_attack_preview
            report['original_glsl_mean_error'] = check_attack(runtime / 'captures', MOD / 'images/characters/denia/realtime_pink_1hit')
            (runtime / 'captures/checks.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            pictures = make_attack_preview(runtime / 'captures')
            run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '60',
                 '-i', str(pictures / 'attack_%04d.png'), '-vf', 'format=yuv420p',
                 '-c:v', 'libx264', '-crf', '18', str(output / 'pink_1hit.mp4')], ROOT, runtime / 'encode.log')
        print(json.dumps(report, ensure_ascii=False))
        return
    if args.form == 'black_rise':
        if args.render:
            run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '60',
                 '-i', str(runtime / 'captures/rise_%04d.png'), '-vf', 'format=yuv420p',
                 '-c:v', 'libx264', '-crf', '18', str(output / 'black_rise.mp4')], ROOT, runtime / 'encode.log')
        print(json.dumps(report, ensure_ascii=False))
        return
    if args.render:
        from check_captures import check, make_preview
        report['original_glsl_mean_error'] = check(runtime / 'captures', MOD / f'images/characters/denia/realtime_{args.form}', args.form)
        (runtime / 'captures/checks.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        pictures = make_preview(runtime / 'captures', args.form)
        run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '60',
             '-i', str(pictures / 'loop_%04d.png'), '-c:v', 'libx264', '-crf', '18',
             '-pix_fmt', 'yuv420p', str(output / f'{args.form}_idle.mp4')], ROOT, runtime / 'encode.log')
        run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '60',
             '-i', str(pictures / 'bubble_%03d.png'), '-c:v', 'libx264', '-crf', '18',
             '-pix_fmt', 'yuv420p', str(output / f'{args.form}_click.mp4')], ROOT, runtime / 'encode-click.log')
    print(json.dumps(report, ensure_ascii=False))


if __name__ == '__main__':
    main()
