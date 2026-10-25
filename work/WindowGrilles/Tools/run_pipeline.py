from pathlib import Path
import json
import subprocess
import sys
import time

base=Path(__file__).resolve().parents[1]
repo=base.parents[1]
verification=base/'Verification'
blender=Path(r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe')
unity=Path(r'E:\Unity\6000.6.3f1\Editor\Unity.exe')
status_file=verification/'pipeline_status.json'

def status(stage,**extra):
    status_file.write_text(json.dumps({'stage':stage,'time':time.time(),**extra},indent=2),encoding='utf-8')

def run(stage,args):
    status(stage)
    with (verification/f'pipeline_{stage}.log').open('w',encoding='utf-8') as log:
        result=subprocess.run([str(a) for a in args],cwd=repo,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
    if result.returncode:raise RuntimeError(f'{stage} exited {result.returncode}')

try:
    for stage,script in [('blender_build','build_grilles_blender.py'),('blender_reimport','reimport_validate.py'),('source','finalize_source.py')]:
        run(stage,[blender,'-b','-t','8','-noaudio','--python-exit-code','1','--python',base/'Tools'/script])
        if stage=='blender_reimport':
            report=json.loads((verification/'blender_reimport.json').read_text(encoding='utf-8'))
            if report['status']!='BLENDER_REIMPORT_PASS':raise RuntimeError(str(report['errors']))
        if stage=='source':
            report=json.loads((verification/'source_scene_validation.json').read_text(encoding='utf-8'))
            if report['status']!='SOURCE_AND_SWEEP_PASS':raise RuntimeError(str(report['errors']))
    run('unity_build',[unity,'-batchmode','-quit','-projectPath',base/'UnityProject','-executeMethod','WindowGrillesBuilder.Build',
                       '-logFile',verification/'pipeline_unity_editor.log'])
    status('complete')
except Exception as error:
    status('failed',error=str(error))
    raise
