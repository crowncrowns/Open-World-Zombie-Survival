"""Update only nested interior architecture; the accepted shell FBX stays intact.

Blender --background --python this_file -- --root <staging-project>.
The full shell generator calls attach() before saving for reproducible authoring.
"""
from pathlib import Path
import argparse
import json
import math
import runpy
import sys


def attach(root, placements, group_name='Reusable interior architecture'):
    import bpy
    group = bpy.data.collections.get(group_name)
    if group is None:
        group = bpy.data.collections.new(group_name)
        bpy.context.scene.collection.children.link(group)
    else:
        expected = {p['id'] for p in placements}
        assert all(obj.name in expected and obj.instance_type == 'COLLECTION'
                   for obj in group.objects), 'Unknown authored interior object; preserve it before rebuild'
        for obj in list(group.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
    ids = sorted({p['prop'] for p in placements})
    # Old interior prototypes are now unreferenced. Reload the freshly built
    # library rather than leaving stale source geometry after a module revision.
    for name in ids:
        collection = bpy.data.collections.get(name)
        if collection is not None:
            assert not any(obj.instance_collection == collection for obj in bpy.data.objects), name
            for obj in list(collection.objects):
                if obj.users == 1:
                    bpy.data.objects.remove(obj, do_unlink=True)
            bpy.data.collections.remove(collection)
    with bpy.data.libraries.load(str(root / 'Sources/KursHub/KursHubProps.blend'), link=False) as (_, data):
        data.collections = ids
    library = {c.name: c for c in data.collections}
    for collection in library.values():
        for obj in collection.objects:
            for slot in obj.material_slots:
                if slot.material:
                    base = bpy.data.materials.get(slot.material.name.split('.')[0])
                    if base:slot.material = base
    for placement in placements:
        instance = bpy.data.objects.new(placement['id'], None)
        instance.instance_type = 'COLLECTION'
        instance.instance_collection = library[placement['prop']]
        group.objects.link(instance)
        x,y,z = placement['position'];instance.location = (x,z,y)
        instance.rotation_euler = (0,0,-math.radians(placement['rotation'][1]))
        x,y,z = placement['scale'];instance.scale = (x,z,y)
        instance['UnityPrefab'] = 'Assets/Environment/KursHub/Props/Prefabs/' + placement['prop'] + '.prefab'
    bpy.context.scene['KursHubStage'] = '5: empty interior zones and reusable architectural modules'


def main():
    import bpy
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True)
    root=parser.parse_args(sys.argv[sys.argv.index('--')+1:]).root.resolve()
    layout=runpy.run_path(str(Path(__file__).with_name('stage5_interior.py')))['layout']()
    path=root/'Assets/Environment/KursHub/KursHubManifest.json'
    manifest=json.loads(path.read_text(encoding='utf8'))
    bpy.ops.wm.open_mainfile(filepath=str(root/'Sources/KursHub/KursHub.blend'))
    attach(root,layout['interiorInstances'])
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(root/'Sources/KursHub/KursHub.blend'))
    manifest.update(stage=5,**layout)
    path.write_text(json.dumps(manifest,indent=2),encoding='utf8')
    print('KURS_STAGE5_INTERIOR_OK',len(layout['interiorInstances']),'nested placements;',len(layout['interiorZones']),'empty zones')


if __name__ == '__main__':main()
