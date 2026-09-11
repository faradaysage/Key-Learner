from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
root = Path(__file__).resolve().parents[1] / 'Content' / 'Fonts'
for name, axes in [('Fredoka', {'wght': 650, 'wdth': 100}), ('BalooBhai2', {'wght': 650})]:
    font = TTFont(root / (name + '.ttf'))
    instance = instantiateVariableFont(font, axes, inplace=True)
    instance.save(root / (name + '-Play.ttf'))
