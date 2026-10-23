"""AK74 weapon-only animation authoring and single-take FBX export.

The caller supplies the shared AK74_Rig. Geometry is never created here.
World convention: barrel points toward -X, +Z is up, +X is recoil/rack.
All seven bones receive location/quaternion/scale keys on every frame.
Animations are authored at 60 fps in metres and are deterministic.
"""
import json
import math
from pathlib import Path

import bpy
from mathutils import Euler, Quaternion, Vector

FPS = 60
BONE_NAMES = ("WeaponRoot", "Magazine", "SpareMagazine", "Trigger", "Selector", "Bolt", "Stock")
ZERO = (0.0, 0.0, 0.0)


def _s(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def _sample(frame, stops):
    """Smooth, bounded interpolation of armature-space translation and degrees."""
    if frame <= stops[0][0]:
        return stops[0][1], stops[0][2]
    for left, right in zip(stops, stops[1:]):
        if frame <= right[0]:
            t = _s((frame - left[0]) / (right[0] - left[0]))
            return tuple(a + (b - a) * t for a, b in zip(left[1], right[1])), tuple(a + (b - a) * t for a, b in zip(left[2], right[2]))
    return stops[-1][1], stops[-1][2]


def _stop(f, loc=ZERO, rot=ZERO):
    return (f, loc, rot)


def _event(frame, function, int_parameter=0, string_parameter="", float_parameter=0.0):
    time = (frame - 1) / FPS
    return {"frame": frame, "time": time, "timeSeconds": time, "function": function,
            "functionName": function, "intParameter": int_parameter,
            "floatParameter": float_parameter, "stringParameter": string_parameter}


def _clip(name, end, loop=False, events=None, description=""):
    return {"name": name, "file": "AK74_" + name + ".fbx", "frameStart": 1,
            "frameEnd": end, "fps": FPS, "duration": (end - 1) / FPS,
            "durationSeconds": (end - 1) / FPS, "loop": loop,
            "events": events or [], "description": description}


CLIPS = [
    _clip("Idle", 181, True, description="Three-second breathing and settling cycle; no component drift."),
    _clip("Fire_Single", 22, events=[_event(2, "OnWeaponFire", 1)], description="One 0.35-second recoil, trigger and bolt cycle."),
    _clip("Fire_Auto", 25, True, [_event(f, "OnWeaponFire", 1) for f in (1, 7, 13, 19)], "Four shots at 600 rounds/min; seamless 0.4-second cycle."),
    _clip("Reload_Tactical", 91, events=[_event(30, "OnMagazineRemoved"), _event(39, "OnSpareMagazineShown"), _event(65, "OnMagazineInserted"), _event(91, "OnReloadComplete")], description="1.5-second visible rock-out/rock-in exchange; no bolt operation."),
    _clip("Reload_Empty", 133, events=[_event(32, "OnMagazineRemoved"), _event(47, "OnSpareMagazineShown"), _event(79, "OnMagazineInserted"), _event(108, "OnBoltPulled"), _event(114, "OnBoltReleased"), _event(133, "OnReloadComplete")], description="2.2-second magazine exchange followed by manual charging; AK bolt does not lock open."),
    _clip("Equip", 43, description="0.7-second rise from a lowered carry pose into neutral."),
    _clip("Unequip", 37, description="0.6-second lowering from neutral to the matching carry pose."),
    _clip("Inspect", 193, description="3.2-second inspection with two stable views and a controlled neutral return."),
    _clip("Aim_In", 16, description="0.25-second relative weapon-mount offset; camera alignment is the FPS controller's responsibility."),
    _clip("Aim_Out", 16, description="Inverse relative weapon-mount offset, 0.25 second."),
    _clip("Walk", 73, True, description="1.2-second two-step sway cycle."),
    _clip("Run", 49, True, description="0.8-second two-step sway cycle with greater inertia."),
    _clip("Sprint", 37, True, description="0.6-second lowered and canted two-step sprint cycle."),
    _clip("Recoil_Recovery", 25, description="0.4-second damped return from the Fire_Single recoil apex."),
    _clip("Bolt_Interaction", 61, events=[_event(28, "OnBoltPulled"), _event(36, "OnBoltReleased")], description="One-second deliberate charging-handle pull, hold and spring return."),
    _clip("Safety_Interaction", 67, events=[_event(23, "OnSafetyChanged", 1), _event(48, "OnSafetyChanged", 0)], description="1.1-second selector check; switch to safe, pause and return to the authored fire position."),
]


def manifest():
    return {"schemaVersion": 1, "asset": "AK74", "rig": "AK74_Rig", "fps": FPS,
            "coordinateSystem": "Blender metres; muzzle -X, width Y, up Z",
            "bones": list(BONE_NAMES), "clips": CLIPS,
            "defaultVisibility": {"Magazine": True, "SpareMagazine": False},
            "notes": [
                "Weapon-only animation set, with no hands, arms or FPS camera animation.",
                "All 16 clips are independently baked; each bone has a keyed transform at every frame.",
                "SpareMagazine starts and ends hidden. Unity presentation events own magazine visibility.",
                "Canceling a reload must restore Magazine visible and SpareMagazine hidden.",
                "Aim transitions are relative offsets. The consuming FPS mount must align rear/front sights with its camera.",
                "These clips provide presentation. Ammunition, shot timing, audio and damage remain the game's responsibility.",
            ]}


def write_manifest(path):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(manifest(), ensure_ascii=False, indent=2), encoding="utf-8")
    return str(path)


