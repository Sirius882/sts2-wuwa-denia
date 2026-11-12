"""Add a whole-body jump to the accepted black attack without changing its contacts.

Background Blender: --python-exit-code 1 --python this_file -- --root ... --output ROOT/.cache/...
Use --preview for a contact-sheet sample before rendering all 72 frames.
"""
import argparse
import json
from pathlib import Path
import sys

import bpy
import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from check_contacts import coordinates, Contacts


def height(frame):
    # Accelerating takeoff and a ballistic middle; soften only the ground joins.
    if not 19 < frame < 43:
        return 0.
    t = (frame - 19) / 24
    edge = min(1., t / .13, (1 - t) / .13)
    edge = edge * edge * (3 - 2 * edge)
    return .22 * 4 * t * (1 - t) * edge


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--preview', action='store_true')
    a = p.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root, out = a.root.resolve(), a.output.resolve()
    if not out.is_relative_to(root / '.cache'):
        raise ValueError('Temporary output must be under root .cache')
    out.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.temporary_directory = str(out / 'blender-temp')
    source = root / 'artResources/Denia/人物形象与动画/DeniaDark_right60_attack.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    s = bpy.context.scene
    arm = next(o for o in s.objects if o.type == 'ARMATURE')
    original = next(o for o in s.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
    baked = bpy.data.objects['Denia_Contact_Baked']
    contacts = Contacts(original, arm)
    root_object = arm
    while root_object.parent:
        root_object = root_object.parent
    if s.get('denia_whole_body_jump'):
        # Rebuild from the saved source even after all temporary files are deleted.
        root_object.animation_data_clear()
        root_object.location.z = 0.
        s.camera.data.ortho_scale *= 768 / 960
    base = root_object.location.copy()
    reports = []
    for f in range(1, 73):
        s.frame_set(f)
        root_object.location = base
        bpy.context.view_layer.update()
        before = coordinates(baked)
        root_object.location.z = base.z + height(f)
        root_object.keyframe_insert('location', frame=f)
        bpy.context.view_layer.update()
        # A common root translation preserves every pre-baked hair/arm separation.
        assert np.array_equal(before, coordinates(baked))
        world = before @ np.array(baked.matrix_world.to_3x3()).T + np.array(baked.matrix_world.translation)
        assert world[:, 2].min() >= -.001
        result = contacts.check(before)
        result['hair_arms'] = contacts.count(before, contacts.hair, contacts.arms)
        assert not sum(result.values()), (f, result)
        reports.append(dict(frame=f, lift=height(f), lowest_world_z=float(world[:, 2].min()), contacts=result))
        print('JUMP_CHECK', f, height(f), flush=True)
    assert height(1) == height(72) == 0 and height(31) == .22
    assert all(r['lowest_world_z'] > .15 for r in reports if 27 <= r['frame'] <= 35)
    s['denia_whole_body_jump'] = True
    s['denia_jump_peak_m'] = .22
    # Extend the render vertically, preserving world units per pixel and camera centre.
    s.render.resolution_y = 960
    s.camera.data.ortho_scale *= 960 / 768
    s.render.resolution_percentage = 100
    s.frame_set(1)
    bpy.context.preferences.filepaths.save_version = 0
    (out / 'jump-checks.json').write_text(json.dumps(reports, indent=2), encoding='utf8')
    if not a.preview:
        bpy.ops.wm.save_as_mainfile(filepath=str(out / source.name), compress=True)
    destination = out / 'renders'
    destination.mkdir(exist_ok=True)
    for f in ((1, 18, 24, 31, 37, 43, 52, 72) if a.preview else range(1, 73)):
        s.frame_set(f)
        s.render.filepath = str(destination / f'frame_{f:04}.png')
        bpy.ops.render.render(write_still=True)
    print('JUMP_COMPLETE', flush=True)


if __name__ == '__main__':
    main()
