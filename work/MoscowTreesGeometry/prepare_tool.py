from pathlib import Path
root=Path(__file__).resolve().parent
s=(root/'repair.py').read_text()
s=s.replace("root=pathlib.Path(__file__).resolve().parent\nsource=root/'backup/Models_FBX'\ndest=root/'Models_FBX'; reports=[]",'''import argparse,sys
parser=argparse.ArgumentParser(description='Repair the original Moscow Trees tube winding/caps and thicken trunks. Input must be unmodified Blender-exported original models, not an already repaired set.')
parser.add_argument('--input',required=True)
parser.add_argument('--output',required=True)
parser.add_argument('--report',required=True)
parser.add_argument('--diameter-scale',type=float,default=1.6)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
source=pathlib.Path(args.input).resolve(); dest=pathlib.Path(args.output).resolve()
assert source!=dest, 'Keep the input unchanged; choose a separate output directory'
assert .5<=args.diameter_scale<=3
report=pathlib.Path(args.report).resolve(); report.parent.mkdir(parents=True,exist_ok=True)
reports=[]''')
s=s.replace('1.6*(a*math.cos(t)+b*math.sin(t))','args.diameter_scale*(a*math.cos(t)+b*math.sin(t))')
s=s.replace("record['trunk_diameter_multiplier']=1.6","record['trunk_diameter_multiplier']=args.diameter_scale")
s=s.replace("(root/'geometry_report.json').write_text",'report.write_text')
out=Path('Tools/MoscowTrees/RepairGeometry.py'); out.parent.mkdir(parents=True,exist_ok=True); out.write_text(s,encoding='utf-8')
