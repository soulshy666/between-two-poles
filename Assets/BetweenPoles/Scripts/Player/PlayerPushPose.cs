using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
// A visual-only pose for the static astronaut mesh; grid movement owns the player root.
public sealed class PlayerPushPose:MonoBehaviour {
    Transform visual; Vector3 restPosition; Quaternion restRotation;
    MeshFilter body; Mesh original,posed;
    Vector3[] vertices,normals,workVertices,workNormals;
    bool[] arms,legs; bool pushing,walking; float walkDistance,walkCellSize=1.5f; GridPlayground board;
    float nextWalkFoot=1,walkFoot=1;
    static PlayerPushPose GetPose(Transform player){
        var pose=player.GetComponent<PlayerPushPose>();
        if(!pose)pose=player.gameObject.AddComponent<PlayerPushPose>();
        pose.Initialize();return pose;
    }
    public static PlayerPushPose Begin(Transform player){
        var pose=GetPose(player);pose.walking=false;pose.pushing=true;pose.Sample(0);return pose;
    }
    public static PlayerPushPose BeginWalk(Transform player,float cellSize){
        var pose=GetPose(player);if(pose.pushing)return pose;
        pose.walking=true;pose.walkFoot=pose.nextWalkFoot;pose.walkDistance=0;pose.walkCellSize=Mathf.Max(.01f,cellSize);pose.SampleWalk(0);return pose;
    }
    public void AdvanceWalk(float distance){
        if(!walking||pushing)return;
        walkDistance+=distance;SampleWalk(walkDistance/walkCellSize);
    }
    public void SampleWalk(float cells){
        if(pushing)return;
        // Half a gait cycle per cell. Preserve the next foot across the short
        // neutral landing pause, so successive cells do not restart on one side.
        ApplyPose(0,walkFoot*Mathf.Sin(cells*Mathf.PI),true);
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
        arms=new bool[vertices.Length];legs=new bool[vertices.Length];
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
            legs[i]=Mathf.Abs(b.center.x)>.045f&&b.max.y<.37f&&(b.min.x>0||b.max.x<0);
        }
        body.sharedMesh=posed;
    }
    static int Root(int[] parents,int i){while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
    static void Join(int[] parents,int a,int b){parents[Root(parents,b)]=Root(parents,a);}
    public void Sample(float progress){
        if(!visual)return;
        float effort=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.12f))
            *(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.84f)/.16f)));
        ApplyPose(effort,Mathf.Sin(progress*Mathf.PI*2),false);
    }
    void ApplyPose(float effort,float stride,bool swingArms){
        if(!visual)return;
        visual.localRotation=restRotation*Quaternion.Euler(13f*effort,0,0);
        visual.localPosition=restPosition+new Vector3(0,-.035f*effort+(swingArms?.008f*Mathf.Abs(stride):0),.035f*effort);
        if(!posed)return;
        Quaternion reach=Quaternion.Euler(-72f*effort,0,0);
        for(int i=0;i<vertices.Length;i++){
            float side=Mathf.Sign(vertices[i].x);
            Vector3 pivot=arms[i]?new Vector3(side*.235f,.635f,0):new Vector3(side*.097f,.34f,0);
            Quaternion turn=arms[i]?(swingArms?Quaternion.Euler(40f*stride*side,0,0):reach):Quaternion.Euler(-(swingArms?40f:26f)*stride*side,0,0);
            bool limb=arms[i]||legs[i];
            // Lift the advancing foot slightly while the opposite arm swings forward.
            Vector3 lift=legs[i]?Vector3.up*((swingArms?.03f:.015f)*Mathf.Max(0,stride*side)):Vector3.zero;
            workVertices[i]=limb?pivot+turn*(vertices[i]-pivot)+lift:vertices[i];
            if(i<normals.Length)workNormals[i]=limb?turn*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
        // Retain the original enlarged bounds used by the spherical-world renderer.
    }
    public void End(){
        if(walking&&walkDistance>.001f){
            int steps=Mathf.Max(1,Mathf.RoundToInt(walkDistance/walkCellSize));
            nextWalkFoot=steps%2==0?walkFoot:-walkFoot;
        }
        ApplyPose(0,0,false);pushing=false;walking=false;walkDistance=0;
    }
    void LateUpdate(){if((pushing||walking)&&(!board||!board.Busy))End();}
    void OnDisable(){End();}
    void OnDestroy(){if(body&&original)body.sharedMesh=original;if(posed){if(Application.isPlaying)Destroy(posed);else DestroyImmediate(posed);}}
}
}
