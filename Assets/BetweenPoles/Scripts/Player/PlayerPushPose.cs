using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
// A visual-only pose for the static astronaut mesh; grid movement owns the player root.
public sealed class PlayerPushPose:MonoBehaviour {
    Transform visual; Vector3 restPosition; Quaternion restRotation;
    MeshFilter body; Mesh original,posed;
    Vector3[] vertices,normals,workVertices,workNormals;
    bool[] arms; bool pushing; GridPlayground board;
    public static PlayerPushPose Begin(Transform player){
        var pose=player.GetComponent<PlayerPushPose>();
        if(!pose)pose=player.gameObject.AddComponent<PlayerPushPose>();
        pose.Initialize();pose.pushing=true;pose.Sample(0);return pose;
    }
    void Initialize(){
        if(visual)return;
        board=GetComponentInParent<GridPlayground>();if(!board)board=FindObjectOfType<GridPlayground>();
        visual=transform.Find("Visual");if(!visual)return;
        restPosition=visual.localPosition;restRotation=visual.localRotation;
        foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
            if(filter.sharedMesh&&filter.sharedMesh.name=="AstronautBody"){body=filter;break;}
        if(!body||!body.sharedMesh.isReadable)return;
        original=body.sharedMesh;posed=Instantiate(original);posed.name="Astronaut push pose";posed.MarkDynamic();
        vertices=original.vertices;normals=original.normals;
        workVertices=new Vector3[vertices.Length];workNormals=new Vector3[normals.Length];
        arms=new bool[vertices.Length];
        // Weld coincident face vertices to identify whole rigid model parts. This avoids
        // tearing shoulder faces or accidentally pulling the chest along with the hands.
        var parent=new int[vertices.Length];var welded=new Dictionary<Vector3,int>();
        for(int i=0;i<parent.Length;i++){parent[i]=i;if(welded.TryGetValue(vertices[i],out int j))parent[i]=j;else welded.Add(vertices[i],i);}
        var triangles=original.triangles;
        for(int i=0;i<triangles.Length;i+=3){Join(parent,triangles[i],triangles[i+1]);Join(parent,triangles[i],triangles[i+2]);}
        var bounds=new Dictionary<int,Bounds>();
        for(int i=0;i<vertices.Length;i++){int root=Root(parent,i);if(!bounds.TryGetValue(root,out var b))b=new Bounds(vertices[i],Vector3.zero);else b.Encapsulate(vertices[i]);bounds[root]=b;}
        for(int i=0;i<vertices.Length;i++){
            var b=bounds[Root(parent,i)];
            arms[i]=Mathf.Abs(b.center.x)>.20f&&b.min.y>.30f&&b.max.y<.74f;
        }
        body.sharedMesh=posed;
    }
    static int Root(int[] parents,int i){while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
    static void Join(int[] parents,int a,int b){parents[Root(parents,b)]=Root(parents,a);}
    public void Sample(float progress){
        if(!visual)return;
        float effort=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.12f))
            *(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.84f)/.16f)));
        visual.localRotation=restRotation*Quaternion.Euler(13f*effort,0,0);
        visual.localPosition=restPosition+new Vector3(0,-.035f*effort,.035f*effort);
        if(!posed)return;
        Quaternion reach=Quaternion.Euler(-72f*effort,0,0);
        for(int i=0;i<vertices.Length;i++){
            Vector3 pivot=new Vector3(Mathf.Sign(vertices[i].x)*.235f,.635f,0);
            workVertices[i]=arms[i]?pivot+reach*(vertices[i]-pivot):vertices[i];
            if(i<normals.Length)workNormals[i]=arms[i]?reach*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
        // Retain the original enlarged bounds used by the spherical-world renderer.
    }
    public void End(){if(!pushing)return;Sample(0);pushing=false;}
    void LateUpdate(){if(pushing&&(!board||!board.Busy))End();}
    void OnDisable(){End();}
    void OnDestroy(){if(body&&original)body.sharedMesh=original;if(posed){if(Application.isPlaying)Destroy(posed);else DestroyImmediate(posed);}}
}
}
