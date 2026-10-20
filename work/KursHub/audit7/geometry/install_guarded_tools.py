"""Promote the actually exercised audit and repair routines into reusable tools."""
from pathlib import Path
import textwrap
W=Path(__file__).resolve().parent
PROJECT=W.parents[3]
TOOLS=PROJECT/'Tools/KursHub'

source=(W/'verify_exports.py').read_text(encoding='utf8')
head=source[:source.index('OUT=')]
function=source[source.index('def geometry(mesh):'):source.index('\nresults=[];errors=[]')]
body=source[source.index('results=[];errors=[]'):source.index("if errors:raise RuntimeError")]
new=head+'import argparse, sys\n\n'+function+'\ndef run(out, raise_on_failure=True):\n'
new+=textwrap.indent("OUT=pathlib.Path(out).resolve()\nPACKAGE=OUT/'output'\nmanifest=json.loads((PACKAGE/'Assets/Environment/KursHub/Props/KursHubPropsManifest.json').read_text(encoding='utf8'))\n"+body,'    ')
new+="    if errors and raise_on_failure:\n        raise RuntimeError('FBX roundtrip found failures; see fbx_roundtrip.json')\n    return report\n\n"
new+="if __name__=='__main__':\n    parser=argparse.ArgumentParser(description=__doc__)\n    parser.add_argument('--out',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parents[2]/'work/KursHub/audit7/geometry')\n    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])\n    run(args.out)\n"
compile(new,'audit7_verify_props.py','exec')
(TOOLS/'audit7_verify_props.py').write_text(new,encoding='utf8')

source=(W/'repair_lod_exports.py').read_text(encoding='utf8')
head=source[:source.index('OUT=')]
body=source[source.index("manifest_path="):]
new=head+'import argparse, sys\n\ndef run(out):\n'
new+=textwrap.indent("OUT=pathlib.Path(out).resolve()\nPACKAGE=OUT/'output'\n"+body,'    ')
new+='    return fixes\n\n'
new+="if __name__=='__main__':\n    parser=argparse.ArgumentParser(description=__doc__)\n    parser.add_argument('--out',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parents[2]/'work/KursHub/audit7/geometry')\n    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])\n    run(args.out)\n"
compile(new,'audit7_repair_lods.py','exec')
(TOOLS/'audit7_repair_lods.py').write_text(new,encoding='utf8')

path=TOOLS/'audit7_geometry.py'
source=path.read_text(encoding='utf8')
start=source.index('def roundtrip():')
end=source.index('\n\nif args.verify_only:',start)
replacement='''def roundtrip():
    """Check actual exports; repair only reduced levels, then check all again."""
    import importlib.util
    def helper(filename):
        path=pathlib.Path(__file__).resolve().parent/filename
        spec=importlib.util.spec_from_file_location(path.stem,path)
        module=importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module
    verifier=helper('audit7_verify_props.py')
    report=verifier.run(OUT,raise_on_failure=False)
    if report['errors'] and not args.verify_only:
        # No failed LOD0 may be silently replaced. The repair helper refuses
        # it and preserves originals of every rejected reduced FBX file.
        helper('audit7_repair_lods.py').run(OUT)
        report=verifier.run(OUT,raise_on_failure=False)
    if report['errors']:
        raise RuntimeError('Actual FBX import remains invalid; see fbx_roundtrip.json')
    return report
'''
source=source[:start]+replacement+source[end:]
compile(source,'audit7_geometry.py','exec')
path.write_text(source,encoding='utf8')
print('GUARDED_TOOLING_INSTALLED',TOOLS)
