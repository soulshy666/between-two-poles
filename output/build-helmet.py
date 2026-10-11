exec(open('output/inspect-helmet.py',encoding='utf-8-sig').read().split('print(len(blocks))')[0])
import math
backup=pathlib.Path('output/AstronautBody-before-helmet.asset')
if not backup.exists(): backup.write_text(s,encoding='utf-8')
sub=[]
for b in blocks[:-1]:
 first=int(re.search(r'firstByte: (\d+)',b)[1]); count=int(re.search(r'indexCount: (\d+)',b)[1]); sub.append(list(struct.unpack_from('<'+'H'*count,ib,first)))
# Closed, opaque octagonal lens behind the existing rim and reflection accents.
outline=[(-.16,.812),(.16,.812),(.181,.831),(.181,1.024),(.16,1.044),(-.16,1.044),(-.181,1.024),(-.181,.831)]
inner=[(x*.96,.928+(y-.928)*.95) for x,y in outline]
def vertex(x,y,z,normal):
 idx=len(verts)//48
 verts.extend(struct.pack('<12f',x,y,z,*normal,1,1,1,1,(x+.19)/.38,(y-.8)/.25))
 return idx
def tri(a,b,c):
 u=[b[k]-a[k] for k in range(3)];v=[c[k]-a[k] for k in range(3)]
 n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];length=math.sqrt(sum(x*x for x in n));n=[x/length for x in n]
 sub[8].extend(vertex(*p,n) for p in (a,b,c))
for i in range(8):
 j=(i+1)%8
 a=(*outline[i],.224);b=(*outline[j],.224);c=(*inner[j],.237);d=(*inner[i],.237)
 tri(a,b,c);tri(a,c,d);tri((0,.928,.237),d,c)
# Compact to referenced vertices: the brain's 3,800 vertices are removed, not hidden.
used=sorted(set(k for ids in sub for k in ids));remap={old:new for new,old in enumerate(used)}
newverts=b''.join(verts[k*48:(k+1)*48] for k in used);newib=bytearray();descriptors=[]
for old,ids in zip(blocks,sub):
 ids=[remap[k] for k in ids];offset=len(newib);newib.extend(struct.pack('<'+'H'*len(ids),*ids))
 b=re.sub(r'firstByte: \d+',f'firstByte: {offset}',old)
 b=re.sub(r'indexCount: \d+',f'indexCount: {len(ids)}',b)
 b=re.sub(r'firstVertex: \d+',f'firstVertex: {min(ids)}',b)
 b=re.sub(r'vertexCount: \d+',f'vertexCount: {max(ids)-min(ids)+1}',b)
 xyz=[struct.unpack_from('<3f',newverts,k*48) for k in ids];lo=[min(v[k] for v in xyz) for k in range(3)];hi=[max(v[k] for v in xyz) for k in range(3)]
 def vec(values): return '{x: %.9g, y: %.9g, z: %.9g}'%tuple(values)
 b=re.sub(r'm_Center: \{[^}]+\}', 'm_Center: '+vec([(a+b)/2 for a,b in zip(lo,hi)]),b)
 b=re.sub(r'm_Extent: \{[^}]+\}', 'm_Extent: '+vec([(b-a)/2 for a,b in zip(lo,hi)]),b);descriptors.append(b)
s=s.replace(''.join(blocks),''.join(descriptors))
s=re.sub(r'm_IndexBuffer: [0-9a-f]+','m_IndexBuffer: '+newib.hex(),s)
s=re.sub(r'_typelessdata: [0-9a-f]+','_typelessdata: '+newverts.hex(),s)
s=re.sub(r'm_VertexCount: \d+',f'm_VertexCount: {len(used)}',s)
s=re.sub(r'm_DataSize: \d+',f'm_DataSize: {len(newverts)}',s)
p.write_text(s,encoding='utf-8')
prefab=pathlib.Path('Assets/BetweenPoles/Art/AstronautV1/AstronautVisual.prefab');ps=prefab.read_text(encoding='utf-8-sig')
ps,n=re.subn(r'  - \{fileID: 2100000, guid: e149f1dc924e04648ade5aca20e51bdd, type: 2\}\n','',ps)
assert n==1
prefab.write_text(ps,encoding='utf-8')
assert len(newverts)==len(used)*48 and max(remap.values())<65536
print('PASS: removed brain submesh/material reference, compacted mesh to',len(used),'vertices; added opaque beveled visor; retained mesh identity and body bounds.')
