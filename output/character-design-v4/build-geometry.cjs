// Keep the approved V3 brain and suit; reuse the previously authored sealed pack.
const fs=require('fs'),path=require('path');
const read=p=>JSON.parse(fs.readFileSync(path.join(__dirname,p),'utf8'));
const original=read('../character-design-v3/unity-geometry.json');
const packVersion=read('../character-design-v2/backup-before-revert-v1/unity-geometry.json');
const isCase=p=>p.name.startsWith('Case_')||p.name.startsWith('Sealed_case');
const oldPack=p=>p.name.startsWith('Life_support_')||p.name.startsWith('Pack_');
const kept=original.parts.filter(p=>!isCase(p)&&!oldPack(p));
const pack=packVersion.parts.filter(isCase);
if(pack.length!==17)throw new Error('Unexpected backpack source: '+pack.length);
for(const p of pack)if(!original.palette[p.material])throw new Error('Missing material '+p.material);
const model={palette:original.palette,parts:[...kept,...pack]};
for(const p of original.parts.filter(p=>p.name.startsWith('Brain_'))){
  if(JSON.stringify(p)!==JSON.stringify(model.parts.find(q=>q.name===p.name)))throw new Error('Brain changed');
}
// Transport body is wholly behind the suit; only the two shoulder straps extend forward.
for(const p of pack.filter(p=>!p.name.startsWith('Case_shoulder_strap_')))
  if(p.polygons.flat().some(v=>v[2]>-.10))throw new Error('Pack intersects hand space: '+p.name);
fs.writeFileSync(path.join(__dirname,'unity-geometry.json'),JSON.stringify(model));
console.log(JSON.stringify({parts:model.parts.length,packParts:pack.length,brainUnchanged:true,
  triangles:model.parts.reduce((s,p)=>s+p.polygons.reduce((n,f)=>n+f.length-2,0),0)}));
