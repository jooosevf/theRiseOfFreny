"""Helpers for writing stage layouts and checking that a stage can be finished.

A layout starts all solid; carve() opens rooms and fill() puts blocks back. finish() returns the
module-level values build_stage.py reads (W, H, solid, air). Coordinates: x right, y up; an entity
at (x, y) stands on row y (its floor is row y-1)."""

class Grid:
    def __init__(self, w, h):
        self.W, self.H = w, h
        self.air = set()
        self.holes = set()   # cells on the border left open on purpose (entrance, exit, pits)

    def carve(self, x0, y0, x1, y1):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                if 0 <= x < self.W and 0 <= y < self.H:
                    self.air.add((x, y))

    def fill(self, x0, y0, x1, y1):
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                self.air.discard((x, y))

    def pit(self, x0, x1, top):
        """a bottomless pit from row `top` down through the bottom edge"""
        self.carve(x0, 0, x1, top)
        for x in range(x0, x1 + 1):
            self.holes.add((x, 0))

    def open_edge(self, cells):
        self.holes |= set(cells)

    def solid_cells(self):
        solid = set()
        for x in range(self.W):
            for y in range(self.H):
                edge = x in (0, self.W - 1) or y in (0, self.H - 1)
                if (x, y) in self.air and not (edge and (x, y) not in self.holes):
                    continue
                if (x, y) in self.holes:
                    continue
                solid.add((x, y))
        return solid

    def finish(self):
        solid = self.solid_cells()
        air = set((x, y) for x in range(self.W) for y in range(self.H) if (x, y) not in solid)
        return solid, air


# ---- reachability ------------------------------------------------------------------------------
# Freny: about 2.6 tiles of jump, short in the air (fast fall). Conservative reach at walking speed:
REACH = {2: 2, 1: 3, 0: 3}   # rise -> max columns travelled (dx); a gap of 2 empty cells is dx 3
TALL = 2

def standable(solid, W, H, x, y):
    return 0 <= x < W and 0 <= y < H and (x, y - 1) in solid and all((x, y + k) not in solid for k in range(TALL))

def clear(solid, x, y0, y1):
    return all((x, y) not in solid for y in range(min(y0, y1), max(y0, y1) + 1))

def moves(solid, W, H, x, y):
    for dx in range(-6, 7):
        for dy in range(-30, 3):
            if dx == 0 and dy == 0:
                continue
            nx, ny = x + dx, y + dy
            if not standable(solid, W, H, nx, ny):
                continue
            adx = abs(dx)
            if dy > 0:
                if adx > REACH[dy]:
                    continue
            else:
                fall = -dy
                if adx > 3 + min(3, fall // 2):
                    continue
            step = 1 if dx > 0 else -1
            if dy >= 0:
                top = ny + TALL - 1 + (1 if adx > 1 else 0)
                # she moves while rising: the start column only needs a hop of headroom
                if not clear(solid, x, y, min(top, y + TALL)):
                    continue
                if adx and not all(clear(solid, x + step * i, top - TALL + 1, top) for i in range(1, adx)):
                    continue
                if not clear(solid, nx, ny, top):
                    continue
            else:
                # walk or hop off the edge, then fall
                if adx and not all(clear(solid, x + step * i, y, y + TALL - 1) for i in range(1, adx)):
                    continue
                if not clear(solid, nx, ny, y + TALL - 1):
                    continue
            yield nx, ny

def reachable(solid, W, H, start):
    sx, sy = start
    while not standable(solid, W, H, sx, sy) and sy > 0:
        sy -= 1
    seen = {(sx, sy)}
    todo = [(sx, sy)]
    while todo:
        x, y = todo.pop()
        for n in moves(solid, W, H, x, y):
            if n not in seen:
                seen.add(n)
                todo.append(n)
    return seen

def check(mod):
    """prints problems: exit unreachable, entities floating or out of reach"""
    solid, W, H = mod.solid, mod.W, mod.H
    start = [e for e in mod.ENT if e[0] == 'player'][0][1:3]
    seen = reachable(solid, W, H, start)
    ok = True
    for e in mod.ENT:
        kind, x, y = e[0], e[1], e[2]
        if kind in ('player', 'bat', 'walltorch', 'coin'):
            continue
        if kind == 'exit':
            if not any(abs(sx - x) <= 1 and abs(sy - y) <= 2 for sx, sy in seen):
                print('  EXIT NOT REACHABLE', (x, y)); ok = False
            continue
        if not standable(solid, W, H, x, y):
            print('  not on a floor:', e); ok = False
        elif kind in ('checkpoint', 'chest', 'door', 'lever', 'save', 'item') and (x, y) not in seen:
            print('  unreachable:', e); ok = False
        if len(e) == 5 and kind != 'gate':
            for px in range(min(e[3], e[4]), max(e[3], e[4]) + 1):
                if not standable(solid, W, H, px, y):
                    print('  patrol leaves the floor:', e, 'at', px); ok = False; break
    coins = [e for e in mod.ENT if e[0] == 'coin']
    far = [c for c in coins if not any(abs(sx - c[1]) <= 1 and 0 <= c[2] - sy <= 3 for sx, sy in seen)]
    if far:
        print('  coins out of reach:', far)
    return ok, seen

def ascii(mod, seen=()):
    ent = {(e[1], e[2]): e[0][0].upper() if e[0] != 'walltorch' else 'w' for e in mod.ENT}
    rows = []
    for y in range(mod.H - 1, -1, -1):
        row = ''
        for x in range(mod.W):
            row += ent.get((x, y), '#' if (x, y) in mod.solid else (':' if (x, y) in seen else '.'))
        rows.append('%3d %s' % (y, row))
    return '\n'.join(rows)


def zigzag(g, x_left, x_right, y_from, y_to, width=4, step=4, start_t=0):
    """ledges every 2 rows from row y_from up to y_to, bouncing between x_left and x_right;
    each ledge touches the next one (no overlap, or the one above blocks the jump). Returns the ledges
    as (x0, x1, row)."""
    tmax = max(1, (x_right - x_left - width + 1) // step)
    t, d = start_t, 1
    ledges = []
    for row in range(y_from, y_to + 1, 2):
        x0 = x_left + t * step
        g.fill(x0, row, x0 + width - 1, row)
        ledges.append((x0, x0 + width - 1, row))
        if t + d > tmax or t + d < 0:
            d = -d
        t += d
    return ledges


def hole_over(g, ledges, floor_row):
    """opens a 2-wide hole in the floor above the last ledge of a zigzag, on the side the climber
    lands from, so the last hop goes through it"""
    (a0, a1, _), (b0, b1, _) = ledges[-2], ledges[-1]
    x = b0 if a1 < b0 else b1 - 1
    g.carve(x, floor_row, x + 1, floor_row)
