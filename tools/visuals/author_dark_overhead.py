"""Raise both black-attack hands half a head above the crown; retain the jump.

Background Blender --python-exit-code 1 --python this_file -- --root ... --output ROOT/.cache/...
"""
import argparse
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from author_dodge import two_bone, elbow_hinge, smooth, update
from bake_arm_hair import ArmHair, damped_release
from check_contacts import coordinates


def render(scene, out, preview):
    scene.camera.data.ortho_scale *= 1152 / scene.render.resolution_y
    scene.render.resolution_y = 1152
    if not preview:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(out/'DeniaDark_right60_attack.blend'), compress=True)
    destination=out/'renders'; destination.mkdir(exist_ok=True)
    for f in ((1,18,24,31,37,43,52,72) if preview else range(1,73)):
        scene.frame_set(f); scene.render.filepath=str(destination/f'frame_{f:04}.png'); bpy.ops.render.render(write_still=True)
    print('OVERHEAD_COMPLETE',flush=True)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--preview', action='store_true')
    a = p.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root, out = a.root.resolve(), a.output.resolve()
    if not out.is_relative_to(root / '.cache'):
        raise ValueError('Temporary output must stay in root .cache')
    out.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.temporary_directory = str(out / 'blender-temp')
    path = root / 'artResources/Denia/人物形象与动画/DeniaDark_right60_attack.blend'
    bpy.ops.wm.open_mainfile(filepath=str(path))
    scene = bpy.context.scene
    if scene.get('denia_overhead_attack'):
        (out/'overhead-checks.json').write_text(scene['denia_overhead_report'], encoding='utf8')
        render(scene, out, a.preview)
        return
    arm = next(o for o in scene.objects if o.type == 'ARMATURE'); bones = arm.pose.bones
    original = next(o for o in scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
    baked = bpy.data.objects['Denia_Contact_Baked']; solver = ArmHair(original, arm)
    raw, poses = [], []
    for f in range(1, 73):
        scene.frame_set(f); update()
        raw.append(coordinates(baked)); poses.append({b.name: b.matrix.copy() for b in bones})
    arm.animation_data_clear()
    for b in bones:
        b.rotation_mode = 'QUATERNION'
        for c in b.constraints: c.mute = True
    for side in ('L','R'):
        for name in ('腕','ひじ','手首'): bones[name+'.'+side].bone.inherit_scale = 'NONE'
    ordered = sorted(bones, key=lambda b: len(b.parent_recursive))
    entries = {}
    for v in original.data.vertices:
        for g in v.groups: entries.setdefault(g.group, []).append((v.index, g.weight))
    groups = []; total = np.zeros(len(raw[0]))
    for g in original.vertex_groups:
        if g.name in bones and entries.get(g.index):
            ids, weights = zip(*entries[g.index]); ids, weights = np.array(ids), np.array(weights)
            total[ids] += weights
            lineage = [g.name] + [b.name for b in bones[g.name].parent_recursive]
            if any(n in lineage for n in ('腕.L', '腕.R')): groups.append((g.name, ids, weights))
    frames, influences, needed, reports = [], [], [], []
    for f, (coords, base) in enumerate(zip(raw, poses), 1):
        scene.frame_set(f)
        for b in ordered:
            kwargs = dict(parent_matrix=base[b.parent.name], parent_matrix_local=b.parent.bone.matrix_local) if b.parent else {}
            b.matrix_basis = b.bone.convert_local_to_pose(base[b.name], b.bone.matrix_local, invert=True, **kwargs)
        update()
        assert all(np.max(abs(np.array(bones[n].matrix) - np.array(base[n]))) < 2e-5
                   for n in ('頭','腕.L','腕.R','手首.L','手首.R'))
        weight = float(smooth((f - 18) / 13) * smooth((59 - f) / 28))
        head = coords[solver.contacts.head]
        crown, chin = float(head[:, 2].max()), float(head[:, 2].min())
        goal_z = crown + .5 * (crown - chin)
        heights = []; palms = []
        for side, sign in (('R', -1), ('L', 1)):
            upper, elbow, hand = [bones[n + '.' + side] for n in ('腕', 'ひじ', '手首')]
            # The requested height refers to the palm, whose centre lies beyond the wrist.
            overhead = Vector((sign * .23, -.12, goal_z - .012))
            reach = ((elbow.head-upper.head).length + (hand.head-elbow.head).length) * .985
            horizontal = overhead - upper.head; horizontal.z = 0
            limit = max(0., reach**2 - (overhead.z-upper.head.z)**2)**.5
            if horizontal.length > limit:
                horizontal *= limit / horizontal.length
                overhead.x, overhead.y = upper.head.x + horizontal.x, upper.head.y + horizontal.y
            target = base[hand.name].translation.lerp(overhead, weight)
            pole = Vector((sign * .6, -.7, -.1))
            two_bone(bones, [upper.name, elbow.name, hand.name], target, pole)
            elbow_hinge(bones, side)
            # Turn the palm with the raised forearm; keep the accepted local wrist bend.
            q = elbow.matrix.to_quaternion() @ base[elbow.name].to_quaternion().inverted() @ base[hand.name].to_quaternion()
            hand.matrix = Matrix.LocRotScale(hand.head, q, base[hand.name].to_scale()); update()
            heights.append(float(hand.head.z))
            palms.append(float((hand.head.z + bones['中指１.'+side].head.z) * .5))
        result = coords.copy()
        for name, ids, weights in groups:
            transform = np.array(bones[name].matrix @ base[name].inverted())
            moved = coords[ids] @ transform[:3, :3].T + transform[:3, 3]
            result[ids] += (moved - coords[ids]) * (weights / total[ids])[:, None]
        if weight == 0: result = coords.copy()
        influence = solver.influence(result, np.array(bones['頭'].head))
        amount = np.zeros(4)
        if solver.tree(result, solver.hair).overlap(solver.tree(result, solver.arms)):
            amount = solver.minimum(result, influence)
        frames.append(result); influences.append(influence); needed.append(amount)
        reports.append(dict(frame=f, weight=weight, crown=crown, head_height=crown-chin, wrist_z=heights, palm_z=palms))
        for b in bones:
            for channel in ('location','rotation_quaternion','scale'): b.keyframe_insert(channel, frame=f)
        print('OVERHEAD_POSE', f, heights, goal_z, flush=True)
    solved = []
    for i, (coords, influence, amount) in enumerate(zip(frames, influences, damped_release(np.array(needed), 24))):
        if reports[i]['weight'] == 0: amount = np.zeros(4)
        if solver.tree(solver.deform(coords, influence, amount), solver.hair).overlap(solver.tree(coords, solver.arms)):
            amount = solver.minimum(coords, influence, amount)
        result = solver.deform(coords, influence, amount)
        assert np.array_equal(result[solver.contacts.head], raw[i][solver.contacts.head])
        assert np.array_equal(result[solver.contacts.feet], raw[i][solver.contacts.feet])
        contact = solver.contacts.check(result); contact['hair_arms'] = solver.contacts.count(result, solver.hair, solver.arms)
        reports[i]['contacts'] = contact
        assert not sum(contact.values()), (i+1, contact)
        solved.append(result)
    peak = reports[30]
    assert min(peak['palm_z']) >= peak['crown'] + .5 * peak['head_height'], peak
    assert np.array_equal(solved[0], raw[0]) and np.array_equal(solved[-1], raw[-1])
    baked.shape_key_clear(); baked.data.vertices.foreach_set('co', solved[0].ravel()); baked.shape_key_add(name='Basis')
    for f, coords in enumerate(solved[1:], 2):
        key = baked.shape_key_add(name=f'Overhead_{f:04}'); key.data.foreach_set('co', coords.ravel())
        for t, value in ((f-1,0.),(f,1.),(f+1,0.)):
            key.value=value; key.keyframe_insert('value',frame=t)
    scene['denia_overhead_attack'] = True
    scene['denia_overhead_report'] = json.dumps(reports, indent=2)
    scene.frame_set(1); bpy.context.preferences.filepaths.save_version = 0
    (out/'overhead-checks.json').write_text(json.dumps(reports, indent=2),encoding='utf8')
    if not a.preview: bpy.ops.wm.save_as_mainfile(filepath=str(out/path.name),compress=True)
    render(scene, out, a.preview)


if __name__ == '__main__': main()
