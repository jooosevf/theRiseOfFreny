# T-4 "A Saída": the last fight of the test dungeon and the door out.
from levelkit import Grid
W,H=50,22
OX,OY=-25,-11
SCENE_DIR='Assets/Scenes/Campaign'
TINT=(0.62,0.9,0.86)
EXIT_PRESS_UP=True
g=Grid(W,H)
g.carve(1,7,48,17)
g.fill(24,7,27,8)      # a block to climb over
solid,air=g.finish()
ENT=[
 ('player',4,7),
 ('checkpoint',16,7),
 ('door',4,7,'f','T-1','f'),
 ('exit',45,7),
 ('torch',10,7),('torch',22,7),('torch',32,7),('torch',42,7),
 ('walltorch',18,12),('walltorch',36,12),
 ('vase',12,7,'v1'),
 ('golem',36,7,30,42),
 ('sorcerer',40,7,34,44),
]
