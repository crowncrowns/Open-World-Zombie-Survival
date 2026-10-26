from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
asset = root / 'UnityProject/Assets/Environment/Windows/MetalGrilles'
width, height = 440, 478
sheet = Image.new('RGB', (width * 5, height * 3), (25, 28, 30))
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 24)
names = ['Classic', 'Forged', 'Reinforced', 'Survivor', 'Hinged']
for row, state in enumerate('ABC'):
    for col, name in enumerate(names):
        picture = Image.open(asset / f'Verification/Grille_{col+1:02d}_{state}.png').convert('RGB')
        picture.thumbnail((width, width), Image.Resampling.LANCZOS)
        sheet.paste(picture, (col * width, row * height))
        draw.text((col * width + 12, row * height + width + 8), f'{col+1:02d} {name} / {state}', fill='white', font=font)
sheet.save(asset / 'Verification/ContactSheet.png')
sheet.save(root / 'Verification/ContactSheet.png')
print('CONTACT_SHEET_CREATED 15 Unity captures; no image adjustment')
