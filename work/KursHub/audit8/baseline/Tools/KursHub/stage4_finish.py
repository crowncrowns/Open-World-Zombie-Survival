"""Stage 4: finishes and reusable prop placements, separate from the shell FBX."""
TEX=ROOT/'Assets/Environment/KursHub/Textures'
for name,prefix in {'BlueCladding':'BluePaint','MallBlue':'BluePaint','Concrete':'CreamConcrete',
    'CreamPanel':'CreamConcrete','Floor':'FloorConcrete','Frame':'RustedSteel','SteelOxide':'RustedSteel',
    'Roof':'RoofMembrane','Glass':'GlassGrime','TarpGreen':'TarpGreen','SandCanvas':'SandCanvas',
    'WornWood':'WornWood','DirtStain':'DirtStreaks'}.items():
    mat=materials[name];nodes=mat.node_tree.nodes;links=mat.node_tree.links;bsdf=nodes.get('Principled BSDF')
    uv=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeVectorMath');mapping.operation='SCALE'
    mapping.inputs[3].default_value=1 if name=='DirtStain' else .5
    links.new(uv.outputs['UV'],mapping.inputs[0])
    def texture(suffix,linear=False):
        path=TEX/(prefix+'_'+suffix+'.png')
        if not path.exists():return None
        n=nodes.new('ShaderNodeTexImage');n.image=bpy.data.images.load(str(path),check_existing=True)
        if linear:n.image.colorspace_settings.name='Non-Color'
        links.new(mapping.outputs[0],n.inputs['Vector']);return n
    albedo=texture('BaseColor');assert albedo,name
    if name=='Floor':
        tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(1.15,1.15,1.15,1)
        links.new(albedo.outputs['Color'],tint.inputs[1]);links.new(tint.outputs[0],bsdf.inputs['Base Color'])
    else:links.new(albedo.outputs['Color'],bsdf.inputs['Base Color'])
    if name in ('Glass','DirtStain'):
        alpha=nodes.new('ShaderNodeMath');alpha.operation='MULTIPLY';alpha.inputs[1].default_value=.5 if name=='Glass' else .7
        links.new(albedo.outputs['Alpha'],alpha.inputs[0]);links.new(alpha.outputs[0],bsdf.inputs['Alpha']);mat.surface_render_method='DITHERED'
    normal=texture('Normal',True)
    if normal:
        n=nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.35
        links.new(normal.outputs['Color'],n.inputs['Color']);links.new(n.outputs[0],bsdf.inputs['Normal'])
    mask=texture('MetallicSmoothness',True)
    if mask:
        split=nodes.new('ShaderNodeSeparateColor');links.new(mask.outputs['Color'],split.inputs[0]);links.new(split.outputs['Red'],bsdf.inputs['Metallic'])
        inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
        links.new(mask.outputs['Alpha'],inv.inputs[1]);links.new(inv.outputs[0],bsdf.inputs['Roughness'])

def place(id,prop,p,yaw=0,scale=(1,1,1)):
    prop_instances.append(dict(id=id,prop=prop,position=list(p),rotation=[0,yaw,0],scale=list(scale)))
place('West lookout','KH_WatchPost',(-37,14.12,-38))
place('East lookout','KH_WatchPost',(36,11.02,-23),180)
place('West roof barrier','KH_Barricade',(-33,11.02,-18),25)
place('East roof barrier','KH_Barricade',(39,11.02,-17),-15)
for side,start in (('west',.73),('east',.20)):
    for row in range(3):
        for i in range(7-row):
            theta=math.pi*(start+(i+.5*(row%2))*.009)
            yaw=-math.degrees(math.atan2(14.65*math.cos(theta),29.65*math.sin(theta)))
            place(f'{side} sandbag {row}_{i}','KH_Sandbag',(29.65*math.cos(theta),12.15+row*.28,-50-14.65*math.sin(theta)),yaw)
place('Left high drape','KH_TarpNarrow',(-42.4,11.15,-50.25),scale=(.8,1,1))
place('Terrace cloth','KH_TarpWide',(-37.5,2.75,-56.22))
place('Right drape','KH_TarpNarrow',(38,9.6,-50.18),scale=(1.3,1.1,1))
place('West side cloth','KH_TarpWide',(-46.62,9.7,-35),90)
for id,prop,p,yaw in [
    ('Left low boards','KH_WindowBoards',(-46.64,1.45,-15.4),90),
    ('Left upper patch','KH_CorrugatedPatch',(-46.64,5.85,22.8),90),
    ('Right upper boards','KH_WindowBoards',(46.64,5.85,15.1),-90),
    ('Right lower patch','KH_CorrugatedPatch',(46.64,1.45,53.4),-90),
    ('Rear boards','KH_WindowBoards',(-19,5.85,65.18),180)]:place(id,prop,p,yaw)
def connect(id,a,b,sag):
    dx,dz=b[0]-a[0],b[2]-a[2]
    place(id,'KH_CableSag',tuple((a[i]+b[i])/2 for i in range(3)),
          math.degrees(math.atan2(-dz,dx)),(math.hypot(dx,dz)/10,sag,1))
connect('West cable',(-38.9,17.3,-36.1),(-41,17.3,-10),.7)
connect('East cable',(34.1,14.2,-24.9),(39,14.2,-10),.9)
connect('Rear cable',(22,16.7,65.35),(29,16.7,65.35),.6)
for id,p,yaw in [('Entry west lamp',(-8,3.65,-64.65),0),('Entry east lamp',(8,3.65,-64.65),0),
    ('Terrace lamp',(-37,5.8,-56.22),0),('East shoulder lamp',(37,5.8,-50.2),0),
    ('Rear left lamp',(-22,5.2,65.22),180),('Rear right lamp',(24,5.2,65.99),180)]:place(id,'KH_Floodlight',p,yaw)
for id,p,yaw,s in [('Terrace stain',(-38,0,-56.04),0,(3,1.3,1)),('Front right stain',(37,0,-50.04),0,(3,.65,1)),
    ('Left stain',(-46.53,0,-5),90,(5,.6,1)),('Right stain',(46.53,0,28),-90,(4,.65,1)),('Rear stain',(-32,0,65.03),180,(4,.65,1))]:place(id,'KH_GrimePatch',p,yaw,s)

# Native collection instances preserve editing and reusable source relationships.
source=ROOT/'Sources/KursHub/KursHubProps.blend';assert source.exists(),'Build props before the full hub'
records=json.loads((OUT/'Props/KursHubPropsManifest.json').read_text(encoding='utf8'))['props']
names={r['id']:r.get('collection',r['id']) for r in records}
with bpy.data.libraries.load(str(source),link=False) as (data_from,data_to):data_to.collections=list(names.values())
loaded={c.name:c for c in data_to.collections if c}
for item in prop_instances:
    c=loaded[names[item['prop']]]
    instance=bpy.data.objects.new(item['id'],None);instance.instance_type='COLLECTION';instance.instance_collection=c
    collection('Reusable exterior props').objects.link(instance)
    instance.location=xyz(item['position']);instance.rotation_euler=(0,0,-math.radians(item['rotation'][1]));instance.scale=xyz(item['scale'])
    instance['UnityPrefab']='Assets/Environment/KursHub/Props/Prefabs/'+item['prop']+'.prefab'
    for obj in c.objects:
        if obj.type=='MESH':
            for slot in obj.material_slots:
                original=slot.material.name.split('.')[0] if slot.material else ''
                if original in materials:slot.material=materials[original]