def _reload(frame, empty):
    if empty:
        root = [_stop(1), _stop(15, (.01, -.012, .008), (20, -6, -9)),
                _stop(30, (.012, -.016, .012), (23, -8, -10)),
                _stop(79, (.012, -.016, .012), (23, -8, -10)),
                _stop(93, (.009, -.013, .012), (32, -5, -7)),
                _stop(116, (.007, -.009, .008), (27, -3, -4)), _stop(133)]
        mag = [_stop(1), _stop(15), _stop(23, (0, 0, -.008), (0, 17, 0)),
               _stop(32, (-.036, -.012, -.175), (0, 31, -5)),
               _stop(78, (-.036, -.012, -.175), (0, 31, -5)), _stop(79)]
        spare = [_stop(1, (-.075, -.01, -.23), (0, 26, -5)),
                 _stop(47, (-.075, -.01, -.23), (0, 26, -5)),
                 _stop(63, (-.012, 0, -.071), (0, 20, 0)),
                 _stop(71, (0, 0, -.012), (0, 12, 0)), _stop(79), _stop(133)]
        bolt = [_stop(1), _stop(98), _stop(108, (.073, 0, 0)),
                _stop(111, (.073, 0, 0)), _stop(114), _stop(133)]
    else:
        root = [_stop(1), _stop(13, (.01, -.012, .008), (20, -6, -9)),
                _stop(29, (.012, -.016, .012), (23, -8, -10)),
                _stop(65, (.012, -.016, .012), (23, -8, -10)), _stop(91)]
        mag = [_stop(1), _stop(13), _stop(21, (0, 0, -.008), (0, 17, 0)),
               _stop(30, (-.036, -.012, -.175), (0, 31, -5)),
               _stop(64, (-.036, -.012, -.175), (0, 31, -5)), _stop(65)]
        spare = [_stop(1, (-.075, -.01, -.23), (0, 26, -5)),
                 _stop(39, (-.075, -.01, -.23), (0, 26, -5)),
                 _stop(52, (-.012, 0, -.071), (0, 20, 0)),
                 _stop(59, (0, 0, -.012), (0, 12, 0)), _stop(65), _stop(91)]
        bolt = [_stop(1), _stop(91)]
    return {"WeaponRoot": _sample(frame, root), "Magazine": _sample(frame, mag),
            "SpareMagazine": _sample(frame, spare), "Bolt": _sample(frame, bolt)}


