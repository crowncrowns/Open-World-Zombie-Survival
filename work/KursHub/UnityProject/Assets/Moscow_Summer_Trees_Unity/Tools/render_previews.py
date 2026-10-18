import vtk, sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import io
root=Path(__file__).resolve().parent.parent
names={'Birch':'БЕРЁЗА','Oak':'ДУБ','Pine':'СОСНА','Spruce':'ЕЛЬ','Linden':'ЛИПА','Maple':'КЛЁН','Aspen':'ОСИНА','Rowan':'РЯБИНА'}
variants=['TallMature','Young','Weathered','Dense']
var_labels=['Взрослое','Молодое','Старое','Густое']
font='/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'
if not Path(font).exists():font='/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf'
heading=ImageFont.truetype(font,23);small=ImageFont.truetype(font,14)

def draw_model(name, variant, w=300,h=350):
 path=root/'Models'/'LOD0'/f'{name}_{variant}_LOD0.glb'
 ren=vtk.vtkRenderer();ren.SetBackground(.92,.95,.96)
 win=vtk.vtkRenderWindow();win.SetOffScreenRendering(1);win.AddRenderer(ren);win.SetSize(w,h)
 win.SetMultiSamples(0)
 importer=vtk.vtkGLTFImporter();importer.SetFileName(str(path));importer.SetRenderWindow(win);importer.Update()
 ren.ResetCamera();bounds=ren.ComputeVisiblePropBounds()
 center=((bounds[0]+bounds[1])*.5,(bounds[2]+bounds[3])*.5,(bounds[4]+bounds[5])*.5)
 height=bounds[3]-bounds[2];rad=max(bounds[1]-bounds[0],bounds[5]-bounds[4])
 camera=ren.GetActiveCamera();camera.ParallelProjectionOn()
 camera.SetFocalPoint(*center);camera.SetPosition(center[0]+rad*1.5,center[1]+height*.4,center[2]+rad*2.2);camera.SetViewUp(0,1,0)
 camera.SetParallelScale(max(height*.63,rad*.85))
 ren.ResetCameraClippingRange();win.Render()
 w2i=vtk.vtkWindowToImageFilter();w2i.SetInput(win);w2i.SetInputBufferTypeToRGB();w2i.ReadFrontBufferOff();w2i.Update()
 writer=vtk.vtkPNGWriter();writer.SetWriteToMemory(True);writer.SetInputConnection(w2i.GetOutputPort());writer.Write()
 result=Image.open(io.BytesIO(memoryview(writer.GetResult()).tobytes())).convert('RGB')
 win.Finalize()
 return result

sheet=Image.new('RGB',(4*320,8*410),(238,243,242));draw=ImageDraw.Draw(sheet)
for i,(name,ru) in enumerate(names.items()):
 for j,variant in enumerate(variants):
  cell=draw_model(name,variant,300,355)
  x=j*320+10;y=i*410+10
  sheet.paste(cell,(x,y))
  draw.text((x+12,y+355),ru,fill=(31,55,39),font=heading)
  draw.text((x+12,y+382),var_labels[j],fill=(90,96,93),font=small)
  print(name,variant,flush=True)
sheet.save(root/'Preview'/'All_32_Trees_Actual_3D.png',optimize=True)
# compact 8 species preview for easy browsing
small_sheet=Image.new('RGB',(4*320,2*410),(238,243,242))
for i,(name,ru) in enumerate(names.items()):
 x=(i%4)*320+10;y=(i//4)*410+10
 crop=sheet.crop((x,y,x+300,y+408))
 small_sheet.paste(crop,(x,y))
small_sheet.save(root/'Preview'/'Eight_Species_Actual_3D.png',optimize=True)
print('Previews saved')
