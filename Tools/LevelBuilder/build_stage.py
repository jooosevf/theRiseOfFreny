"""Builds a campaign stage scene from a layout module, using a MaxLevels scene as the base
(cameras, lights, player, savepoint, exit, tilemap objects). Run: build_stage.py layout_1_6 1-2 1-6"""
import sys,re,os,random,uuid,importlib
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from ruletile import RuleTile,matrix
REPO=os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..'))
rnd=random.Random(16)
L=importlib.import_module(sys.argv[1]); BASE=sys.argv[2]; OUT=sys.argv[3]
src=open(f'{REPO}/Assets/Scenes/MaxLevels/{BASE}.unity').read()
parts=re.split(r'(^--- !u!\d+ &-?\d+(?: stripped)?\n)',src,flags=re.M)
pre=parts[0]; docs=[[h,b] for h,b in zip(parts[1::2],parts[2::2])]
def cls(h): return int(re.match(r'--- !u!(\d+)',h).group(1))
def fid(h): return re.match(r'--- !u!\d+ &(-?\d+)',h).group(1)
byid={fid(h):i for i,(h,b) in enumerate(docs)}
def go_of(b):
    m=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',b); return m.group(1) if m else None
names={fid(h):re.search(r'm_Name: (.*)',b).group(1) for h,b in docs if cls(h)==1}
comps={}
for h,b in docs:
    g=go_of(b)
    if g and g!='0': comps.setdefault(g,[]).append(fid(h))
tr_of={}; father={}; children={}
for h,b in docs:
    if cls(h)==4 and 'stripped' not in h:
        g=go_of(b); tr_of[g]=fid(h)
        father[fid(h)]=re.search(r'm_Father: \{fileID: (-?\d+)\}',b).group(1)
        children[fid(h)]=re.findall(r'- \{fileID: (-?\d+)\}',b.split('m_Children:')[1].split('m_Father')[0])
go_of_tr={t:g for g,t in tr_of.items()}
# ---------- what to keep ----------
KEEP_ROOTS={'Main Camera','CM Follow Cam','cm','Global Light 2D','Global Volume','Player','Savepoint','LevelExit','World Boundary','Tilemap','Monitor','Scanlines'}
KEEP_TILEMAPS={'Ground_Tilemap','Background_Tilemap','Foreground_Tilemap'}
if getattr(L,'NO_EXIT',False): KEEP_ROOTS.discard('LevelExit')   # a room of the test dungeon, left through its doors
delete=set()   # doc ids
moved=set()
# the global light sits under a parent that gets deleted in some bases: move it to the scene root
for g,n in names.items():
    t=tr_of.get(g)
    if n=='Global Light 2D' and t and father[t]!='0':
        p=father[t]; moved.add(t); children[p]=[c for c in children[p] if c!=t]; father[t]='0'
        i=byid[t]; docs[i][1]=re.sub(r'm_Father: \{fileID: -?\d+\}','m_Father: {fileID: 0}',docs[i][1])
        break
def kill_go(g):
    for c in comps.get(g,[]):
        delete.add(c)
        if c in children:
            for ch in children[c]:
                if ch in go_of_tr: kill_go(go_of_tr[ch])
                else: delete.add(ch)
    delete.add(g)
for g,n in names.items():
    t=tr_of.get(g)
    if t is None: continue
    if father[t]=='0' and n not in KEEP_ROOTS: kill_go(g)
grid_go=[g for g,n in names.items() if n=='Tilemap' and father.get(tr_of.get(g))=='0'][0]
for ch in children[tr_of[grid_go]]:
    g=go_of_tr.get(ch)
    if g and names[g] not in KEEP_TILEMAPS: kill_go(g)
# every prefab instance goes (coins, torches, chests...), with its stripped objects
for h,b in docs:
    if cls(h)==1001: delete.add(fid(h))
for h,b in docs:
    if 'stripped' in h: delete.add(fid(h))
    m=re.search(r'm_PrefabInstance: \{fileID: (-?\d+)\}',b)
    if m and m.group(1) in delete: delete.add(fid(h))