def _pose(name, f):
    pose = {}
    if name == "Idle":
        t = 2 * math.pi * (f - 1) / 180
        pose["WeaponRoot"] = ((.00045 * math.sin(t), .00075 * math.sin(t), .0009 * math.sin(2*t)),
                              (.16 * math.sin(t), .14 * math.sin(2*t), .09 * math.sin(t)))
    elif name == "Fire_Single":
        pose["WeaponRoot"] = _sample(f, [_stop(1), _stop(3, (.012, -.0008, .0018), (.18, 1.8, -.22)),
                                         _stop(6, (.004, -.0002, .0005), (.05, .65, -.08)),
                                         _stop(12, (-.0006, .0001, -.0002), (0, -.12, .02)), _stop(22)])
        pose["Bolt"] = _sample(f, [_stop(1), _stop(3, (.068, 0, 0)), _stop(6), _stop(22)])
        pose["Trigger"] = _sample(f, [_stop(1), _stop(2, rot=(0,-13,0)), _stop(5, rot=(0,-13,0)), _stop(11), _stop(22)])
    elif name == "Fire_Auto":
        phase = (f - 1) % 6
        # Four complete 10 Hz cycles. Values at f1 and f25 are exactly identical.
        recoil = (0.0, 1.0, .58, .25, .075, .012)[phase]
        bolt = (0.0, .068, .035, .005, 0.0, 0.0)[phase]
        pose["WeaponRoot"] = ((.0105 * recoil, -.0007 * recoil, .0015 * recoil),
                              (.13 * recoil, 1.55 * recoil, -.19 * recoil))
        pose["Bolt"] = ((bolt, 0, 0), ZERO)
        # The trigger stays depressed through an automatic burst; it does not
        # reciprocate in step with the bolt carrier.
        pose["Trigger"] = (ZERO, (0, -12, 0))
    elif name in ("Reload_Tactical", "Reload_Empty"):
        pose = _reload(f, name == "Reload_Empty")
    elif name == "Equip":
        pose["WeaponRoot"] = _sample(f, [_stop(1, (.10, .025, -.15), (8,-24,-14)),
                                         _stop(24, (-.003, -.001, .004), (-.8,1.1,.4)), _stop(43)])
    elif name == "Unequip":
        pose["WeaponRoot"] = _sample(f, [_stop(1), _stop(9, (-.002, -.001, .002), (0,1,0)),
                                         _stop(37, (.10, .025, -.15), (8,-24,-14))])
    elif name == "Inspect":
        pose["WeaponRoot"] = _sample(f, [_stop(1), _stop(32, (.015, -.022, -.022), (-39,6,-23)),
                                         _stop(66, (.015, -.022, -.022), (-39,6,-23)),
                                         _stop(108, (.008, .018, -.025), (31,-5,17)),
                                         _stop(142, (.008, .018, -.025), (31,-5,17)), _stop(193)])
    elif name in ("Aim_In", "Aim_Out"):
        aim = (.014, -.012, .019)
        stops = [_stop(1), _stop(16, aim, (0,.35,0))]
        pose["WeaponRoot"] = _sample(f if name == "Aim_In" else 17-f, stops)
    elif name in ("Walk", "Run", "Sprint"):
        period = {"Walk": 72, "Run": 48, "Sprint": 36}[name]
        a = {"Walk": 1.0, "Run": 1.8, "Sprint": 2.6}[name]
        t = 2 * math.pi * (f-1) / period
        loc = (.0009*a*math.sin(2*t), .0027*a*math.sin(t), .0019*a*(math.cos(2*t)-1))
        rot = (.34*a*math.sin(t), .53*a*math.sin(2*t), .31*a*math.sin(t))
        if name == "Sprint":
            loc = tuple(v + d for v, d in zip(loc, (.025, .014, -.064)))
            rot = tuple(v + d for v, d in zip(rot, (13,-13,-12)))
        pose["WeaponRoot"] = (loc, rot)
    elif name == "Recoil_Recovery":
        pose["WeaponRoot"] = _sample(f, [_stop(1, (.012,-.0008,.0018), (.18,1.8,-.22)),
                                         _stop(7, (.002,0,.0003), (.03,.4,-.04)),
                                         _stop(14, (-.0006,.0001,-.0002), (0,-.12,.02)), _stop(25)])
    elif name == "Bolt_Interaction":
        pose["WeaponRoot"] = _sample(f, [_stop(1), _stop(16, (.008,-.01,.006), (29,-3,-5)),
                                         _stop(39, (.008,-.01,.006), (29,-3,-5)), _stop(61)])
        pose["Bolt"] = _sample(f, [_stop(1), _stop(19), _stop(28, (.073,0,0)),
                                   _stop(33, (.073,0,0)), _stop(36), _stop(61)])
    elif name == "Safety_Interaction":
        pose["WeaponRoot"] = _sample(f, [_stop(1), _stop(15, (.005,-.006,.003), (15,-2,-3)),
                                         _stop(52, (.005,-.006,.003), (15,-2,-3)), _stop(67)])
        pose["Selector"] = _sample(f, [_stop(1), _stop(13), _stop(23, rot=(0,20,0)),
                                       _stop(39, rot=(0,20,0)), _stop(48), _stop(67)])
    return pose


def reset_pose(rig):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.location = ZERO
        bone.rotation_quaternion = (1, 0, 0, 0)
        bone.scale = (1, 1, 1)


def _apply_world_delta(bone, location, degrees):
    # matrix_basis lives in the bone's rest basis, not armature-space XYZ.
    # Conjugation preserves the physical pivot and the intended world axes.
    rest = bone.bone.matrix_local.to_quaternion()
    q = Euler(tuple(math.radians(a) for a in degrees), "XYZ").to_quaternion()
    bone.location = rest.inverted() @ Vector(location)
    bone.rotation_quaternion = rest.inverted() @ q @ rest


def _fcurves(action):
    if hasattr(action, "layers"):
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        yield curve
    elif hasattr(action, "fcurves"):
        yield from action.fcurves


