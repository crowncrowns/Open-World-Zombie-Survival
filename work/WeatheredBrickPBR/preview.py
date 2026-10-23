from PIL import Image
from pathlib import Path
p=Path('Assets/Maps/Buildings/Textures/WeatheredRedBrick_01')
a=Image.open(p/'WBR01_BaseColor.png').resize((700,700))
canvas=Image.new('RGB',(1400,1400))
for y in range(2):
 for x in range(2):canvas.paste(a,(700*x,700*y))
canvas.save('work/WeatheredBrickPBR/tiling-preview.jpg',quality=92)
