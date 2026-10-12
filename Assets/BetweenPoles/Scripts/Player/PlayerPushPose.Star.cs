using UnityEngine;
namespace BetweenPoles {
public sealed partial class PlayerPushPose {
    bool collectingStarPose;
    public static PlayerPushPose BeginStar(Transform player){var pose=GetPose(player);pose.End();pose.collectingStarPose=true;return pose;}
    public void EndStar(){collectingStarPose=false;End();}
    public Vector3 SampleStar(float time){
        float scoop=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/.55f));
        float toss=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1.2f,time));
        float recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.3f,1.85f,time));
        float pitch=Mathf.Lerp(-55*scoop,-155,toss)*(1-recover);
        visual.localPosition=restPosition+Vector3.down*(.055f*Mathf.Sin(scoop*Mathf.PI)*(1-toss));
        visual.localRotation=restRotation;
        var turn=Quaternion.Euler(pitch,0,0);
        for(int i=0;i<vertices.Length;i++){
            var v=vertices[i];var n=normals[i];
            if(arms[i]){var pivot=new Vector3(Mathf.Sign(v.x)*.235f,.635f,0);v=pivot+turn*(v-pivot);n=turn*n;}
            else if(mapHead[i]){var q=Quaternion.Euler(-15*toss*(1-recover),0,0);var neck=new Vector3(0,.74f,0);v=neck+q*(v-neck);n=q*n;}
            workVertices[i]=v;workNormals[i]=n;
        }
        posed.vertices=workVertices;posed.normals=workNormals;
        return body.transform.TransformPoint(new Vector3(0,.635f,0)+turn*new Vector3(0,-.27f,0)+new Vector3(0,.08f,.07f));
    }
}
}
