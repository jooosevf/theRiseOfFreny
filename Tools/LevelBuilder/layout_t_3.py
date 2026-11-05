# T-3 "O Fundo": a save point on arrival, a golem and a sorcerer guarding the second lever, and the
# shortcut back to the hall behind a gate that the same lever opens.
from levelkit import Grid
W,H=70,24
OX,OY=-35,-12
SCENE_DIR='Assets/Scenes/Campaign'
TINT=(0.62,0.9,0.86)
NO_EXIT=True
g=Grid(W,H)
g.carve(1,8,68,19)
g.fill(12,11,13,19)    # the wall over the gate
g.fill(38,8,41,9)      # a block to climb over
solid,air=g.finish()
ENT=[
 ('player',64,8),
 ('checkpoint',46,8),
 ('save',60,8),
 ('door',64,8,'b','T-2','b'),
 ('door',5,8,'s','T-1','s'),
 ('gate',12,8,'G2','L2'),
 ('lever',17,8,'L2'),
 ('torch',8,8),('torch',20,8),('torch',34,8),('torch',50,8),('torch',62,8),
 ('walltorch',26,13),('walltorch',44,14),
 ('coin',39,11),('coin',40,11),
 ('vase',54,8,'v1'),('vase',22,8,'v2'),
 ('golem',30,8,24,36),
 ('sorcerer',20,8,16,24),
]
