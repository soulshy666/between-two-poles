using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class MagnetDebugPanel {
    // Orthographic XZ projection of the actual unbent meshes, with a Y depth buffer.
    // This preserves upright footprints, holes, polarity and two-cell occupancy.
    readonly Dictionary<string,Texture2D> topViews=new Dictionary<string,Texture2D>();
    void ClearTopViews(){foreach(var t in topViews.Values)if(t)Destroy(t);topViews.Clear();}
    void OnDestroy(){ClearTopViews();}
    void TopView(Rect rect,MagnetPiece piece,Vector2Int cell){
        if(Event.current.type!=EventType.Repaint)return;
        var key=piece.geometry.GetInstanceID()+":"+cell+":"+piece.geometry.localToWorldMatrix.GetHashCode();
        Texture2D texture;
        if(!topViews.TryGetValue(key,out texture)){
            if(topViews.Count>128)ClearTopViews();
            texture=MakeTopView(piece,cell);topViews.Add(key,texture);
        }
        GUI.DrawTexture(rect,texture,ScaleMode.StretchToFill,true);
    }
    Texture2D MakeTopView(MagnetPiece piece,Vector2Int cell){
        const int resolution=96;
        var colors=new Color[resolution*resolution];var depth=new float[colors.Length];
        for(int i=0;i<depth.Length;i++)depth[i]=float.NegativeInfinity;
        float left=(cell.x-.5f)*board.cellSize,bottom=(cell.y-.5f)*board.cellSize;
        foreach(var meshFilter in piece.geometry.GetComponentsInChildren<MeshFilter>()){
            var renderer=meshFilter.GetComponent<Renderer>();if(!renderer||!renderer.enabled||!meshFilter.gameObject.activeInHierarchy)continue;
            var mesh=meshFilter.sharedMesh;if(!mesh)continue;
            var vertices=mesh.vertices;var triangles=mesh.triangles;var projected=new Vector3[vertices.Length];
            for(int i=0;i<vertices.Length;i++){
                var world=meshFilter.transform.TransformPoint(vertices[i]);
                projected[i]=new Vector3((world.x-left)/board.cellSize*resolution,(world.z-bottom)/board.cellSize*resolution,world.y);
            }
            var color=renderer.sharedMaterial.color;color.a=1;
            for(int i=0;i<triangles.Length;i+=3){
                var a=projected[triangles[i]];var b=projected[triangles[i+1]];var c=projected[triangles[i+2]];
                float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(det)<.0001f)continue;
                int x0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))));
                int x1=Mathf.Min(resolution-1,Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));
                int y0=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))));
                int y1=Mathf.Min(resolution-1,Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){
                    float u=((b.y-c.y)*(x+.5f-c.x)+(c.x-b.x)*(y+.5f-c.y))/det;
                    float v=((c.y-a.y)*(x+.5f-c.x)+(a.x-c.x)*(y+.5f-c.y))/det;float w=1-u-v;
                    if(u<-.0001f||v<-.0001f||w<-.0001f)continue;
                    float height=u*a.z+v*b.z+w*c.z;int index=y*resolution+x;
                    if(height>=depth[index]){depth[index]=height;colors[index]=color;}
                }
            }
        }
        var result=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        result.SetPixels(colors);result.Apply(false,true);return result;
    }
}
}
