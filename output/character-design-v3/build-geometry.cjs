// Native low-poly geometry, no image textures or external generation service.
const fs=require('fs'),vm=require('vm'),path=require('path');
const original=fs.readFileSync(path.join(__dirname,'../character-design-v2/rollback-before-v2/astronaut-design.html'),'utf8');
const base=original.match(/<script>([\s\S]*?)<\/script>/)[1];
// Reuse the original bevel-box authoring helper; keep the suit and case exact.
const additions=`
  palette.brainFlesh='#CCA59C';
  for(let i=parts.length-1;i>=0;i--) if(['Visor','Visor_gasket','Visor_lower_reflection'].includes(parts[i].name))parts.splice(i,1);
  const shell=parts.find(p=>p.name==='Helmet_shell');
  // Remove only the front plane; preserve the first-version chamfered silhouette.
  shell.polygons=shell.polygons.filter(face=>!face.every(p=>Math.abs(p[2]-.208)<1e-5));
  box('Helmet_inner_rear',[0,.926,-.115],[.345,.247,.015],'visor',.006);
  box('Helmet_inner_floor',[0,.807,.04],[.344,.022,.310],'graphite',.007);
  for(const sign of [-1,1]) {
    box('Window_side_'+sign,[sign*.180,.926,.221],[.023,.226,.026],'rubber',.009);
    box('Window_inner_side_'+sign,[sign*.180,.926,.04],[.018,.217,.32],'visor',.005);
  }
  box('Window_top',[0,1.039,.221],[.374,.024,.026],'rubber',.010);
  box('Window_bottom',[0,.813,.221],[.374,.024,.026],'rubber',.010);
  box('Window_edge_glint',[.089,.831,.241],[.147,.008,.004],'glass',.002);
  box('Brain_support',[0,.832,.006],[.138,.014,.118],'graphite',.005);

  const normalize=v=>{const n=Math.hypot(...v);return v.map(x=>x/n);};
  const sphere=(t,p)=>[Math.sin(t)*Math.sin(p),Math.cos(t),Math.sin(t)*Math.cos(p)];
  const grooves=[];
  // Three broad winding sulci per hemisphere plus short branches. No painted lines.
  for(const sign of [-1,1]) {
    for(let row=0;row<3;row++) {
      const points=[];
      for(let j=0;j<=42;j++) {
        const p=.10+j/42*2.94;
        const t=.64+row*.63+.15*Math.sin(p*3.6+row*1.8+sign*.25)+.045*Math.sin(p*7);
        points.push(sphere(t,p*sign));
      }
      grooves.push(points);
    }
    for(let branch=0;branch<2;branch++) {
      const points=[];
      for(let j=0;j<=16;j++) {
        const t=.8+j/16*.91;
        const p=.72+branch*1.25+.15*Math.sin(t*5+branch);
        points.push(sphere(t,p*sign));
      }
      grooves.push(points);
    }
  }
  function segmentDistance(v,a,b) {
    const ab=sub(b,a),av=sub(v,a),u=Math.max(0,Math.min(1,dot(av,ab)/dot(ab,ab)));
    return Math.hypot(...av.map((x,i)=>x-ab[i]*u));
  }
  function brainSurface(t,p) {
    const n=sphere(t,p);
    let distance=2;
    for(const curve of grooves) for(let k=1;k<curve.length;k++)distance=Math.min(distance,segmentDistance(n,curve[k-1],curve[k]));
    const depth=.105*Math.exp(-Math.pow(distance/.059,2));
    const ripple=.018*Math.sin(p*5+t*3)*Math.sin(t);
    const r=1-depth+ripple;
    // Longitudinal fissure, strongest on the upper surface; both hemispheres join below.
    const fissure=Math.exp(-Math.pow(n[0]/.075,2))*Math.max(0,n[1]+.16);
    return [.143*n[0]*r,.928+.086*n[1]*r-.014*fissure,.041+.117*n[2]*r];
  }
  function surfaceMesh(name,surface,rows,columns) {
    const polygons=[],normals=[];
    const pointNormal=(t,p)=>{
      t=Math.max(.0002,Math.min(Math.PI-.0002,t));
      const dt=sub(surface(t+.0001,p),surface(t-.0001,p));
      const dp=sub(surface(t,p+.0001),surface(t,p-.0001));
      return normalize(cross(dt,dp));
    };
    for(let j=0;j<rows;j++)for(let k=0;k<columns;k++) {
      const a=j*Math.PI/rows,b=(j+1)*Math.PI/rows,c=k*Math.PI*2/columns,d=(k+1)*Math.PI*2/columns;
      const uv=j===0?[[a,c],[b,c],[b,d]]:j===rows-1?[[a,c],[b,c],[a,d]]:[[a,c],[b,c],[b,d],[a,d]];
      // dt cross dp is outward for this spherical parametrization.
      polygons.push(uv.map(([t,p])=>surface(t,p)));
      normals.push(uv.map(([t,p])=>pointNormal(t,p)));
    }
    parts.push({name,material:'brainFlesh',polygons,normals});
  }
  surfaceMesh('Brain_cerebrum',brainSurface,22,40);
  surfaceMesh('Brain_cerebellum',(t,p)=>{
    const n=sphere(t,p),r=1-.027*Math.cos(t*14);
    return [.055*n[0]*r,.855+.031*n[1]*r,-.020+.054*n[2]*r];
  },6,12);
  box('Brain_short_stem',[0,.841,.014],[.027,.027,.034],'brainFlesh',.011);
`;
const code=base.replace('return {palette,parts};',additions+'\nreturn {palette,parts};');
const model=vm.runInNewContext(code+'\nAstronautDesign;');
// Verify manifold-like authored faces have nonzero area and normal winding.
const subtract=(a,b)=>a.map((v,i)=>v-b[i]);
let triangles=0,brainTriangles=0;
for(const part of model.parts)for(const f of part.polygons){
  if(f.some(v=>v.some(x=>!Number.isFinite(x))))throw new Error('Invalid point: '+part.name);
  triangles+=f.length-2;if(part.name.startsWith('Brain_'))brainTriangles+=f.length-2;
}
fs.writeFileSync(path.join(__dirname,'unity-geometry.json'),JSON.stringify(model));
console.log(JSON.stringify({parts:model.parts.length,triangles,brainTriangles}));
