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
    PlayerFootstepAudio footsteps;
    bool walkSoundPlayed;
    bool balancing; float balanceTime; Vector2Int balanceDirection;
    const float BalanceSeconds=1.35f;
    public static void BeginEdgeBalance(Transform player,Vector2Int direction){
        var pose=GetPose(player);
        if(pose.balancing&&pose.balanceDirection==direction)return;
        pose.End();pose.balancing=true;pose.balanceTime=0;pose.balanceDirection=direction;
        player.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        pose.SampleBalance(0);
    }
    public void InterruptEdgeBalance(Vector2Int direction){if(balancing&&direction!=balanceDirection)End();}
    public void EndEdgeBalance(){if(balancing)End();}
    static PlayerPushPose GetPose(Transform player){
        var pose=player.GetComponent<PlayerPushPose>();
        if(!pose)pose=player.gameObject.AddComponent<PlayerPushPose>();
        pose.Initialize();return pose;
    }
    public static PlayerPushPose Begin(Transform player){
        var pose=GetPose(player);pose.EndEdgeBalance();pose.walking=false;pose.pushing=true;pose.Sample(0);return pose;
    }
    public static PlayerPushPose BeginWalk(Transform player,float cellSize){
        var pose=GetPose(player);pose.EndEdgeBalance();if(pose.pushing)return pose;
        pose.walking=true;pose.walkSoundPlayed=false;pose.walkFoot=pose.nextWalkFoot;pose.walkDistance=0;pose.walkCellSize=Mathf.Max(.01f,cellSize);pose.SampleWalk(0);return pose;
    }
    public static PlayerPushPose BeginCrossSweep(Transform player){
        var pose=GetPose(player);pose.End();pose.pushing=true;pose.SampleCrossSweep(0);return pose;
    }
    public static float CrossSweepProgress(float progress){
        return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.78f,progress));
    }
    public void SampleCrossSweep(float progress){
        if(!visual)return;
        float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.18f,progress));
        float recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1,progress));
        float weight=reach*(1-recover),sweep=CrossSweepProgress(progress);
        // Both hands sweep right-to-left together in front of the chest, then lower.
        visual.localRotation=restRotation*Quaternion.Euler(8*weight,Mathf.Lerp(10,-12,sweep)*weight,0);
        visual.localPosition=restPosition+new Vector3(0,-.015f*weight,.04f*weight);
        if(!posed)return;
        for(int i=0;i<vertices.Length;i++){
            float side=Mathf.Sign(vertices[i].x);
            var pivot=new Vector3(side*.235f,.635f,0);
            Quaternion turn=Quaternion.AngleAxis(Mathf.Lerp(45,-55,sweep)*weight,Vector3.up)
                *Quaternion.Euler(-80*weight,0,0);
            workVertices[i]=arms[i]?pivot+turn*(vertices[i]-pivot):vertices[i];
            if(i<normals.Length)workNormals[i]=arms[i]?turn*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
    public void AdvanceWalk(float distance){
        if(!walking||pushing)return;
        walkDistance+=distance;SampleWalk(walkDistance/walkCellSize);
        if(Application.isPlaying&&!walkSoundPlayed&&distance>.0001f){
            walkSoundPlayed=true;
            if(!footsteps){footsteps=GetComponent<PlayerFootstepAudio>();if(!footsteps)footsteps=gameObject.AddComponent<PlayerFootstepAudio>();}
            footsteps.PlayStep();
        }
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
    // Opening cinematic: rigid limbs support the body while it rises from prone.
    public static PlayerPushPose CrashPose(Transform player){var p=GetPose(player);p.End();return p;}
    public void SampleCrashSeat(){
        if(!visual)return;
        visual.localRotation=restRotation;visual.localPosition=restPosition+Vector3.down*.30f;
        if(!posed)return;
        for(int i=0;i<vertices.Length;i++){
            float side=Mathf.Sign(vertices[i].x);
            Vector3 pivot=arms[i]?new Vector3(side*.235f,.635f,0):new Vector3(side*.097f,.34f,0);
            Quaternion q=Quaternion.Euler(arms[i]?-48:-82,0,0);
            bool limb=arms[i]||legs[i];workVertices[i]=limb?pivot+q*(vertices[i]-pivot):vertices[i];
            if(i<normals.Length)workNormals[i]=limb?q*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
    public void SampleCrashPose(float prone,float support){SampleInjuredRecovery(prone,support,0,0);}
    public void SampleInjuredRecovery(float prone,float support,float headTouch,float rub){
        if(!visual)return;
        visual.localRotation=restRotation*Quaternion.Euler(88*prone,0,headTouch*4);
        visual.localPosition=restPosition+Vector3.up*(.17f*prone-.09f*support*(1-prone));
        if(!posed)return;
        for(int i=0;i<vertices.Length;i++){
            float side=Mathf.Sign(vertices[i].x);
            Vector3 pivot=arms[i]?new Vector3(side*.235f,.635f,0):new Vector3(side*.097f,.34f,0);
            Quaternion q=arms[i]?Quaternion.Euler(-65*support,0,-side*12*support):Quaternion.Euler(40*support,0,0);
            // One hand stays braced; the other reaches the helmet and gently rubs it.
            if(arms[i]&&side>0)q=Quaternion.Slerp(q,Quaternion.Euler(-12+rub*3,0,-155+rub*4),headTouch);
            bool limb=arms[i]||legs[i];workVertices[i]=limb?pivot+q*(vertices[i]-pivot):vertices[i];
            if(i<normals.Length)workNormals[i]=limb?q*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
    public void End(){
        if(walking&&walkDistance>.001f){
            int steps=Mathf.Max(1,Mathf.RoundToInt(walkDistance/walkCellSize));
            nextWalkFoot=steps%2==0?walkFoot:-walkFoot;
        }
        ApplyPose(0,0,false);pushing=false;walking=false;balancing=false;walkDistance=0;
    }
    void SampleBalance(float seconds){
        if(!visual)return;
        float p=Mathf.Clamp01(seconds/BalanceSeconds);
        float weight=Mathf.SmoothStep(0,1,p/.14f)*(1-Mathf.SmoothStep(0,1,(p-.68f)/.32f));
        float flail=seconds*Mathf.PI*2*2.2f;
        // Rotate the visual about its feet; the logical player never leaves its cell.
        visual.localRotation=restRotation*Quaternion.Euler((32+3*Mathf.Sin(flail))*weight,0,3*Mathf.Sin(flail*.5f)*weight);
        visual.localPosition=restPosition+new Vector3(0,-.025f*weight,.09f*weight);
        if(!posed)return;
        for(int i=0;i<vertices.Length;i++){
            float side=Mathf.Sign(vertices[i].x);
            Vector3 pivot=arms[i]?new Vector3(side*.235f,.635f,0):new Vector3(side*.097f,.34f,0);
            // Spread away from the torso after the smaller forward/back swing.
            // Keeping pitch below 90 degrees prevents the spread from folding inward.
            Quaternion turn=arms[i]
                ?Quaternion.AngleAxis(side*(55+15*Mathf.Cos(flail))*weight,Vector3.forward)
                    *Quaternion.Euler((-25+30*Mathf.Sin(flail+side*.8f))*weight,0,0)
                :Quaternion.Euler(-18*weight,0,0);
            bool limb=arms[i]||legs[i];
            workVertices[i]=limb?pivot+turn*(vertices[i]-pivot):vertices[i];
            if(i<normals.Length)workNormals[i]=limb?turn*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
    void LateUpdate(){
        if(balancing){
            if(board&&board.Busy){End();return;}
            balanceTime+=Time.deltaTime;
            if(balanceTime>=BalanceSeconds)End();else SampleBalance(balanceTime);
        }else if((pushing||walking)&&(!board||!board.Busy))End();
    }
    void OnDisable(){End();}
    void OnDestroy(){if(body&&original)body.sharedMesh=original;if(posed){if(Application.isPlaying)Destroy(posed);else DestroyImmediate(posed);}}
}
}
