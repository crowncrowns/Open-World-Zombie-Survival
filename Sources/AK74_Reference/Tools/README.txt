Blender 5.2.2: re-export the existing sixteen Actions from AK74_Master.blend.

The .blend is the editable source. These helpers are optional and are not
required to use the exported FBX files or the Unity prefab.

In Blender's Python Console, add this Tools folder to sys.path, then:
  import bpy
  from ak74_animation import export_animations
  export_animations(bpy.data.objects['AK74_Rig'], '/your/output/Animations')

Keep ak74_animation.py and ak74_fbx_rest_export.py together.
The exporter is Blender's standard io_scene_fbx binary writer. A temporary
context corrects default bone-node rest transforms for armature-only clips;
animation baking is unchanged. The installed Blender files are not modified.
Do not call build_actions unless you intentionally want to replace edited
Actions with the original procedural choreography.
