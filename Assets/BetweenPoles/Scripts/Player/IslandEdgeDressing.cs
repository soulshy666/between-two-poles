using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
// Lightweight peripheral artwork, independent of movement and magnet occupancy.
public sealed class IslandEdgeDressing:MonoBehaviour {
    readonly List<GameObject> roots=new List<GameObject>();
    readonly List<Mesh> meshes=new List<Mesh>();
    Material[] materials;
    sealed class Geometry {
        public readonly List<Vector3> vertices=new List<Vector3>();
        public readonly List<int>[] faces={new List<int>(),new List<int>()};
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,int material){
            int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
            faces[material].Add(n);faces[material].Add(n+1);faces[material].Add(n+2);
        }
        public void Crystal(Vector3 foot,float radius,float height,Vector3 lean){
            var tip=foot+Vector3.up*height+lean;
            for(int i=0;i<5;i++){
                float a=i*Mathf.PI*2/5,b=(i+1)*Mathf.PI*2/5;
                var p=foot+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
                var q=foot+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius;
                var shoulderP=p+Vector3.up*height*.65f+lean*.65f;
                var shoulderQ=q+Vector3.up*height*.65f+lean*.65f;
                int tone=i%3==0?1:0;
                Triangle(p,shoulderP,shoulderQ,tone);Triangle(p,shoulderQ,q,tone);
                Triangle(shoulderP,tip,shoulderQ,1);
            }
        }
    }
    public static void Refresh(GridPlayground board){
        var scene=board.gameObject.scene.name;
        if(!scene.Contains("Ice")&&!scene.Contains("磁铁机制测试"))return;
        var art=board.GetComponent<IslandEdgeDressing>();
        if(!art)art=board.gameObject.AddComponent<IslandEdgeDressing>();
        art.Rebuild(board);
    }
    void Clear(){
        foreach(var root in roots)if(root){root.SetActive(false);Destroy(root);}
        foreach(var mesh in meshes)if(mesh)Destroy(mesh);
        roots.Clear();meshes.Clear();
    }
    void Rebuild(GridPlayground board){
        var window=board.GetComponent<FiveIslandWindow>();
        if(window)foreach(var room in window.rooms)
            room.surfaces=(room.surfaces??new Renderer[0]).Where(r=>r&&!roots.Contains(r.gameObject)).ToArray();
        Clear();
        if(materials==null){
            var colors=new[]{new Color(.29f,.65f,.77f),new Color(.68f,.91f,.96f)};
            materials=new Material[2];
            for(int i=0;i<2;i++){
                var mat=new Material(Shader.Find("BetweenPoles/PaintedIceTrial")){name="Crystal edge "+i,color=colors[i]};
                mat.SetFloat("_Painted",0);mat.SetFloat("_Grid",0);mat.SetFloat("_Snow",0);materials[i]=mat;
            }
        }
        float size=board.cellSize;
        var tiles=new Dictionary<Vector2Int,GridTile>();
        foreach(var t in board.tiles)if(t)tiles[new Vector2Int(Mathf.RoundToInt(t.transform.position.x/size),Mathf.RoundToInt(t.transform.position.z/size))]=t;
        var groups=new Dictionary<Transform,Geometry>();
        foreach(var entry in tiles){
            var tile=entry.Value;var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();
            var owner=binding&&binding.center?binding.center:board.transform;
            if(!groups.TryGetValue(owner,out var geometry)){geometry=new Geometry();groups.Add(owner,geometry);}
            foreach(var d in new[]{Vector2Int.right,Vector2Int.up,Vector2Int.left,Vector2Int.down}){
                if(tiles.ContainsKey(entry.Key+d))continue;
                bool crossing=false;
                for(int step=2;step<=4;step++)if(tiles.ContainsKey(entry.Key+d*step)){crossing=true;break;}
                if(crossing)continue;
                var tangentCell=new Vector2Int(-d.y,d.x);
                bool corner=!tiles.ContainsKey(entry.Key+tangentCell)||!tiles.ContainsKey(entry.Key-tangentCell);
                var outward=new Vector3(d.x,0,d.y);var tangent=new Vector3(-d.y,0,d.x);
                var center=new Vector3(entry.Key.x*size,tile.surfaceHeight,entry.Key.y*size);
                uint hash=unchecked((uint)(entry.Key.x*73856093^entry.Key.y*19349663^d.x*83492791^d.y*1237));
                float variation=(hash%101)/100f;
                // All decoration rests on the upper rim, never below the island.
                if(hash%4==0&&!tile.blocked){
                    // Move corner clusters toward the straight outer rim, clear
                    // of the rounded-away corner. Embed roots in the upper bevel.
                    float along=corner?(!tiles.ContainsKey(entry.Key+tangentCell)?-.18f:.18f):.06f;
                    var foot=center+outward*size*.44f+tangent*size*along;
                    foot.y-=.10f*(size/1.5f);
                    geometry.Crystal(foot,.12f,.55f+variation*.18f,outward*.065f);
                    geometry.Crystal(foot-tangent*.19f,.085f,.36f,tangent*-.05f);
                    geometry.Crystal(foot+tangent*.16f-outward*.04f,.07f,.25f,tangent*.045f);
                }
            }
        }
        foreach(var group in groups){
            var data=group.Value;if(data.vertices.Count==0)continue;
            var mesh=new Mesh{name="Island perimeter crystals",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(data.vertices);mesh.subMeshCount=2;
            for(int i=0;i<2;i++)mesh.SetTriangles(data.faces[i],i);
            mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
            var root=new GameObject("岛屿外沿 · 冰晶");root.transform.SetParent(board.transform,false);
            root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;
            var parentScale=board.transform.lossyScale;root.transform.localScale=new Vector3(1/parentScale.x,1/parentScale.y,1/parentScale.z);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var anchor=root.AddComponent<IslandSurfaceAnchor>();anchor.center=group.Key;anchor.Apply();roots.Add(root);
            if(window)foreach(var room in window.rooms)if(room.center==group.Key)room.surfaces=room.surfaces.Concat(new[]{renderer}).ToArray();
        }
        if(window&&window.VisibleCenters!=null)window.Show(window.CurrentRoom);
    }
    void OnDestroy(){Clear();if(materials!=null)foreach(var mat in materials)if(mat)Destroy(mat);}
}
}
