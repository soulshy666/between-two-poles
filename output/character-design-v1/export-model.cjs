// Export the same mesh used in the interactive design review. No Unity assets
// or scenes are changed. Run with the bundled Node runtime.
const fs=require('fs');
const path=require('path');
const vm=require('vm');
const source=process.argv[2];
if(!source)throw new Error('Pass the absolute astronaut-design.html path.');
const html=fs.readFileSync(source,'utf8');
const code=html.match(/<script>([\s\S]*?)<\/script>/)[1];
const model=vm.runInNewContext(code+'\nAstronautDesign;');
const obj=['# Between Two Poles astronaut design v1; Y up, +Z front; metres','mtllib astronaut-v1.mtl'];
let vertex=0,triangles=0;
for(const part of model.parts){
  obj.push('o '+part.name,'usemtl '+part.material,'s off');
  for(const polygon of part.polygons){
    for(const p of polygon)obj.push('v '+p.map(v=>v.toFixed(6)).join(' '));
    for(let i=1;i+1<polygon.length;i++){obj.push(`f ${vertex+1} ${vertex+i+1} ${vertex+i+2}`);triangles++;}
    vertex+=polygon.length;
  }
}
const mtl=['# Flat colour materials, no texture dependencies'];
for(const [name,color] of Object.entries(model.palette)){
  const rgb=color.slice(1).match(/../g).map(v=>(parseInt(v,16)/255).toFixed(6)).join(' ');
  mtl.push('newmtl '+name,'Kd '+rgb,'Ka '+rgb,'Ks 0.025 0.025 0.025','Ns 8','d 1','illum 2','');
}
fs.writeFileSync(path.join(__dirname,'astronaut-v1.obj'),obj.join('\n')+'\n');
fs.writeFileSync(path.join(__dirname,'astronaut-v1.mtl'),mtl.join('\n'));
const points=model.parts.flatMap(p=>p.polygons.flat());
const bounds=[0,1,2].map(i=>[Math.min(...points.map(p=>p[i])),Math.max(...points.map(p=>p[i]))]);
console.log(JSON.stringify({parts:model.parts.length,triangles,vertices:vertex,bounds,output:__dirname},null,2));
