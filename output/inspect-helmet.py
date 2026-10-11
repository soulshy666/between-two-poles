import re,struct,pathlib,json
p=pathlib.Path('Assets/BetweenPoles/Art/AstronautV1/Meshes/AstronautBody.asset')
s=p.read_text(encoding='utf-8-sig')
verts=bytearray.fromhex(re.search(r'_typelessdata: ([0-9a-f]+)',s)[1])
ib=bytes.fromhex(re.search(r'm_IndexBuffer: ([0-9a-f]+)',s)[1])
blocks=re.findall(r'  - serializedVersion: 2\n    firstByte:.*?(?=  - serializedVersion: 2|  m_Shapes:)',s,re.S)
print(len(blocks))
for i,b in enumerate(blocks):
 first=int(re.search(r'firstByte: (\d+)',b)[1]); count=int(re.search(r'indexCount: (\d+)',b)[1]); ids=struct.unpack_from('<'+'H'*count,ib,first)
 xyz=[struct.unpack_from('<3f',verts,j*48) for j in ids]
 print(i,count,[(min(v[k] for v in xyz),max(v[k] for v in xyz)) for k in range(3)])
