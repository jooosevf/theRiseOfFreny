# T-1 "O Salão": the hub of the test dungeon. The way out is behind the gate on the left, which
# needs the levers of T-2 and T-3; the door on the right leads on, and the one in the middle is the
# shortcut that opens from T-3.
from levelkit import Grid
W,H=70,24
OX,OY=-35,-12
SCENE_DIR='Assets/Scenes/Campaign'
TINT=(0.62,0.9,0.86)
NO_EXIT=True
g=Grid(W,H)
g.carve(1,7,68,18)
g.fill(14,10,15,18)    # the wall over the gate
g.fill(50,7,54,8)      # a block to climb over
solid,air=g.finish()
ENT=[
 ('player',22,7),
 ('checkpoint',44,7),
 ('save',26,7),
 ('door',4,7,'f','T-4','f'),
 ('door',32,7,'s','T-3','s'),
 ('door',64,7,'a','T-2','a'),
 ('gate',14,7,'G1','L1+L2'),
 ('torch',8,7),('torch',20,7),('torch',36,7),('torch',48,7),('torch',60,7),
 ('walltorch',28,12),('walltorch',56,12),
 ('coin',38,8),('coin',39,8),('coin',52,10),
 ('vase',29,7,'v1'),('vase',58,7,'v2'),
 ('bug',40,7,34,42),
]
