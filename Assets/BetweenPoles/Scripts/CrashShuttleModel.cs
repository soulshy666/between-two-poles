using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
// A compact faceted rescue shuttle. Local +Z is the nose, +Y the canopy.
public static class CrashShuttleModel {
    static Material Mat(string name,Color color){
        var m=new Material(Shader.Find("BetweenPoles/PaintedIceTrial"));m.name=name;m.color=color;
        m.SetFloat("_Grid",0);m.SetFloat("_Painted",0);m.SetFloat("_Snow",0);return m;
    }
    static GameObject MeshPart(Transform parent,string name,Mesh mesh,Material material){
        var go=new GameObject(name);go.transform.SetParent(parent,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;
        r.localBounds=new Bounds(Vector3.zero,Vector3.one*300);return go;
    }
    static Mesh Hull(string name,float[] z,float[] width,float[] height){
        var verts=new List<Vector3>();var tris=new List<int>();
        System.Action<Vector3,Vector3,Vector3> tri=(a,b,c)=>{int i=verts.Count;verts.Add(a);verts.Add(b);verts.Add(c);tris.Add(i);tris.Add(i+1);tris.Add(i+2);};
        System.Func<int,int,Vector3> v=(ring,j)=>{float a=(j+.5f)*Mathf.PI/4;return new Vector3(Mathf.Cos(a)*width[ring],Mathf.Sin(a)*height[ring],z[ring]);};
        for(int k=0;k<z.Length-1;k++)for(int j=0;j<8;j++){int n=(j+1)%8;tri(v(k,j),v(k,n),v(k+1,n));tri(v(k,j),v(k+1,n),v(k+1,j));}
        for(int j=0;j<8;j++){tri(new Vector3(0,0,z[0]),v(0,(j+1)%8),v(0,j));int k=z.Length-1;tri(new Vector3(0,0,z[k]),v(k,j),v(k,(j+1)%8));}
        var mesh=new Mesh{name=name};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static GameObject Box(Transform p,string name,Vector3 at,Vector3 size,Material m){
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(p,false);go.transform.localPosition=at;go.transform.localScale=size;
        var c=go.GetComponent<Collider>();c.enabled=false;if(Application.isPlaying)Object.Destroy(c);else Object.DestroyImmediate(c);
        var r=go.GetComponent<Renderer>();r.sharedMaterial=m;r.localBounds=new Bounds(Vector3.zero,Vector3.one*300);return go;
    }
    public static GameObject Create(){
        var root=new GameObject("白色尖头救生飞船");var p=root.transform;
        var white=Mat("Shuttle ivory",new Color(.87f,.91f,.91f));var gray=Mat("Shuttle graphite",new Color(.12f,.17f,.21f));
        var black=Mat("Shuttle dark cockpit",new Color(.025f,.065f,.09f));var trim=Mat("Shuttle steel",new Color(.36f,.45f,.50f));var glow=Mat("Shuttle engine glow",new Color(1.9f,.75f,.22f));
        MeshPart(p,"Faceted white fuselage",Hull("ShuttleHull",new[]{-1f,-.65f,.50f,1.35f},new[]{.32f,.48f,.40f,.015f},new[]{.29f,.38f,.31f,.015f}),white);
        var canopy=MeshPart(p,"Black tapered canopy",Hull("ShuttleCanopy",new[]{-.04f,.15f,.9f},new[]{.26f,.25f,.018f},new[]{.10f,.12f,.01f}),black);canopy.transform.localPosition=new Vector3(0,.30f,0);
        Box(p,"Canopy spine",new Vector3(0,.41f,.28f),new Vector3(.025f,.025f,.6f),trim);
        foreach(float side in new[]{-1f,1f}){
            var fin=MeshPart(p,"Swept stabilizer",Hull("ShuttleFin",new[]{-.96f,-.6f,.2f},new[]{.42f,.35f,.015f},new[]{.05f,.05f,.012f}),white);fin.transform.localPosition=new Vector3(side*.45f,-.13f,-.1f);fin.transform.localRotation=Quaternion.Euler(0,side*18,0);
            var engine=MeshPart(p,"Dark engine pod",Hull("ShuttleEngine",new[]{-.27f,.2f},new[]{.18f,.16f},new[]{.18f,.16f}),gray);engine.transform.localPosition=new Vector3(side*.28f,0,-1.02f);
            Box(p,"Engine rim",new Vector3(side*.28f,0,-1.30f),new Vector3(.32f,.32f,.045f),trim);
            var flame=Box(p,"Thruster flame",new Vector3(side*.28f,0,-1.49f),new Vector3(.16f,.16f,.34f),glow);
            Box(p,"Hull seam",new Vector3(side*.43f,.02f,-.20f),new Vector3(.025f,.36f,.028f),gray);
        }
        return root;
    }
}
}
