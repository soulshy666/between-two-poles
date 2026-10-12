using UnityEngine;
namespace BetweenPoles {
public sealed partial class PlayerPushPose {
    bool collectingStarPose;
    public static PlayerPushPose BeginStar(Transform player){var pose=GetPose(player);pose.End();pose.collectingStarPose=true;return pose;}
    public void EndStar(){collectingStarPose=false;End();}
    public Vector3 SampleStar(float time,float absorbAge=-1){
        float scoop=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/.3f));
        float windup=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.95f,time));
        float toss=Mathf.Pow(Mathf.InverseLerp(.95f,1.2f,time),2);
        float recover=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.3f,1.85f,time));
        float pitch=Mathf.Lerp(-65*scoop+25*windup,-165,toss)*(1-recover);
        // One deep, relaxed breath follows the incoming light, then settles to rest.
        float relax=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.95f,absorbAge))
            *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.75f,2.3f,absorbAge)));
        float breathPhase=Mathf.InverseLerp(.45f,2.3f,absorbAge);
        float breath=Mathf.Pow(Mathf.Sin(breathPhase*Mathf.PI),2);
        float chestLift=.055f*breath;
        visual.localPosition=restPosition+Vector3.down*(.1f*windup*(1-toss))+Vector3.up*(.045f*toss*(1-recover));
        visual.localRotation=restRotation*Quaternion.Euler(10*windup*(1-toss)-8*toss*(1-recover),0,0);
        var turn=Quaternion.Euler(pitch,0,0);
        for(int i=0;i<vertices.Length;i++){
            var v=vertices[i];var n=normals[i];
            if(arms[i]){
                float side=Mathf.Sign(v.x);
                var pivot=new Vector3(side*.235f,.635f,0);
                var armTurn=Quaternion.Euler(pitch+10*relax,0,side*(18*relax+9*breath));
                var shoulder=new Vector3(side*.012f*breath,chestLift*.74f-.012f*relax,0);
                v=pivot+armTurn*(v-pivot)+shoulder;n=armTurn*n;
            }
            else if(legs[i]&&time<.7f){var pivot=new Vector3(Mathf.Sign(v.x)*.097f,.34f,0);var q=Quaternion.Euler(Mathf.Sign(v.x)*22*Mathf.Sin(time/.7f*Mathf.PI*2),0,0);v=pivot+q*(v-pivot);n=q*n;}
            else if(mapHead[i]){var q=Quaternion.Euler(-15*toss*(1-recover)-27*relax-8*breath,0,0);var neck=new Vector3(0,.74f,0);v=neck+q*(v-neck)+Vector3.up*chestLift;n=q*n;}
            else if(!legs[i]&&v.y>.34f){
                // Expand the torso above the hips; feet stay planted and the helmet stays rigid.
                float height=Mathf.Clamp01((v.y-.34f)/.4f);
                float depthScale=1+.12f*breath;
                v.y+=chestLift*height;
                v.z*=depthScale;
                n=new Vector3(n.x,n.y/(v.y<.74f+chestLift?1+chestLift/.4f:1),n.z/depthScale).normalized;
            }
            workVertices[i]=v;workNormals[i]=n;
        }
        posed.vertices=workVertices;posed.normals=workNormals;
        return body.transform.TransformPoint(new Vector3(0,.635f,0)+turn*new Vector3(0,-.27f,0)+new Vector3(0,.08f,.07f));
    }
}
}