docs=[d for d in docs if fid(d[0]) not in delete]
alive=set(fid(h) for h,b in docs)
def clean_children(b):
    if 'm_Children:' not in b: return b
    head,rest=b.split('m_Children:',1); kids,tail=rest.split('  m_Father',1)
    ks=[k for k in re.findall(r'- \{fileID: (-?\d+)\}',kids) if k in alive and k not in moved]
    kids=' []\n' if not ks else '\n'+''.join(f'  - {{fileID: {k}}}\n' for k in ks)
    return head+'m_Children:'+kids+'  m_Father'+tail
for d in docs:
    if cls(d[0])==4: d[1]=clean_children(d[1])
# ---------- helpers ----------
def doc_of_go(name,klass):
    g=[g for g,n in names.items() if n==name and g in alive][0]
    for d in docs:
        if cls(d[0])==klass and go_of(d[1])==g: return d
def set_pos(name,x,y):
    d=doc_of_go(name,4)
    d[1]=re.sub(r'm_LocalPosition: \{x: [-\d.e]+, y: [-\d.e]+,',f'm_LocalPosition: {{x: {x:.3f}, y: {y:.3f},',d[1])
def world(x,y): return x+L.OX, y+L.OY
# ---------- tilemaps ----------
rt=RuleTile(REPO+'/Assets/Sprites/Environment/Ground Rule Tile.asset')
RULE_GUID='4155d5ad04bc344429a808dcc06db62f'; TILESET_GUID='3d109a51476971d46b86abdc38c84075'
def base_cells(name):
    d=doc_of_go(name,1839735485); b=d[1]
    ta=re.findall(r'm_Data: \{fileID: (-?\d+)(?:, guid: (\w+))?',b.split('m_TileAssetArray:')[1].split('m_TileSpriteArray')[0])
    sa=re.findall(r'm_Data: \{fileID: (-?\d+)(?:, guid: (\w+))?',b.split('m_TileSpriteArray:')[1].split('m_TileMatrixArray')[0])
    cells=re.findall(r'- first: \{x: (-?\d+), y: (-?\d+), z: 0\}\n\s+second:\n\s+serializedVersion: 2\n\s+m_TileIndex: (\d+)\n\s+m_TileSpriteIndex: (\d+)',b)
    return {(int(x),int(y)):(ta[int(ti)],sa[int(si)]) for x,y,ti,si in cells}
