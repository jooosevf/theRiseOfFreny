# Level builder (campaign stages without opening Unity)

Python 3 + Pillow.

- `render.py <scene.unity> <out.png>`: draws a scene's tilemaps with markers for the start,
  enemies, exits, savepoints, coins and chests (env `PX` = pixels per tile, `BRIGHT` = brightness).
- `build_stage.py <layout module> <base stage> <new stage>`: builds
  `Assets/Scenes/MaxLevels/<new stage>.unity` from a layout (see `layout_1_6.py`), keeping the
  base stage's cameras, lights, player, savepoint, exit and tilemap objects. The ground is painted
  with `Ground Rule Tile` exactly as Unity would (`ruletile.py` reproduces its rules), the
  background repeats a patch of the base stage's bricks, and enemies, coins, chests and torches
  are prefab instances. A new stage also needs a line in `CampaignLevels.cs` and an entry in
  the Build Settings.
- `check_layout.py <layout module> [--map]`: checks that the exit, checkpoint and chest can be
  reached with Freny's jump and that enemies stand on a floor; `--map` prints the layout with the
  reachable cells marked `:`. Run it before building.
- `levelkit.py`: `Grid` (carve, fill, pit, open_edge), `zigzag` (climbs), `hole_over`, and the
  reachability check. Layout options: `TINT` (tilemap colour per dungeon), `SCENE_DIR`,
  `EXIT_PRESS_UP` and `BOSS` (the exit stays shut while that enemy lives).
- `openings.py <scene.unity>`: lists the openings at the edges of a stage (where exits go).
- Test dungeon rooms (`layout_t_*.py`, built on `1-2`): `NO_EXIT` leaves out the level exit, and these
  entities become named empties that `WorldMarkers.cs` turns into the real thing: `door`
  (id, scene, door there), `save`, `lever` (id), `gate` (id, levers joined with `+`), `vase` (id) and
  `item` (id, kind, amount). Ids must stay stable: saves remember them.

Layout rules of thumb (Freny's jump is about 2.6 tiles high and short): steps and ledges at most
2 tiles up, gaps at most 2 tiles wide, and an entity at (x, y) stands on row y (floor is row y-1).

Example:

    cd Tools/LevelBuilder
    python3 check_layout.py layout_2_1 --map
    python3 build_stage.py layout_2_1 1-2 2-1
    python3 render.py ../../Assets/Scenes/Campaign/2-1.unity /tmp/2-1.png