def build_actions(rig):
    """Create 16 Blender-5 layered actions; return manifest clip metadata."""
    if rig.type != "ARMATURE":
        raise TypeError("build_actions requires the shared armature")
    missing = set(BONE_NAMES) - set(rig.pose.bones.keys())
    if missing:
        raise RuntimeError("Rig lacks bones: " + ", ".join(sorted(missing)))
    if set(rig.pose.bones.keys()) != set(BONE_NAMES):
        raise RuntimeError("Unexpected bones would break the seven-bone animation contract")
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.render.fps_base = 1.0
    rig.animation_data_create()
    for track in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(track)
    for metadata in CLIPS:
        name = metadata["name"]
        old = bpy.data.actions.get(name)
        if old:
            bpy.data.actions.remove(old)
        reset_pose(rig)
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        action.use_frame_range = True
        action.frame_start = 1
        action.frame_end = metadata["frameEnd"]
        action["weapon_clip"] = True
        action["loop"] = metadata["loop"]
        action["fps"] = FPS
        action["events_json"] = json.dumps(metadata["events"])
        rig.animation_data.action = action
        for frame in range(1, metadata["frameEnd"] + 1):
            scene.frame_set(frame)
            reset_pose(rig)
            for bone_name, (location, degrees) in _pose(name, frame).items():
                _apply_world_delta(rig.pose.bones[bone_name], location, degrees)
            for bone in rig.pose.bones:
                bone.keyframe_insert("location", frame=frame, group=bone.name)
                bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
                bone.keyframe_insert("scale", frame=frame, group=bone.name)
        for curve in _fcurves(action):
            for key in curve.keyframe_points:
                key.interpolation = "LINEAR"
        for event in metadata["events"]:
            marker = action.pose_markers.new(event["functionName"])
            marker.frame = event["frame"]
        if len(list(_fcurves(action))) != 70:
            raise RuntimeError(name + ": incomplete 7 x 10-channel bake")
    rig.animation_data.action = bpy.data.actions["Idle"]
    scene.frame_start = 1
    scene.frame_end = 181
    scene.frame_set(1)
    reset_pose(rig)
    bpy.context.view_layer.update()
    return json.loads(json.dumps(CLIPS))


def export_animations(rig, directory):
    """Export armature-only single-take FBXs with one identical rest hierarchy.

    Only the chosen action is sampled. There are no leaf/end bones, geometry,
    cameras, lights, NLA tracks or duplicate action takes in these files.
    """
    from ak74_fbx_rest_export import canonical_rest_bone_nodes
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    previous_action = rig.animation_data.action
    previous_frame = scene.frame_current
    previous_range = (scene.frame_start, scene.frame_end)
    previous_selection = list(bpy.context.selected_objects)
    previous_active = bpy.context.view_layer.objects.active
    exports = []
    try:
        bpy.ops.object.select_all(action="DESELECT")
        rig.hide_set(False)
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        for metadata in CLIPS:
            rig.animation_data.action = bpy.data.actions[metadata["name"]]
            scene.frame_start = 1
            scene.frame_end = metadata["frameEnd"]
            scene.frame_set(1)
            path = directory / metadata["file"]
            # Canonical rest defaults in bone Model nodes; the standard FBX
            # writer still samples and writes the unmodified authored action.
            with canonical_rest_bone_nodes():
                bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
                    object_types={"ARMATURE"}, global_scale=1.0,
                    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                    axis_forward="-Z", axis_up="Y", use_space_transform=True,
                    bake_space_transform=False, add_leaf_bones=False,
                    primary_bone_axis="Y", secondary_bone_axis="X",
                    use_armature_deform_only=True, bake_anim=True,
                    bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                    bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
                    path_mode="AUTO", embed_textures=False)
            if not path.is_file() or path.stat().st_size < 1024:
                raise RuntimeError("FBX not written: " + str(path))
            exports.append(str(path))
    finally:
        rig.animation_data.action = previous_action
        scene.frame_start, scene.frame_end = previous_range
        scene.frame_set(previous_frame)
        bpy.ops.object.select_all(action="DESELECT")
        for obj in previous_selection:
            if obj.name in bpy.context.view_layer.objects:
                obj.select_set(True)
        if previous_active and previous_active.name in bpy.context.view_layer.objects:
            bpy.context.view_layer.objects.active = previous_active
    write_manifest(directory / "AK74_AnimationManifest.json")
    return exports


if __name__ == "__main__":
    # Direct invocation only writes the readable metadata; it never touches a scene.
    write_manifest(Path(__file__).with_name("ak74_animation_manifest.json"))