def write_tilemap(name,cells,flags):
    """cells: {(x,y): (tileref, spriteref, matrix)} with refs as (fileID, guid, type)"""
    d=doc_of_go(name,1839735485); b=d[1]
    tiles=[];sprites=[];mats=[];tcount={};scount={};mcount={}
    def idx(lst,cnt,v):
        if v not in cnt: lst.append(v); cnt[v]=0
        cnt[v]+=1; return lst.index(v)
    rows=[]
    for (x,y) in sorted(cells,key=lambda c:(c[1],c[0])):
        t,s,m=cells[(x,y)]
        ti=idx(tiles,tcount,t); si=idx(sprites,scount,s); mi=idx(mats,mcount,m)
        rows.append(f'  - first: {{x: {x}, y: {y}, z: 0}}\n    second:\n      serializedVersion: 2\n      m_TileIndex: {ti}\n      m_TileSpriteIndex: {si}\n      m_TileMatrixIndex: {mi}\n      m_TileColorIndex: 0\n      m_TileObjectToInstantiateIndex: 65535\n      dummyAlignment: 0\n      m_AllTileFlags: {flags}\n')
    def ref(r): return f'{{fileID: {r[0]}, guid: {r[1]}, type: {r[2]}}}'
    out='  m_Tiles:\n'+''.join(rows) if rows else '  m_Tiles: {}\n'
    out+='  m_AnimatedTiles: {}\n  m_TileAssetArray:\n'+''.join(f'  - m_RefCount: {tcount[t]}\n    m_Data: {ref(t)}\n' for t in tiles)
    out+='  m_TileSpriteArray:\n'+''.join(f'  - m_RefCount: {scount[s]}\n    m_Data: {ref(s)}\n' for s in sprites)
    out+='  m_TileMatrixArray:\n'
    for m in mats:
        out+=f'  - m_RefCount: {mcount[m]}\n    m_Data:\n'+''.join(f'      e{i}{j}: {m[i][j]:g}\n' for i in range(4) for j in range(4))
    out+=f'  m_TileColorArray:\n  - m_RefCount: {len(cells)}\n    m_Data: {{r: 1, g: 1, b: 1, a: 1}}\n'
    if not cells: out=out.replace('  m_TileColorArray:\n  - m_RefCount: 0\n    m_Data: {r: 1, g: 1, b: 1, a: 1}\n','  m_TileColorArray: []\n').replace('m_TileAssetArray:\n','m_TileAssetArray: []\n').replace('m_TileSpriteArray:\n','m_TileSpriteArray: []\n').replace('m_TileMatrixArray:\n','m_TileMatrixArray: []\n')
    head=b.split('  m_Tiles:')[0]; tail=b.split('  m_TileObjectToInstantiateArray:')[1]
    if cells:
        xs=[c[0] for c in cells]; ys=[c[1] for c in cells]
        tail=re.sub(r'm_Origin: \{x: -?\d+, y: -?\d+, z: 0\}',f'm_Origin: {{x: {min(xs)}, y: {min(ys)}, z: 0}}',tail)
        tail=re.sub(r'm_Size: \{x: \d+, y: \d+, z: 1\}',f'm_Size: {{x: {max(xs)-min(xs)+1}, y: {max(ys)-min(ys)+1}, z: 1}}',tail)
    d[1]=head+out+'  m_TileObjectToInstantiateArray:'+tail
mat_key=lambda m: tuple(tuple(r) for r in m)
# ground
solid={(x+L.OX,y+L.OY) for x,y in L.solid}
# extend the walls a little past the stage so the edges look closed
ground={}
for c in solid:
    spr,(sx,sy,a)=rt.pick(c,lambda p:p in solid)
    ground[c]=(('11400000',RULE_GUID,2),(spr,TILESET_GUID,3),mat_key(matrix(sx,sy,a)))
write_tilemap('Ground_Tilemap',ground,1073741826)
# background: repeat a fully painted patch of the base stage's bricks behind every open cell
bg_src=base_cells('Background_Tilemap')
best=None
for (pw,ph) in ((16,10),(12,8),(10,6),(8,4)):
    for (x0,y0) in sorted(bg_src):
        if all((x0+i,y0+j) in bg_src for i in range(pw) for j in range(ph)):
            best=(x0,y0,pw,ph); break
    if best: break
bx,by,pw,ph=best
ident=mat_key(matrix(1,1,0))
bg={}
air={(x+L.OX,y+L.OY) for x,y in L.air}
for (x,y) in air|{(x+dx,y+dy) for x,y in air for dx in (-1,0,1) for dy in (-1,0,1)}:
    (t,s)=bg_src[(bx+(x%pw),by+(y%ph))]
    bg[(x,y)]=((t[0],t[1],2),(s[0],s[1],3),ident)
write_tilemap('Background_Tilemap',bg,1073741825)
write_tilemap('Foreground_Tilemap',{},1073741825)
tint=getattr(L,'TINT',None)
if tint:
    for n in ('Ground_Tilemap','Background_Tilemap'):
        d=doc_of_go(n,1839735485)
        d[1]=re.sub(r'  m_Color: \{r: [\d.]+, g: [\d.]+, b: [\d.]+, a: [\d.]+\}\n  m_Origin',f'  m_Color: {{r: {tint[0]}, g: {tint[1]}, b: {tint[2]}, a: 1}}\n  m_Origin',d[1])
