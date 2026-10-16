"""Bake arm-driven back-hair contact and damped release in background Blender.

Run with --python-exit-code 1. Work files and reports must stay in root .cache.
The source .blend retains the rig, packed textures and baked shape animation.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from check_contacts import Contacts, coordinates


class ArmHair:
    def __init__(self, original, armature):
        self.contacts = Contacts(original, armature)
        self.tri = self.contacts.tri
        self.hair = self.contacts.hair
        self.weights = np.zeros((len(original.data.vertices), 4))
        arm_bones = {b.name for b in armature.pose.bones if any(
            name in [b.name] + [p.name for p in b.parent_recursive] for name in ('腕.L', '腕.R'))}
        own = []
        for vertex in original.data.vertices:
            groups = [(original.vertex_groups[g.group].name, g.weight) for g in vertex.groups
                      if original.vertex_groups[g.group].name in armature.pose.bones]
            own.append(max(groups, key=lambda item: item[1])[0] in arm_bones)
            for name, weight in groups:
                for i, (prefix, side) in enumerate((('后发A', 'L'), ('后发A', 'R'), ('后发B', 'L'), ('后发B', 'R'))):
                    if name.startswith(prefix) and name.endswith('.' + side):
                        self.weights[vertex.index, i] += weight
        own = np.array(own)
        names = [m.name for m in original.data.materials]
        material = np.array([t.material_index for t in original.data.loop_triangles])
        self.arms = np.where(own[self.tri].all(axis=1) & ~np.isin(np.arange(len(self.tri)), self.hair)
                             & np.array([names[i] not in ('fur', 'fur di') for i in material]))[0]
        self.groups = [self.hair[np.any(self.weights[self.tri[self.hair], i] > .001, axis=1)] for i in range(4)]

    def tree(self, coords, faces):
        return BVHTree.FromPolygons(coords.tolist(), self.tri[faces].tolist(), all_triangles=True)

    def influence(self, coords, head):
        t = np.clip((head[2] - .03 - coords[:, 2]) / .25, 0, 1)
        return self.weights * (t * t * (3 - 2 * t))[:, None]

    @staticmethod
    def deform(coords, influence, amount):
        result = coords.copy()
        offset = influence @ amount
        result[:, 1] += offset
        result[:, 2] += .15 * offset
        return result

    def minimum(self, coords, influence, initial=None):
        obstacle = self.tree(coords, self.arms)
        amount = np.full(4, .32) if initial is None else initial.copy()
        for _ in range(6):
            for i, faces in enumerate(self.groups):
                floor = 0. if initial is None else initial[i]
                safe = .32
                for value in np.arange(.32, floor - .0201, -.02):
                    value = max(floor, value)
                    amount[i] = value
                    corrected = self.deform(coords, influence, amount)
                    if self.tree(corrected, faces).overlap(obstacle):
                        assert value < .32, ('Excessive strand displacement', i)
                        low, high = value, safe
                        for _ in range(5):
                            mid = (low + high) / 2
                            amount[i] = mid
                            if self.tree(self.deform(coords, influence, amount), faces).overlap(obstacle):
                                low = mid
                            else:
                                high = mid
                        amount[i] = min(.32, high + .004)
                        break
                    safe = value
                else:
                    amount[i] = floor
            corrected = self.deform(coords, influence, amount)
            if not self.tree(corrected, self.hair).overlap(obstacle):
                return amount
        raise AssertionError('Arm/hair contact did not converge')


def damped_release(required, fps):
    # Contact pushes immediately; damped restoration approximates the hair falling back.
    result = np.zeros_like(required)
    position, velocity = required[0].copy(), np.zeros(4)
    for i in range(len(required)):
        anticipated = required[i].copy()
        for j in range(max(0, i - 3), min(len(required), i + 4)):
            anticipated = np.maximum(anticipated, required[j] - .024 * abs(i - j))
        for _ in range(8):
            dt = 1 / (fps * 8)
            velocity += (-110 * position - 20 * velocity) * dt
            position += velocity * dt
            hit = position < anticipated
            position = np.maximum(position, anticipated)
            velocity[hit] = np.maximum(velocity[hit], 0.)
        result[i] = position
    # The hands are back at rest: approach the same supported pose smoothly.
    for i in range(max(0, len(result) - 9), len(result)):
        t = (i - (len(result) - 9)) / 8
        blend = t * t * (3 - 2 * t)
        result[i] = result[i] * (1 - blend) + required[-1] * blend
        result[i] = np.maximum(result[i], required[i])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--form', choices=('pink', 'dark'), required=True)
    parser.add_argument('--probe', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root, output = args.root.resolve(), args.output.resolve()
    if not output.is_relative_to(root / '.cache'):
        raise ValueError('Process files must stay in root .cache')
    output.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.temporary_directory = str(output / 'blender-temp')
    sys.path.insert(0, str(Path(bpy.utils.user_resource('SCRIPTS')) / 'addons/mmd_tools'))
    bpy.ops.preferences.addon_enable(module='mmd_tools')
    path = root / f'artResources/Denia/人物形象与动画/Denia{args.form.title()}_right60_attack.blend'
    backup = output / ('before-' + path.name)
    if not backup.exists():
        __import__('shutil').copy2(path, backup)
    bpy.ops.wm.open_mainfile(filepath=str(backup))
    scene = bpy.context.scene
    arm = next(o for o in scene.objects if o.type == 'ARMATURE')
    original = next(o for o in scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
    baked = bpy.data.objects['Denia_Contact_Baked']
    solver = ArmHair(original, arm)
    raw, influences, required = [], [], []
    for frame in range(1, scene.frame_end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        coords = coordinates(baked)
        influence = solver.influence(coords, np.array(arm.pose.bones['頭'].head))
        amount = solver.minimum(coords, influence)
        raw.append(coords); influences.append(influence); required.append(amount)
        print('CONTACT_TARGET', args.form, frame, amount.tolist(), flush=True)
    raw, required = np.array(raw), np.array(required)
    required[-1] = required[0]
    amounts = damped_release(required, scene.render.fps)
    frames, report = [], []
    for i, (coords, influence, amount) in enumerate(zip(raw, influences, amounts)):
        amount = solver.minimum(coords, influence, amount)
        result = solver.deform(coords, influence, amount)
        assert not solver.tree(result, solver.hair).overlap(solver.tree(result, solver.arms))
        assert np.array_equal(coords[solver.weights.sum(axis=1) == 0], result[solver.weights.sum(axis=1) == 0])
        frames.append(result)
        report.append(dict(frame=i + 1, required=required[i].tolist(), amount=amount.tolist(), arm_hair_contacts=0))
    assert np.max(abs(frames[-1] - frames[0])) < 1e-6, 'Attack seam differs'
    data = np.array(frames)
    np.savez_compressed(output / f'{args.form}-solved.npz', frames=data, required=required,
                        amounts=np.array([r['amount'] for r in report]), weights=solver.weights)
    baked.shape_key_clear()
    baked.data.vertices.foreach_set('co', frames[0].ravel())
    baked.shape_key_add(name='Basis')
    for frame, coords in enumerate(frames[1:], 2):
        key = baked.shape_key_add(name=f'ArmHair_{frame:04}')
        key.data.foreach_set('co', coords.ravel())
        for time, value in ((frame - 1, 0.), (frame, 1.), (frame + 1, 0.)):
            key.value = value
            key.keyframe_insert('value', frame=time)
    scene.frame_set(1)
    tree = scene.compositing_node_group
    natural = next(n for n in tree.nodes if n.bl_idname == 'CompositorNodeRLayers' and n.layer == 'Natural')
    output_node = next(n for n in tree.nodes if n.bl_idname == 'NodeGroupOutput')
    tree.links.new(natural.outputs['Image'], output_node.inputs['Image'])
    scene['denia_arm_hair_contact_baked'] = True
    scene.view_layers['Body'].use = False
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(output / path.name), compress=True)
    (output / f'{args.form}-arm-hair.json').write_text(json.dumps(dict(
        frames=report, max_offset=float(np.linalg.norm(data - raw, axis=2).max()),
        max_hair_step=float(np.linalg.norm(np.diff(data[:, solver.contacts.hair_ids], axis=0), axis=2).max()),
        original_sha256=hashlib.sha256(backup.read_bytes()).hexdigest()), indent=2), encoding='utf8')
    if args.probe:
        for frame in (1, 12, 25, 37, 49, 60, 66, 72):
            scene.frame_set(frame)
            scene.render.filepath = str(output / f'{args.form}-solved-{frame:04}.png')
            bpy.ops.render.render(write_still=True)
    else:
        destination = output / f'renders/{args.form}-attack'
        destination.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(destination / 'frame_')
        bpy.ops.render.render(animation=True)
    print('ARM_HAIR_COMPLETE', args.form, flush=True)


if __name__ == '__main__':
    main()
