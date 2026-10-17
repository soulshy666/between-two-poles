using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
// Pose a private mesh copy: never modify the shared gameplay character asset.
[ExecuteAlways]
public sealed class CoverActorFloat:MonoBehaviour {
    public MeshFilter body;
    public Mesh sourceMesh;
    Mesh original,posed;
    Vector3[] source,normals,vertices,workNormals;
    bool[] arms,legs;
    float[] jointWeights;
    int sampledFrame=int.MinValue;
    public void Sample(float t){
        if(!posed)Initialize();if(!posed)return;
        int frame=Mathf.FloorToInt(t*8);if(frame==sampledFrame)return;sampledFrame=frame;t=frame/8f;
        var leftArm=LimbRotation(t,-1,true);var rightArm=LimbRotation(t,1,true);
        var leftLeg=LimbRotation(t,-1,false);var rightLeg=LimbRotation(t,1,false);
        float leftElbow=JointBend(t,-1,true),rightElbow=JointBend(t,1,true);
        float leftKnee=JointBend(t,-1,false),rightKnee=JointBend(t,1,false);
        for(int i=0;i<source.Length;i++){
            float side=Mathf.Sign(source[i].x);bool arm=arms[i],leg=legs[i];
            var pivot=arm?new Vector3(side*.235f,.635f,0):new Vector3(side*.097f,.34f,0);
            var turn=arm?(side<0?leftArm:rightArm):(side<0?leftLeg:rightLeg);
            var point=source[i];var normal=i<normals.Length?normals[i]:Vector3.up;
            if(arm||leg){
                var joint=arm?new Vector3(side*.235f,.51f,0):new Vector3(side*.097f,.19f,0);
                float bend=arm?(side<0?leftElbow:rightElbow):(side<0?leftKnee:rightKnee);
                // Blend only around the joint; coincident vertices get identical weights.
                var distal=Quaternion.Euler(bend*jointWeights[i],0,0);
                point=joint+distal*(point-joint);normal=distal*normal;
                point=pivot+turn*(point-pivot);normal=turn*normal;
            }
            vertices[i]=point;if(i<normals.Length)workNormals[i]=normal;
        }
        posed.vertices=vertices;posed.normals=workNormals;posed.RecalculateBounds();
    }
    // Different periods and phases let each limb settle independently in zero gravity.
    // Held pixel frames keep the motion crisp while elbows and knees flex locally.
    static Quaternion LimbRotation(float t,float side,bool arm){
        float phase=side>0?1.7f:.2f;
        float sway=Mathf.Sin(t*(arm?.67f:.53f)+phase);
        float settle=Mathf.Sin(t*(arm?.39f:.31f)+phase+1.2f);
        float x=arm?-15+sway*17+settle*4:(side>0?34:12)+sway*13;
        float y=arm?side*(4+settle*4):settle*3;
        float z=arm?side*((side>0?62:38)+Mathf.Sin(t*.57f+phase+1)*17)
                   :side*((side>0?16:10)+Mathf.Sin(t*.43f+phase)*9);
        return Quaternion.Euler(Mathf.Round(x),Mathf.Round(y),Mathf.Round(z));
    }
    static float JointBend(float t,float side,bool arm){
        float phase=side>0?2.4f:.4f;
        // Hands and feet lag behind the shoulder/hip sway rather than moving as rods.
        float wave=Mathf.Sin(t*(arm?.76f:.61f)+phase);
        float secondary=Mathf.Sin(t*.29f+phase+1);
        return Mathf.Round(arm?-(28+wave*20+secondary*5):30+wave*22+secondary*5);
    }
    void Initialize(){
        if(!body||!sourceMesh||!sourceMesh.isReadable)return;
        original=sourceMesh;source=original.vertices;normals=original.normals;
        vertices=new Vector3[source.Length];workNormals=new Vector3[normals.Length];arms=new bool[source.Length];legs=new bool[source.Length];jointWeights=new float[source.Length];
        var parent=new int[source.Length];var welded=new Dictionary<Vector3,int>();
        for(int i=0;i<parent.Length;i++){parent[i]=i;if(welded.TryGetValue(source[i],out int j))parent[i]=j;else welded.Add(source[i],i);}
        var triangles=original.triangles;
        for(int i=0;i<triangles.Length;i+=3){Join(parent,triangles[i],triangles[i+1]);Join(parent,triangles[i],triangles[i+2]);}
        var bounds=new Dictionary<int,Bounds>();
        for(int i=0;i<source.Length;i++){int root=Root(parent,i);if(!bounds.TryGetValue(root,out var b))b=new Bounds(source[i],Vector3.zero);else b.Encapsulate(source[i]);bounds[root]=b;}
        for(int i=0;i<source.Length;i++){var b=bounds[Root(parent,i)];arms[i]=Mathf.Abs(b.center.x)>.20f&&b.min.y>.30f&&b.max.y<.74f;legs[i]=Mathf.Abs(b.center.x)>.045f&&b.max.y<.37f&&(b.min.x>0||b.max.x<0);}
        for(int i=0;i<source.Length;i++){
            float jointY=arms[i]?.51f:.19f;
            jointWeights[i]=Mathf.SmoothStep(0,1,Mathf.InverseLerp(jointY+.035f,jointY-.045f,source[i].y));
        }
        posed=Instantiate(original);posed.name="封面主角 · 失重姿态";posed.hideFlags=HideFlags.HideAndDontSave;posed.MarkDynamic();body.sharedMesh=posed;
    }
    static int Root(int[] p,int i){while(p[i]!=i){p[i]=p[p[i]];i=p[i];}return i;}
    static void Join(int[] p,int a,int b){p[Root(p,b)]=Root(p,a);}
    void OnDisable(){if(body&&original)body.sharedMesh=original;if(posed){if(Application.isPlaying)Destroy(posed);else DestroyImmediate(posed);}posed=null;original=null;sampledFrame=int.MinValue;}
}
}
