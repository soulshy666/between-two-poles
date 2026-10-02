using System;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles.Authoring {
// Port of curve_ground.cs / clip_crust_corners.cs: original four-facet .70 radius and .18 bevel.
public static class IceIslandArt {
 public static Mesh Build(IEnumerable<Vector2Int> points,float size=1.5f){
  var cells=new HashSet<Vector2Int>(points);var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();float h=size/2,scale=size/1.5f;
  Action<Vector3,Vector3,Vector3> triangle=(a,b,c)=>{int n=vertices.Count;vertices.AddRange(new[]{a,b,c});colors.AddRange(new[]{Color.white,Color.white,Color.white});indices.AddRange(new[]{n,n+1,n+2});};
  foreach(var cell in cells){
   var cuts=new List<Vector2[]>();
   foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1}){
    if(cells.Contains(cell+new Vector2Int(sx,0))||cells.Contains(cell+new Vector2Int(0,sz)))continue;
    for(int j=0;j<4;j++){float a=j*Mathf.PI/8,b=(j+1)*Mathf.PI/8;cuts.Add(new[]{new Vector2(sx*(.05f+.70f*Mathf.Cos(a))*scale,sz*(.05f+.70f*Mathf.Sin(a))*scale),new Vector2(sx*(.05f+.70f*Mathf.Cos(b))*scale,sz*(.05f+.70f*Mathf.Sin(b))*scale)});}
   }
   Func<List<Vector2>,List<Vector2>> clip=poly=>{foreach(var cut in cuts){Vector2 e=cut[1]-cut[0],normal=new Vector2(-e.y,e.x);if(Vector2.Dot(normal,cut[0])<0)normal=-normal;var next=new List<Vector2>();for(int i=0;i<poly.Count;i++){Vector2 a=poly[i],b=poly[(i+1)%poly.Count];float da=Vector2.Dot(normal,a-cut[0]),db=Vector2.Dot(normal,b-cut[0]);if(da<=.000001f)next.Add(a);if((da<0)!=(db<0))next.Add(Vector2.Lerp(a,b,da/(da-db)));}poly=next;}return poly;};
   var edges=new List<Vector2[]>();
   for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++){var near=cell+new Vector2Int(dx,dz);if(!cells.Contains(near))continue;foreach(var dir in new[]{Vector2Int.left,Vector2Int.up,Vector2Int.right,Vector2Int.down})if(!cells.Contains(near+dir)){Vector2 middle=new Vector2(dx*size+dir.x*h,dz*size+dir.y*h),tangent=new Vector2(-dir.y,dir.x)*h;edges.Add(new[]{middle-tangent,middle+tangent});}}
   Func<Vector2,float> height=p=>{float distance=100;foreach(var edge in edges){var d=edge[1]-edge[0];var near=edge[0]+d*Mathf.Clamp01(Vector2.Dot(p-edge[0],d)/d.sqrMagnitude);distance=Mathf.Min(distance,Vector2.Distance(p,near));}return -.018f*scale-.18f*scale*Mathf.SmoothStep(0,1,Mathf.Clamp01(1-distance/(.26f*scale)));};
   Vector3 center=new Vector3(cell.x*size,0,cell.y*size);
   Func<Vector2,Vector3> top=p=>center+new Vector3(p.x,height(p),p.y);
   for(int x=0;x<12;x++)for(int z=0;z<12;z++){
    float a=x*size/12-h,b=(x+1)*size/12-h,c=z*size/12-h,d=(z+1)*size/12-h;
    var polygon=clip(new List<Vector2>{new Vector2(a,c),new Vector2(a,d),new Vector2(b,d),new Vector2(b,c)});
    for(int k=1;k+1<polygon.Count;k++){var p=top(polygon[0]);var q=top(polygon[k]);var r=top(polygon[k+1]);if(Vector3.Cross(q-p,r-p).sqrMagnitude>1e-12f)triangle(p,q,r);}
   }
   var boundary=clip(new List<Vector2>{new Vector2(-h,-h),new Vector2(-h,h),new Vector2(h,h),new Vector2(h,-h)});
   for(int i=0;i<boundary.Count;i++){
    var a=boundary[i];var b=boundary[(i+1)%boundary.Count];if(Vector2.Distance(a,b)<.00001f)continue;
    bool shared=false;foreach(var dir in new[]{Vector2Int.left,Vector2Int.up,Vector2Int.right,Vector2Int.down})if(cells.Contains(cell+dir)){Vector2 n=dir;if(Mathf.Abs(Vector2.Dot(a,n)-h)<.00001f&&Mathf.Abs(Vector2.Dot(b,n)-h)<.00001f)shared=true;}if(shared)continue;
    int segments=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/(size/12)));
    for(int j=0;j<segments;j++){var p=top(Vector2.Lerp(a,b,j/(float)segments));var q=top(Vector2.Lerp(a,b,(j+1f)/segments));var lowP=p+Vector3.down*.60f*scale;var lowQ=q+Vector3.down*.60f*scale;triangle(lowP,lowQ,q);triangle(lowP,q,p);}
   }
  }
  var mesh=new Mesh{name="Original floating ice contour",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
 }
}
}