for d in docs:   # the composite collider is rebuilt from the tiles
    if cls(d[0])==66:
        b=d[1]
        b=re.sub(r'  m_ColliderPaths:\n.*?(?=  m_CompositePaths:)','  m_ColliderPaths: []\n',b,flags=re.S)
        b=re.sub(r'  m_CompositePaths:\n.*?(?=  m_VertexDistance:)','  m_CompositePaths:\n    m_Paths: []\n',b,flags=re.S)
        d[1]=b
# ---------- fixed objects ----------
ents=L.ENT
def ent(kind): return [e for e in ents if e[0]==kind]
px,py=world(*ent('player')[0][1:3]); set_pos('Player',px+0.5,py+1.02); set_pos('CM Follow Cam',px+0.5,py+2)
cx,cy=world(*ent('checkpoint')[0][1:3]); set_pos('Savepoint',cx+0.5,cy+0.5)
if not getattr(L,'NO_EXIT',False):
    ex,ey=world(*ent('exit')[0][1:3])
    press=getattr(L,'EXIT_PRESS_UP',False); boss=getattr(L,'BOSS','')
    set_pos('LevelExit',ex+(0.5 if press else 0.3),ey+1.2)
    door=[d for d in docs if cls(d[0])==114 and go_of(d[1])==[g for g,n in names.items() if n=='LevelExit' and g in alive][0]][0]
    door[1]=re.sub(r'  pressUp: \d\n.*',f'  pressUp: {1 if press else 0}\n  bossName: {boss}\n',door[1],flags=re.S)
xs=[c[0] for c in solid]; ys=[c[1] for c in solid]
wb=doc_of_go('World Boundary',60)
wb[1]=re.sub(r'm_Paths:\n(    - - .*\n(      - .*\n)*)',f'm_Paths:\n    - - {{x: {min(xs)}, y: {max(ys)+1}}}\n      - {{x: {min(xs)}, y: {min(ys)}}}\n      - {{x: {max(xs)+1}, y: {min(ys)}}}\n      - {{x: {max(xs)+1}, y: {max(ys)+1}}}\n',wb[1])
# ---------- new objects ----------
used=set(fid(h) for h,b in docs)
def newid():
    while True:
        n=str(rnd.randint(10**8,2*10**9))
        if n not in used: used.add(n); return n
new=[]
def point(name,x,y):
    g,t=newid(),newid()
    new.append([f'--- !u!1 &{g}\n',f'GameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n  - component: {{fileID: {t}}}\n  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n'])
    new.append([f'--- !u!4 &{t}\n',f'Transform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {g}}}\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: {x:.3f}, y: {y:.3f}, z: 0}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_Children: []\n  m_Father: {{fileID: 0}}\n  m_RootOrder: 0\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n'])
    return t
PREFABS={
 'bug':('60d87c22096e25a46afea541c8d6f3e2','8274441830027215983','6101331372153397134','8871542281216455844'),
 'golem':('49496e0f74af48345867a62b81c12758','6496106852740584793','5877934722757971472','1568797996859213525'),
 'licker':('c46c49e424834b74baa7cd1d50995add','332949994701094415','2064545646695465190','3840879670598176171'),
 'sorcerer':('20e5ccc9f5a154c48af6bbaec4f33329','4902039112802150574','6741818021595377839','1835722495582547347'),
 'coin':('71247d32d6f48964d842d8570fb2e767','1839722716228608488','1839722716228608493',None),
 'chest':('b275f40a18a7d8442999d5655b1d037a','840620173825923865','6149968991823573760',None),
 'torch':('a49ca141e7943584390fcad0b1ccc4b2','2638542340515973895','2638542340515973894',None),
 'walltorch':('d96bb00201465194f9850d8ff78ac9fb','4869257856637464572','4869257856637464573',None),
 'bat':('9bbb006b562745c0a0aa7aa53edcac30','8274441830027215983','6101331372153397134','8871542281216455844'),
 'supreme':('1937232610ea4673b7e52f0b1ecd8845','4902039112802150574','6741818021595377839','1835722495582547347'),
}
def instance(kind,x,y,name,extra=()):
    guid,root_go,root_tr,mb=PREFABS[kind]
    mods=[(root_go,'m_Name',name,'0'),(root_tr,'m_LocalPosition.x',f'{x:.3f}','0'),(root_tr,'m_LocalPosition.y',f'{y:.3f}','0'),(root_tr,'m_LocalPosition.z','0','0')]
    mods+=[(mb,p,v,r) for p,v,r in extra]
    body='PrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    m_TransformParent: {fileID: 0}\n    m_Modifications:\n'
    for t,p,v,r in mods:
        body+=f'    - target: {{fileID: {t}, guid: {guid}, type: 3}}\n      propertyPath: {p}\n      value: {v}\n      objectReference: {{fileID: {r}}}\n'
    body+=f'    m_RemovedComponents: []\n  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}\n'
    new.append([f'--- !u!1001 &{newid()}\n',body])
