"""Ensure canonical rest transforms in animation-only Blender FBX exports.

Blender 5.2's standard FBX writer writes bone Model default transforms from
the current pose after animation baking. For armature-only FBXs there are no
skin clusters to supply a neutral bind transform on import. A clip whose first
frame is non-neutral can therefore import with a different rest skeleton.

This session-local context changes only default bone Model transforms to
use the armature's actual rest matrices. Animation sampling uses the original
writer unchanged. Nothing in the installed Blender exporter is edited, and
the patch is removed in finally even when export raises an exception.
"""
from contextlib import contextmanager


@contextmanager
def canonical_rest_bone_nodes():
    from io_scene_fbx import export_fbx_bin
    from io_scene_fbx.fbx_utils import ObjectWrapper
    original_element = export_fbx_bin.fbx_data_object_elements
    original_tx = ObjectWrapper.fbx_object_tx
    writing_default_bone_node = [False]

    def default_bone_element(root, ob_obj, scene_data):
        previous = writing_default_bone_node[0]
        writing_default_bone_node[0] = ob_obj.is_bone
        try:
            return original_element(root, ob_obj, scene_data)
        finally:
            writing_default_bone_node[0] = previous

    def rest_default_tx(self, scene_data, rest=False, rot_euler_compat=None):
        return original_tx(self, scene_data,
                           rest=(rest or (writing_default_bone_node[0] and self.is_bone)),
                           rot_euler_compat=rot_euler_compat)

    export_fbx_bin.fbx_data_object_elements = default_bone_element
    ObjectWrapper.fbx_object_tx = rest_default_tx
    try:
        yield
    finally:
        export_fbx_bin.fbx_data_object_elements = original_element
        ObjectWrapper.fbx_object_tx = original_tx


def export_animations_with_shared_rest(rig, directory):
    """Compatibility alias; the official exporter now scopes its own correction."""
    from ak74_animation import export_animations
    return export_animations(rig, directory)
