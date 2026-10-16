"""Author the shared three-second guard/dodge from the accepted idle pose.

Run in background Blender with --python-exit-code 1. Candidate sources, renders
and validation reports are written only to the workspace root .cache directory.
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
from bake_arm_hair import ArmHair, damped_release
from check_contacts import coordinates


def smooth(t):
    t = np.clip(t, 0., 1.)
    return t * t * (3 - 2 * t)


def curve(keys, time):
    """One clamped cubic spline; passing a path point does not add a pause."""
    ts = np.array([t for t, _ in keys]); values = np.array([v for _, v in keys], dtype=float)
    h = np.diff(ts); matrix = np.zeros((len(ts), len(ts))); rhs = np.zeros_like(values)
    matrix[0, 0:2] = (2 * h[0], h[0]); rhs[0] = 6 * (values[1] - values[0]) / h[0]
    matrix[-1, -2:] = (h[-1], 2 * h[-1]); rhs[-1] = -6 * (values[-1] - values[-2]) / h[-1]
    for i in range(1, len(ts) - 1):
        matrix[i, i - 1:i + 2] = (h[i - 1], 2 * (h[i - 1] + h[i]), h[i])
        rhs[i] = 6 * ((values[i + 1] - values[i]) / h[i] - (values[i] - values[i - 1]) / h[i - 1])
    second = np.linalg.solve(matrix, rhs)
    if time <= ts[0]: return Vector(values[0])
    if time >= ts[-1]: return Vector(values[-1])
    i = int(np.searchsorted(ts, time) - 1); span = h[i]; u = (time - ts[i]) / span; v = 1 - u
    return Vector(v * values[i] + u * values[i + 1]
                  + ((v ** 3 - v) * second[i] + (u ** 3 - u) * second[i + 1]) * span ** 2 / 6)


def update():
    bpy.context.view_layer.update()


def aim(bone, direction):
    q = (bone.tail - bone.head).normalized().rotation_difference(direction.normalized())
    bone.matrix = Matrix.LocRotScale(bone.head, q @ bone.matrix.to_quaternion(), bone.matrix.to_scale())
    update()


def two_bone(bones, names, target, pole):
    upper, elbow, wrist = [bones[name] for name in names]
    start = upper.head.copy(); target = Vector(target)
    first = (elbow.head - start).length; second = (wrist.head - elbow.head).length
    direction = (target - start).normalized(); distance = (target - start).length
    assert abs(first - second) < distance < first + second, (names, distance, first + second)
    along = (first * first - second * second + distance * distance) / (2 * distance)
    pole = Vector(pole); pole = (pole - direction * pole.dot(direction)).normalized()
    position = start + direction * along + pole * math.sqrt(max(0., first * first - along * along))
    aim(upper, position - start); aim(elbow, target - elbow.head)
    assert (wrist.head - target).length < 2e-5, (names, list(wrist.head), list(target))
    return position


def elbow_hinge(bones, side):
    upper, elbow, wrist = [bones[n + '.' + side] for n in ('腕', 'ひじ', '手首')]
    start, joint, end = upper.head.copy(), elbow.head.copy(), wrist.head.copy()
    hand = wrist.matrix.copy(); direction = (joint - start).normalized(); forearm = (end - joint).normalized()
    axis = direction.cross(forearm).normalized() * (1 if side == 'R' else -1)
    q = upper.matrix.to_quaternion()
    local_axis = upper.bone.matrix_local.to_3x3().inverted() @ elbow.bone.matrix_local.to_3x3().col[2]
    current = q @ local_axis; current = (current - direction * current.dot(direction)).normalized()
    angle = math.atan2(direction.dot(current.cross(axis)), current.dot(axis))
    upper.matrix = Matrix.LocRotScale(start, Quaternion(direction, angle) @ q, upper.matrix.to_scale()); update()
    q = elbow.matrix.to_quaternion(); q = (q @ Vector((0, 1, 0))).rotation_difference(forearm) @ q
    current = q @ Vector((0, 0, 1)); current = (current - forearm * current.dot(forearm)).normalized()
    angle = math.atan2(forearm.dot(current.cross(axis)), current.dot(axis))
    elbow.matrix = Matrix.LocRotScale(joint, Quaternion(forearm, angle) @ q, elbow.matrix.to_scale()); update()
    wrist.matrix = hand; update()
    error = abs(1 - elbow.matrix.to_3x3().normalized().col[2].dot(axis))
    assert error < 2e-5, ('Elbow axis', side, error)
    return error


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--form', choices=('pink', 'dark'), required=True)
    parser.add_argument('--preview', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root, output = args.root.resolve(), args.output.resolve()
    if not output.is_relative_to(root / '.cache'):
        raise ValueError('Process files must stay under workspace root .cache')
    output.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.temporary_directory = str(output / 'blender-temp')
    sys.path.insert(0, str(Path(bpy.utils.user_resource('SCRIPTS')) / 'addons/mmd_tools'))
    bpy.ops.preferences.addon_enable(module='mmd_tools')
    source = root / f'artResources/Denia/人物形象与动画/Denia{args.form.title()}_right60_idle.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene = bpy.context.scene; scene.frame_set(1); update()
    arm = next(o for o in scene.objects if o.type == 'ARMATURE'); bones = arm.pose.bones
    original = next(o for o in scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
    baked = bpy.data.objects['Denia_Contact_Baked']; solver = ArmHair(original, arm)
    start = coordinates(baked); base = {b.name: b.matrix.copy() for b in bones}
    arm.animation_data_clear()
    for b in bones:
        for constraint in b.constraints: constraint.mute = True
    ordered = sorted(bones, key=lambda b: len(b.parent_recursive))
    for b in ordered: b.matrix = base[b.name]
    update()
    pose = {b.name: b.matrix_basis.copy() for b in bones}
    # Apply bone deltas to the accepted baked surface, retaining its rest-pose fixes.
    groups = []; entries_by_group = {}
    for vertex in original.data.vertices:
        for assignment in vertex.groups:
            entries_by_group.setdefault(assignment.group, []).append((vertex.index, assignment.weight))
    for group in original.vertex_groups:
        if group.name not in bones: continue
        entries = entries_by_group.get(group.index, [])
        if entries:
            ids, weights = zip(*entries); groups.append((group.name, np.array(ids), np.array(weights)))
    total = np.zeros(len(start))
    for _, ids, weights in groups: total[ids] += weights
    inverse = {n: m.inverted() for n, m in base.items()}
    def skin():
        result = start.copy()
        for name, ids, weights in groups:
            transform = np.array(bones[name].matrix @ inverse[name])
            moved = start[ids] @ transform[:3, :3].T + transform[:3, 3]
            result[ids] += (moved - start[ids]) * (weights / total[ids])[:, None]
        return result
    foot = {side: base['足首.' + side].copy() for side in ('L', 'R')}
    skirt_weights = np.array([sum(g.weight for g in v.groups
        if original.vertex_groups[g.group].name.startswith(('スカート', '披风_'))) for v in original.data.vertices])
    root_bone = bones['全ての親']; frames = []; influences = []; required = []; reports = []
    scene.frame_start = 1; scene.frame_end = 72; scene.render.fps = 24
    sample_frames = (1, 9, 17, 25, 33, 41, 51, 62, 72) if args.preview else range(1, 73)
    for frame in sample_frames:
        scene.frame_set(frame)
        for name, matrix in pose.items(): bones[name].matrix_basis = matrix
        update(); time = (frame - 1) / 71 * 3
        # Continuous retreat and return, with both feet anchored and knees absorbing weight.
        lean = math.sin(math.pi * time / 3) ** 2
        shift = Vector((0., .045 * lean, -.035 * lean))
        root_bone.matrix = Matrix.Translation(shift) @ base[root_bone.name]; update()
        for name, degrees in (('下半身', -3.), ('上半身', -9.), ('上半身2', -8.)):
            b = bones[name]; b.matrix = Matrix.LocRotScale(b.head,
                Quaternion((1, 0, 0), math.radians(degrees * lean)) @ b.matrix.to_quaternion(), b.matrix.to_scale()); update()
        for side, sign in (('R', -1), ('L', 1)):
            two_bone(bones, ['足.' + side, 'ひざ.' + side, '足首.' + side], foot[side].translation,
                     (sign * .04, -1, -.04))
            bones['足首.' + side].matrix = foot[side]; update()
            for dst, src in ([('足D', '足'), ('ひざD', 'ひざ'), ('足首D', '足首')]
                             + [(f'足調整{i}', '足') for i in (1, 2, 3)]):
                bones[dst + '.' + side].matrix = bones[src + '.' + side].matrix @ inverse[src + '.' + side] @ base[dst + '.' + side]; update()
        opening = float(smooth(time / .8) * smooth((3 - time) / .85))
        palm_report = {}; hinge_error = 0.
        for side, sign in (('R', -1), ('L', 1)):
            upper, elbow, hand = [bones[n + '.' + side] for n in ('腕', 'ひじ', '手首')]
            rest = base[hand.name].translation
            guard = Vector((-.025 if side == 'R' else .035, -.16 if side == 'R' else -.04,
                            1.19 if side == 'R' else 1.22)) + shift
            target = curve([(0, rest), (.48, (sign * .26, .12, 1.01)),
                            (.75, (sign * .20, -.13, 1.18)),
                            (.95, guard), (1.5, guard + Vector((0, 0, .025))),
                            (2.05, guard + Vector((sign * .025, -.01, -.02))),
                            (2.25, (sign * .20, -.13, 1.18)),
                            (2.55, (sign * .26, .14, 1.00)), (3, rest)], time)
            direction = (target - upper.head).normalized()
            rest_pole = base[elbow.name].translation - base[upper.name].translation
            rest_pole = (rest_pole - direction * rest_pole.dot(direction)).normalized()
            pole = Vector((sign, -.5, -.65)); pole = (pole - direction * pole.dot(direction)).normalized()
            two_bone(bones, [upper.name, elbow.name, hand.name], target, rest_pole.lerp(pole, opening))
            hinge_error = max(hinge_error, elbow_hinge(bones, side))
            forward = (hand.bone.matrix_local.inverted() @ bones['中指１.' + side].bone.head_local).normalized()
            across = (hand.bone.matrix_local.to_3x3().inverted() @
                      (bones['人指１.' + side].bone.head_local - bones['小指１.' + side].bone.head_local)).normalized()
            normal = forward.cross(across).normalized() * sign
            desired = Vector((-sign * .65, 0, .76)).normalized()
            q = base[hand.name].to_quaternion(); q = (q @ forward).rotation_difference(desired) @ q
            current = q @ normal; target_normal = Vector((0, -1, 0))
            angle = math.atan2(desired.dot(current.cross(target_normal)), current.dot(target_normal))
            q = Quaternion(desired, angle) @ q
            hand.matrix = Matrix.LocRotScale(hand.head, base[hand.name].to_quaternion().slerp(q, opening), hand.matrix.to_scale()); update()
            palm_report[side] = float((hand.matrix.to_quaternion() @ normal).dot(target_normal))
            for finger in ('人指', '中指', '薬指', '小指'):
                for joint in ('１', '２', '３'):
                    b = bones[finger + joint + '.' + side]
                    b.rotation_quaternion = b.rotation_quaternion.slerp(Quaternion(), opening * .88)
            update()
        # Long strands retain separate curls and a delayed response to the retreat.
        for b in bones:
            if b.name.startswith(('后发A', '后发B')) and not b.name.startswith(('_',)):
                lag = math.sin(math.pi * time / 3) ** 2 * math.sin(math.tau * time / 3 - .5)
                b.rotation_quaternion = b.rotation_quaternion @ Quaternion((1, 0, 0), math.radians(2.2 * lag))
        update()
        coords = skin()
        radial = coords[:, :2] - np.array((base['下半身'].translation.x, base['下半身'].translation.y + shift.y))
        radial /= np.maximum(np.linalg.norm(radial, axis=1), .02)[:, None]
        coords[:, :2] += radial * (skirt_weights * .035 * lean)[:, None]
        # Mixed calf weights must not drag the planted shoes along with the knees.
        coords[solver.contacts.feet] = start[solver.contacts.feet]
        if frame in (1, 72): coords = start.copy()
        influence = solver.influence(coords, np.array(bones['頭'].head))
        amount = np.zeros(4)
        if frame not in (1, 72) and solver.tree(coords, solver.hair).overlap(solver.tree(coords, solver.arms)):
            amount = solver.minimum(coords, influence)
        frames.append(coords); influences.append(influence); required.append(amount)
        reports.append(dict(frame=frame, palms_forward=palm_report, elbow_axis_error=hinge_error,
                            head=list(bones['頭'].head), hands={s: list(bones['手首.' + s].head) for s in ('L', 'R')}))
        for b in bones:
            for channel in ('location', 'rotation_quaternion', 'scale'): b.keyframe_insert(channel, frame=frame)
        print('DODGE_POSE', args.form, frame, amount.tolist(), flush=True)
    amounts = np.array(required) if args.preview else damped_release(np.array(required), 24)
    amounts[0] = 0.; amounts[-1] = 0.
    solved = []
    for index, (coords, influence, amount) in enumerate(zip(frames, influences, amounts)):
        proposed = solver.deform(coords, influence, amount)
        if solver.tree(proposed, solver.hair).overlap(solver.tree(coords, solver.arms)):
            amount = solver.minimum(coords, influence, amount)
        result = solver.deform(coords, influence, amount)
        assert not solver.tree(result, solver.hair).overlap(solver.tree(result, solver.arms))
        reports[index]['contacts'] = solver.contacts.check(result)
        reports[index]['hair_support'] = amount.tolist(); solved.append(result)
    (output / f'{args.form}-dodge-poses.json').write_text(json.dumps(reports, indent=2), encoding='utf8')
    assert all(not sum(r['contacts'].values()) for r in reports), [r for r in reports if sum(r['contacts'].values())]
    assert np.array_equal(solved[0], solved[-1]) and np.array_equal(solved[0], start)
    baked.shape_key_clear(); baked.data.vertices.foreach_set('co', solved[0].ravel()); baked.shape_key_add(name='Basis')
    for frame, coords in zip(sample_frames[1:], solved[1:]):
        key = baked.shape_key_add(name=f'Dodge_{frame:04}'); key.data.foreach_set('co', coords.ravel())
        for time, value in ((frame - 1, 0.), (frame, 1.), (frame + 1, 0.)):
            key.value = value; key.keyframe_insert('value', frame=time)
    scene['denia_elbow_hinge_corrected'] = True; scene['denia_arm_hair_contact_baked'] = True
    scene.frame_set(1); bpy.context.preferences.filepaths.save_version = 0
    if not args.preview:
        bpy.ops.wm.save_as_mainfile(filepath=str(output / f'Denia{args.form.title()}_right60_dodge.blend'), compress=True)
    (output / f'{args.form}-dodge-poses.json').write_text(json.dumps(reports, indent=2), encoding='utf8')
    if args.preview:
        for frame in sample_frames:
            scene.frame_set(frame); scene.render.filepath = str(output / f'{args.form}-dodge-{frame:04}.png'); bpy.ops.render.render(write_still=True)
    else:
        destination = output / f'renders/{args.form}-dodge'; destination.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(destination / 'frame_'); bpy.ops.render.render(animation=True)
    print('DODGE_COMPLETE', args.form, 'preview' if args.preview else 'render', flush=True)


if __name__ == '__main__':
    main()
