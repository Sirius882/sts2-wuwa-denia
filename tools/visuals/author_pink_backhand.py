"""Author the pink chest-load, two-frame hold and right-front backhand attack.

Run in background Blender. Candidates, renders and reports stay in root .cache.
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from author_dodge import curve, elbow_hinge, smooth, two_bone, update
from bake_arm_hair import ArmHair, damped_release
from check_contacts import coordinates


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--preview', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root, output = args.root.resolve(), args.output.resolve()
    if not output.is_relative_to(root / '.cache'):
        raise ValueError('Process files must stay under workspace root .cache')
    output.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.temporary_directory = str(output / 'blender-temp')
    sys.path.insert(0, str(Path(bpy.utils.user_resource('SCRIPTS')) / 'addons/mmd_tools'))
    bpy.ops.preferences.addon_enable(module='mmd_tools')
    bpy.ops.wm.open_mainfile(filepath=str(root / 'artResources/Denia/人物形象与动画/DeniaPink_right60_idle.blend'))
    scene = bpy.context.scene; scene.frame_set(1); update()
    arm = next(o for o in scene.objects if o.type == 'ARMATURE'); bones = arm.pose.bones
    original = next(o for o in scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
    baked = bpy.data.objects['Denia_Contact_Baked']; solver = ArmHair(original, arm)
    start = coordinates(baked); base = {b.name: b.matrix.copy() for b in bones}
    arm.animation_data_clear()
    for b in bones:
        for constraint in b.constraints: constraint.mute = True
    for b in sorted(bones, key=lambda b: len(b.parent_recursive)): b.matrix = base[b.name]
    update(); pose = {b.name: b.matrix_basis.copy() for b in bones}
    entries = {}
    for vertex in original.data.vertices:
        for assignment in vertex.groups:
            entries.setdefault(assignment.group, []).append((vertex.index, assignment.weight))
    groups = []
    for group in original.vertex_groups:
        if group.name in bones and entries.get(group.index):
            ids, weights = zip(*entries[group.index]); groups.append((group.name, np.array(ids), np.array(weights)))
    total = np.zeros(len(start))
    for _, ids, weights in groups: total[ids] += weights
    inverse = {n: m.inverted() for n, m in base.items()}
    skirt_weights = np.array([sum(g.weight for g in v.groups
        if original.vertex_groups[g.group].name.startswith(('スカート', '披风_'))) for v in original.data.vertices])
    def skin():
        result = start.copy()
        for name, ids, weights in groups:
            transform = np.array(bones[name].matrix @ inverse[name])
            moved = start[ids] @ transform[:3, :3].T + transform[:3, 3]
            result[ids] += (moved - start[ids]) * (weights / total[ids])[:, None]
        return result

    hand = bones['手首.R']; rest = base[hand.name].translation
    chest = Vector((.025, -.10, 1.16)); strike = Vector((-.30, -.225, 1.19))
    strike_direction = (strike - chest).normalized()
    # Front is -Y and the character's own right is -X. The back of the hand leads.
    forward = (hand.bone.matrix_local.inverted() @ bones['中指１.R'].bone.head_local).normalized()
    across = (hand.bone.matrix_local.to_3x3().inverted() @
              (bones['人指１.R'].bone.head_local - bones['小指１.R'].bone.head_local)).normalized()
    # Right-hand palm points along middle-finger direction cross pinky-to-index.
    normal = forward.cross(across).normalized()
    scene.frame_start = 1; scene.frame_end = 72; scene.render.fps = 24
    frames = []; influences = []; required = []; reports = []
    samples = (1, 10, 18, 21, 22, 23, 25, 27, 31, 42, 56, 66, 72) if args.preview else range(1, 73)
    for frame in samples:
        scene.frame_set(frame)
        for name, matrix in pose.items(): bones[name].matrix_basis = matrix
        update()
        if frame <= 21:
            target = curve([(1, rest), (10, (-.27, .13, 1.01)),
                            (16, (-.18, -.12, 1.12)), (21, chest)], frame)
            twist = -5 * float(smooth((frame - 1) / 20))
        elif frame <= 22:
            target = chest.copy(); twist = -5.
        elif frame <= 27:
            # Five intervals (0.208 s): accelerate out of the explicit two-frame load.
            hit = float(smooth((frame - 22) / 5))
            target = chest.lerp(strike, hit); twist = -5 + 12 * hit
        else:
            target = curve([(27, strike), (35, (-.29, -.20, 1.14)),
                            (49, (-.29, .03, .99)), (60, (-.22, .22, .96)), (72, rest)], frame)
            twist = 7 * (1 - float(smooth((frame - 27) / 45)))
        opening = float(smooth((frame - 1) / 15) * smooth((72 - frame) / 16))
        for name, factor in (('上半身', .55), ('上半身2', .45)):
            b = bones[name]
            b.matrix = Matrix.LocRotScale(b.head, Quaternion((0, 0, 1), math.radians(twist * factor))
                                         @ b.matrix.to_quaternion(), b.matrix.to_scale()); update()
        # The non-striking hand stays supported behind the back while the torso turns.
        for side in ('R', 'L'):
            upper, elbow, wrist = [bones[n + '.' + side] for n in ('腕', 'ひじ', '手首')]
            goal = target if side == 'R' else base[wrist.name].translation
            direction = (goal - upper.head).normalized()
            rest_pole = base[elbow.name].translation - base[upper.name].translation
            rest_pole = (rest_pole - direction * rest_pole.dot(direction)).normalized()
            pole = Vector((-1, -.5, -.65)); pole = (pole - direction * pole.dot(direction)).normalized()
            two_bone(bones, [upper.name, elbow.name, wrist.name], goal,
                     rest_pole.lerp(pole, opening) if side == 'R' else rest_pole)
            error = elbow_hinge(bones, side)
            if side == 'R':
                # Keep the hand on the forearm's line instead of cocking it upright.
                desired = (wrist.head - elbow.head).normalized()
                q = base[wrist.name].to_quaternion()
                q = (q @ forward).rotation_difference(desired) @ q
                palm_target = -strike_direction
                palm_target = (palm_target - desired * palm_target.dot(desired)).normalized()
                current = q @ normal
                q = Quaternion(desired, math.atan2(desired.dot(current.cross(palm_target)),
                                                  current.dot(palm_target))) @ q
            wrist.matrix = Matrix.LocRotScale(wrist.head,
                base[wrist.name].to_quaternion().slerp(q, opening) if side == 'R' else base[wrist.name].to_quaternion(),
                base[wrist.name].to_scale()); update()
            assert error < 2e-5
        for finger in ('人指', '中指', '薬指', '小指'):
            for joint in ('１', '２', '３'):
                b = bones[finger + joint + '.R']
                b.rotation_quaternion = b.rotation_quaternion.slerp(Quaternion(), opening * .82)
        # The turn gently draws the separated long strands; contact adds only needed clearance.
        for b in bones:
            if b.name.startswith(('后发A', '后发B')):
                b.rotation_quaternion = b.rotation_quaternion @ Quaternion((0, 0, 1), math.radians(-twist * .12))
        update(); coords = skin()
        radial = coords[:, :2] - np.array(base['下半身'].translation[:2])
        radial /= np.maximum(np.linalg.norm(radial, axis=1), .02)[:, None]
        coords[:, :2] += radial * (skirt_weights * .008 * opening)[:, None]
        if frame in (1, 72): coords = start.copy()
        influence = solver.influence(coords, np.array(bones['頭'].head))
        release = float(smooth((.20 - target.y) / .22)) * opening
        # The accepted idle already supports the back hair; free only the striking side.
        coords = solver.deform(coords, influence, -np.array((0., .10, 0., .07)) * release)
        amount = np.zeros(4)
        if frame not in (1, 72) and solver.tree(coords, solver.hair).overlap(solver.tree(coords, solver.arms)):
            amount = solver.minimum(coords, influence)
        frames.append(coords); influences.append(influence); required.append(amount)
        reports.append(dict(frame=frame, right_hand=list(hand.head),
                            backhand_alignment=float(-(hand.matrix.to_quaternion() @ normal).dot(strike_direction)),
                            straight_wrist_alignment=float((hand.matrix.to_quaternion() @ forward).dot(
                                (hand.head - bones['ひじ.R'].head).normalized()))))
        for b in bones:
            for channel in ('location', 'rotation_quaternion', 'scale'): b.keyframe_insert(channel, frame=frame)
        print('BACKHAND_POSE', frame, amount.tolist(), flush=True)
    amounts = np.array(required) if args.preview else damped_release(np.array(required), 24)
    amounts[0] = 0.; amounts[-1] = 0.; solved = []
    for index, (coords, influence, amount) in enumerate(zip(frames, influences, amounts)):
        if solver.tree(solver.deform(coords, influence, amount), solver.hair).overlap(solver.tree(coords, solver.arms)):
            amount = solver.minimum(coords, influence, amount)
        result = solver.deform(coords, influence, amount)
        contacts = solver.contacts.check(result)
        contacts['hair_arms'] = solver.contacts.count(result, solver.hair, solver.arms)
        reports[index].update(contacts=contacts, hair_support=amount.tolist()); solved.append(result)
    (output / 'pink-backhand-poses.json').write_text(json.dumps(reports, indent=2), encoding='utf8')
    assert all(not sum(r['contacts'].values()) for r in reports), [r for r in reports if sum(r['contacts'].values())]
    assert np.array_equal(solved[0], start) and np.array_equal(solved[-1], start)
    hold = [r for r in reports if r['frame'] in (21, 22)]
    assert np.max(abs(np.array(hold[0]['right_hand']) - hold[1]['right_hand'])) < 1e-6
    assert all(r['backhand_alignment'] > .60 and r['straight_wrist_alignment'] > .99999
               for r in reports if 21 <= r['frame'] <= 27)
    assert strike.x < chest.x and strike.y < chest.y
    baked.shape_key_clear(); baked.data.vertices.foreach_set('co', solved[0].ravel()); baked.shape_key_add(name='Basis')
    for frame, coords in zip(list(samples)[1:], solved[1:]):
        key = baked.shape_key_add(name=f'Backhand_{frame:04}'); key.data.foreach_set('co', coords.ravel())
        for time, value in ((frame - 1, 0.), (frame, 1.), (frame + 1, 0.)):
            key.value = value; key.keyframe_insert('value', frame=time)
    scene['denia_elbow_hinge_corrected'] = True; scene['denia_arm_hair_contact_baked'] = True
    scene['denia_pink_backhand_hold_frames'] = '21,22'
    scene.frame_set(1); bpy.context.preferences.filepaths.save_version = 0
    if not args.preview:
        bpy.ops.wm.save_as_mainfile(filepath=str(output / 'DeniaPink_right60_attack.blend'), compress=True)
        destination = output / 'renders/pink-attack'; destination.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(destination / 'frame_'); bpy.ops.render.render(animation=True)
    else:
        for frame in samples:
            scene.frame_set(frame); scene.render.filepath = str(output / f'pink-backhand-{frame:04}.png'); bpy.ops.render.render(write_still=True)
    print('BACKHAND_COMPLETE', 'preview' if args.preview else 'render', flush=True)


if __name__ == '__main__':
    main()
