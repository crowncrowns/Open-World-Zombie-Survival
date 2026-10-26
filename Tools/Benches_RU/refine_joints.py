from pathlib import Path

p=Path(__file__).with_name("build_benches.py")
s=p.read_text(encoding="utf-8")
a=s.index("def add_grime_and_moss(")
b=s.index("\ndef add_text(",a)
s=s[:a]+'''def add_grime_and_moss(wear, rng, length, seat_z, mats, amount=1):
    if wear < 2: return
    # Deposits stay in the two structural seat joints, below the exposed sitting surface.
    for side in (-1,1):
        for index in range(3 if wear == 2 else 7):
            x=side*length*0.34+rng.uniform(-0.012,0.012)
            y=rng.choice((-0.15,0.15))+rng.uniform(-0.006,0.006)
            z=seat_z-0.023+rng.uniform(-0.004,0.004)
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=(x,y,z))
            obj=bpy.context.object; obj.name="Organic deposit within support joint"
            obj.scale=(rng.uniform(0.006,0.012),rng.uniform(0.004,0.008),rng.uniform(0.002,0.005))
            bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
            assign_material(obj,mats["moss"] if wear==3 and index%2 else mats["dirt"])

''' + s[b:]
a=s.index("        if wear >= 2:\n            # Old pale limewash")
b=s.index("    if wear == 3:\n        beam(\"Exposed reinforcement",a)
s=s[:a]+s[b:]
temp=p.with_suffix(".new")
temp.write_text(s,encoding="utf-8")
temp.replace(p)
