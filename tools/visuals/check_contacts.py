"""Check baked Denia animation surfaces in background Blender.

Run with --python-exit-code 1. Reports go exclusively under workspace .cache.
Scalp attachments, coincident hair layers and transparent fur cards are intentional
overlaps; the checks cover forehead hair/body, forehead hair/gloves, gloves/other
body, and skirt/leg skin. The complete mesh renders with normal depth ordering;
only visible gloves receive a hair-free overlay. Hair intersections are reported
separately, not certified as collision-free.
This is not a general mesh self-intersection checker.
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


def coordinates(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    result = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get('co', result)
    evaluated.to_mesh_clear()
    return result.reshape(-1, 3)


class Contacts:
    def __init__(self, mesh, armature):
        mesh.data.calc_loop_triangles()
        self.tri = np.array([list(t.vertices) for t in mesh.data.loop_triangles])
        material = np.array([t.material_index for t in mesh.data.loop_triangles])
        names = [m.name for m in mesh.data.materials]
        dominant, sides = [], {}
        for bone in armature.pose.bones:
            lineage = [bone.name] + [p.name for p in bone.parent_recursive]
            sides[bone.name] = next((s for s in ('L', 'R') if '腕.' + s in lineage), None)
        for vertex in mesh.data.vertices:
            groups = [g for g in vertex.groups if mesh.vertex_groups[g.group].name in armature.pose.bones]
            dominant.append(mesh.vertex_groups[max(groups, key=lambda g: g.weight).group].name if groups else '')
        hair = [i for i, name in enumerate(names) if '发' in name or '髪' in name]
        self.hair = np.where(np.isin(material, hair))[0]
        self.hair_ids = np.unique(self.tri[self.hair])
        head = np.array([all(dominant[v] in ('頭', '首') for v in t) for t in self.tri])
        self.solid = np.where(~np.isin(material, hair) & ~head & np.array([
            names[i] not in ('fur', 'emo1', 'emo3', 'Cheek') and i >= 11 for i in material]))[0]
        moving = np.array([not all(dominant[v] == '頭' for v in t) for t in self.tri])
        self.moving_hair = self.hair[moving[self.hair]]
        self.back_hair = np.where(np.isin(material, hair) & (
            moving | np.array(['后发' in names[i] for i in material])))[0]
        self.front_hair = np.setdiff1d(self.hair, self.back_hair)
        self.gloves = np.where(np.array([names[i] == '手套' for i in material]))[0]
        self.hands = {}
        for side in ('L', 'R'):
            own = np.array([sides.get(n) == side for n in dominant])
            glove = self.gloves[own[self.tri[self.gloves]].all(axis=1)]
            other = self.solid[~own[self.tri[self.solid]].any(axis=1)]
            self.hands[side] = (glove, other)
        leg = np.array([n.startswith(('足D', 'ひざD', '足首D', '足先EX', '足調整')) for n in dominant])
        self.legs = np.where(leg[self.tri].all(axis=1) & np.array([names[i] == '肌肤' for i in material]))[0]
        skirt = np.array([n.startswith(('スカート', '披风_')) for n in dominant])
        self.skirt = np.where(skirt[self.tri].any(axis=1) & ~np.isin(np.arange(len(self.tri)), self.hair))[0]
        shoes = np.where(np.array([names[i] == '鞋子' for i in material]))[0]
        self.feet = np.unique(self.tri[shoes])
        head_group = mesh.vertex_groups['頭'].index
        hair_vertices = set(self.hair_ids)
        self.head = np.array([v.index for v in mesh.data.vertices if v.index not in hair_vertices
            and any(g.group == head_group and g.weight > .99999 for g in v.groups)])

    def count(self, coords, first, second):
        points = coords.tolist()
        a = BVHTree.FromPolygons(points, self.tri[first].tolist(), all_triangles=True)
        b = BVHTree.FromPolygons(points, self.tri[second].tolist(), all_triangles=True)
        return len(a.overlap(b))

    def check(self, coords):
        result = {'forehead_hair_body': self.count(coords, self.front_hair, self.solid),
                  'forehead_hair_gloves': self.count(coords, self.front_hair, self.gloves),
                  'skirt_legs': self.count(coords, self.skirt, self.legs)}
        for side, (glove, other) in self.hands.items():
            result['glove_' + side] = self.count(coords, glove, other)
        return result


def check_occlusion(scene, baked):
    body, natural = bpy.data.objects['DeniaBodyRender'], bpy.data.objects['DeniaNaturalRender']
    assert body.data == baked.data == natural.data, 'Render layers must share the baked animation'
    assert baked.hide_render and not body.hide_render and not natural.hide_render
    assert not natural.modifiers, 'Natural layer must render the complete mesh'
    assert scene.render.film_transparent and scene.render.use_compositing
    # shortcut: visible-hand compositing is authored for the fixed camera;
    # recompute spatial contacts before using these meshes from another camera.
    attribute = baked.data.attributes['denia_hair_surface']
    for polygon, flag in zip(baked.data.polygons, attribute.data):
        name = baked.data.materials[polygon.material_index].name
        assert flag.value == ('发' in name or '髪' in name), 'Hair mask must retain whole material surfaces'
    body_view, natural_view = scene.view_layers['Body'], scene.view_layers['Natural']
    assert body_view.layer_collection.children['Denia_Natural_Layer'].exclude
    assert natural_view.layer_collection.children['Denia_Body_Layer'].exclude
    previous_view = bpy.context.window.view_layer
    for obj, view in ((body, body_view), (natural, natural_view)):
        bpy.context.window.view_layer = view
        bpy.context.view_layer.update()
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        assert mesh.polygons
        if obj == body:
            assert not any(flag.value for flag in mesh.attributes['denia_hair_surface'].data)
        else:
            assert len(mesh.polygons) == len(baked.data.polygons), 'Natural layer lost faces'
        evaluated.to_mesh_clear()
    bpy.context.window.view_layer = previous_view
    assert any(a.name == 'DeniaVisibleHands' and a.type == 'VALUE' for a in body_view.aovs)
    for material in baked.data.materials:
        nodes = [n for n in material.node_tree.nodes if n.bl_idname == 'ShaderNodeOutputAOV'
                 and n.aov_name == 'DeniaVisibleHands']
        assert len(nodes) == (1 if material.name == '手套' else 0), 'Overlay mask includes non-glove material'
        if nodes:
            assert not nodes[0].inputs['Value'].is_linked and nodes[0].inputs['Value'].default_value == 1
    tree = scene.compositing_node_group
    mix = next(n for n in tree.nodes if n.bl_idname == 'ShaderNodeMix')
    assert mix.data_type == 'RGBA' and mix.blend_type == 'MIX'
    mask = mix.inputs[0].links[0].from_socket
    assert mask.name == 'DeniaVisibleHands' and mask.node.layer == 'Body'
    assert mix.inputs[6].links[0].from_node.layer == 'Natural'
    assert mix.inputs[7].links[0].from_node.layer == 'Body'
    output = next(n for n in tree.nodes if n.bl_idname == 'NodeGroupOutput')
    assert output.inputs['Image'].links[0].from_node == mix
    assert output.inputs['Image'].links[0].from_socket.type == 'RGBA'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[4])
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    root, output = args.root.resolve(), args.output.resolve()
    if not output.is_relative_to(root / '.cache'):
        raise ValueError('Reports must be under workspace .cache')
    output.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.temporary_directory = str(output / 'blender-temp')
    sys.path.insert(0, str(Path(bpy.utils.user_resource('SCRIPTS')) / 'addons/mmd_tools'))
    bpy.ops.preferences.addon_enable(module='mmd_tools')
    idle, summary = {}, {}
    clips = [('pink', 'idle', 96), ('dark', 'idle', 96), ('pink', 'attack', 72), ('dark', 'attack', 72), ('pink', 'death', 48)]
    for form, clip, count in clips:
        path = root / f'artResources/Denia/人物形象与动画/Denia{form.title()}_right60_{clip}.blend'
        bpy.ops.wm.open_mainfile(filepath=str(path))
        scene = bpy.context.scene
        baked = bpy.data.objects['Denia_Contact_Baked']
        arm = next(o for o in scene.objects if o.type == 'ARMATURE')
        original = next(o for o in scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
        contacts = Contacts(original, arm)
        assert scene.frame_end == count
        scene.frame_set(1)
        bpy.context.view_layer.update()
        check_occlusion(scene, baked)
        frames, hair, first, feet = [], [], None, None
        head_local, max_head_error = None, 0.
        for frame in range(1, count + 1):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            coords = coordinates(baked)
            if first is None:
                first, feet = coords.copy(), coords[contacts.feet].copy()
            result = contacts.check(coords)
            assert sum(result.values()) == 0, (form, clip, frame, result)
            result['occluded_hair_body_3d_diagnostic'] = contacts.count(coords, contacts.back_hair, contacts.solid)
            result['hair_gloves_3d_diagnostic'] = contacts.count(coords, contacts.hair, contacts.gloves)
            assert coords[:, 2].min() >= -.001, (form, clip, frame, 'ground')
            head_matrix = arm.pose.bones['頭'].matrix
            local = (coords[contacts.head] - np.array(head_matrix.translation)) @ np.array(head_matrix.to_3x3())
            if head_local is None:
                head_local = local.copy()
            max_head_error = max(max_head_error, float(np.max(abs(local - head_local))))
            assert max_head_error < 2e-6, ('Head deformed', form, clip, frame, max_head_error)
            if clip == 'idle':
                assert np.max(abs(coords[contacts.feet] - feet)) < 1e-6, 'Idle feet moved'
            frames.append(dict(frame=frame, pairs=result, min_z=float(coords[:, 2].min())))
            hair.append(coords[contacts.hair_ids])
        if clip == 'idle':
            idle[form] = first
        else:
            assert np.max(abs(first - idle[form])) < 1e-6, 'Start pose differs from idle'
        if clip == 'attack':
            assert np.max(abs(coords - first)) < 1e-6, 'Attack end differs from idle'
        hair = np.array(hair)
        summary[form + '-' + clip] = dict(frames=count, sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            max_hair_travel=float(np.linalg.norm(hair - hair[0], axis=2).max()),
            max_hair_step=float(np.linalg.norm(np.diff(hair, axis=0), axis=2).max()),
            max_head_error=max_head_error, hair_render='Complete mesh with visible-glove mask verified', contacts=frames)
        (output / f'{form}-{clip}-contacts.json').write_text(json.dumps(summary[form + '-' + clip], indent=2), encoding='utf8')
        print('CONTACTS_PASS', form, clip, count, flush=True)
    (output / 'contacts-summary.json').write_text(json.dumps(summary, indent=2), encoding='utf8')
    print('ALL_384_FRAMES_PASS', flush=True)


if __name__ == '__main__':
    main()
