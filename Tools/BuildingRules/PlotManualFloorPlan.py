import json
from PIL import Image, ImageDraw
from pathlib import Path
root=Path('Verification/AllBuildingsV6_20261027')
for row in json.loads((root/'ManualFloorPlan.json').read_text(encoding='utf-8-sig')):
    walls=row['walls']; lo=min(w['minx'] for w in walls); hi=max(w['maxx'] for w in walls)
    low=min(w['minz'] for w in walls); high=max(w['maxz'] for w in walls)
    scale=min(1100/(hi-lo),900/(high-low)); im=Image.new('RGB',(1200,1000),'white'); d=ImageDraw.Draw(im)
    def p(x,z): return (45+(x-lo)*scale,45+(high-z)*scale)
    for w in walls:
        a=p(w['minx'],w['maxz']);b=p(w['maxx'],w['minz'])
        color='#cc8989' if 'Door_Leaf' in w['path'] else '#737f90'
        d.rectangle((a,b),fill=color)
    for door in row['doors']:
        xyz=[float(s) for s in door['pos'].strip('()').split(',')]
        if -.95<=xyz[1]<=1:
            a=p(xyz[0],xyz[2]);d.ellipse((a[0]-3,a[1]-3,a[0]+3,a[1]+3),fill='red');d.text(a, f"{xyz[0]:.1f},{xyz[2]:.1f}",fill='red')
    d.text((15,15),row['name']+' | wall slice y=1.7; red=door',fill='black')
    im.save(root/(row['name'].replace(' ','_')+'_Plan.png'))
