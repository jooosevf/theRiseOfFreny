# T-2 "A Galeria": a corridor with two pits, the first lever up on the blocks and a potion to carry.
from levelkit import Grid
W,H=80,24
OX,OY=-40,-12
SCENE_DIR='Assets/Scenes/Campaign'
TINT=(0.62,0.9,0.86)
NO_EXIT=True
g=Grid(W,H)
g.carve(1,9,78,20)
g.pit(22,23,8)
g.pit(66,67,8)
g.fill(48,9,52,10)     # floor 11
g.fill(54,9,58,12)     # floor 13, the lever
solid,air=g.finish()
ENT=[
 ('player',4,9),
 ('checkpoint',36,9),
 ('door',4,9,'a','T-1','a'),
 ('door',75,9,'b','T-3','b'),
 ('lever',57,13,'L1'),
 ('item',71,9,'p1','Potion',1),
 ('torch',10,9),('torch',30,9),('torch',44,9),('torch',62,9),('torch',72,9),
 ('walltorch',18,14),('walltorch',52,17),
 ('coin',23,12),('coin',55,15),('coin',56,15),
 ('vase',16,9,'v1'),('vase',42,9,'v2'),
 ('licker',30,9,26,35),
 ('bat',46,15),
 ('bug',62,9,60,65),
]