# height of the pivot above the floor: for enemies, the bottom of the body collider (below the
# pivot in the prefab) plus a little, so they don't start inside the ground and fall through it
OFFSET={'torch':1.15,'walltorch':1.5,'coin':0.5,'chest':0.5,'bat':0.5,'bug':0.6,'golem':0.85,'licker':1.06,'sorcerer':1.06,'supreme':1.06}
counts={}
# the test dungeon's objects are named empties that WorldMarkers.cs turns into the real thing;
# where each stands above its cell
WORLD={'door':(0.5,1.2),'save':(0.5,1.0),'lever':(0.5,0.8),'gate':(0.5,1.5),'vase':(0.5,0.5),'item':(0.5,0.6)}
for e in ents:
    k=e[0]
    if k in ('player','exit','checkpoint'): continue
    if k in WORLD:
        wx,wy=world(e[1],e[2]); dx,dy=WORLD[k]
        point('World|'+'|'.join([k]+[str(a) for a in e[3:]]),wx+dx,wy+dy)
        continue
    counts[k]=counts.get(k,0)+1
    x,y=world(e[1],e[2])
    label={'bat':'Bat_Enemy','supreme':'Sorcerer_Supreme','bug':'Bug_Enemy','golem':'Golem_Enemy','licker':'Licker_Enemy','sorcerer':'Sorcerer_Enemy','coin':'Coin','chest':'Chest','torch':'Standing Torch Point Light 2D','walltorch':'Torch Point Light 2D'}[k]
    name=f'{label} ({counts[k]})'
    if len(e)==5:   # enemy with a patrol
        ax,_=world(e[3],e[2]); bx_,_=world(e[4],e[2])
        h=OFFSET.get(k,0.6)
        pa=point(f'PointA {name}',ax+0.5,y+h); pb=point(f'PointB {name}',bx_+0.5,y+h)
        instance(k,x+0.5,y+h,name,[('pointA','',pa),('pointB','',pb),('spawnerUuid','','0')])
    else:
        instance(k,x+0.5,y+OFFSET.get(k,0.5),name)
# enemy patrol refs: value empty + objectReference; spawnerUuid is a string, not a reference
fixed=[]
for h,b in new:
    b=re.sub(r'propertyPath: spawnerUuid\n      value: 0\n      objectReference: \{fileID: 0\}','propertyPath: spawnerUuid\n      value: \n      objectReference: {fileID: 0}',b)
    fixed.append([h,b])
docs+=fixed
out=pre+''.join(h+b for h,b in docs)
scene_dir=getattr(L,'SCENE_DIR','Assets/Scenes/MaxLevels')
os.makedirs(f'{REPO}/{scene_dir}',exist_ok=True)
path=f'{REPO}/{scene_dir}/{OUT}.unity'
open(path,'w').write(out)
if not os.path.exists(path+'.meta'):
    open(path+'.meta','w').write(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print('wrote',path,len(out)//1024,'KB; ground',len(ground),'bg',len(bg),'new docs',len(new))
