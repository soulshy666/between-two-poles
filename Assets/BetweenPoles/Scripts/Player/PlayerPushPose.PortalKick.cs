using UnityEngine;
namespace BetweenPoles {
public sealed partial class PlayerPushPose {
    Vector3 portalVisualScale;
    bool portalEntryScaling;
    void ResetPortalEntry(){
        if(portalEntryScaling&&visual)visual.localScale=portalVisualScale;
        portalEntryScaling=false;
    }
    public void SamplePortalEntry(float progress){
        float p=Mathf.Clamp01(progress),tuck=Mathf.Sin(p*Mathf.PI);
        SamplePortalLimbs(-65*tuck,-65*tuck,55*tuck,55*tuck);
        if(visual){
            if(!portalEntryScaling){portalVisualScale=visual.localScale;portalEntryScaling=true;}
            float suction=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,p));
            visual.localScale=portalVisualScale*(1-suction);
            visual.localRotation=restRotation*Quaternion.Euler(18*tuck,0,0);
            visual.localPosition=Vector3.Lerp(restPosition,new Vector3(0,-.2f,0),suction);
        }
    }
    public void SamplePlatformJump(float progress){
        float p=Mathf.Clamp01(progress);
        float crouch=p<.18f?Mathf.Sin(p/.18f*Mathf.PI):p>.83f?Mathf.Sin((p-.83f)/.17f*Mathf.PI)*.6f:0;
        float tuck=Mathf.Sin(Mathf.InverseLerp(.28f,.83f,p)*Mathf.PI);
        SamplePortalLimbs(-45*crouch-95*tuck,-45*crouch-95*tuck,48*crouch+40*tuck,48*crouch+40*tuck);
        if(visual){visual.localPosition=restPosition+Vector3.down*(.10f*crouch);visual.localRotation=restRotation*Quaternion.Euler(12*crouch+8*tuck,0,0);}
    }
    public void SamplePortalFlight(float progress){
        float p=Mathf.Clamp01(progress),phase=p*Mathf.PI*8;
        float fall=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,p));
        SamplePortalLimbs(Mathf.Lerp(-105+55*Mathf.Sin(phase),-55,fall),Mathf.Lerp(-105+55*Mathf.Sin(phase+Mathf.PI),-55,fall),Mathf.Lerp(45*Mathf.Sin(phase+1),0,fall),Mathf.Lerp(45*Mathf.Sin(phase+1+Mathf.PI),0,fall));
        if(visual){visual.localRotation=restRotation*Quaternion.Euler(Mathf.Lerp(-18+12*Mathf.Sin(phase*.5f),88,fall),0,8*Mathf.Sin(phase)*(1-fall));visual.localPosition=restPosition+Vector3.up*(.17f*fall);}
    }
    void SamplePortalLimbs(float leftArm,float rightArm,float leftLeg,float rightLeg){
        if(!posed)return;
        for(int i=0;i<vertices.Length;i++){
            float side=Mathf.Sign(vertices[i].x);bool limb=arms[i]||legs[i];
            var pivot=arms[i]?new Vector3(side*.235f,.635f,0):new Vector3(side*.097f,.34f,0);
            float angle=arms[i]?(side<0?leftArm:rightArm):(side<0?leftLeg:rightLeg);
            var turn=Quaternion.Euler(angle,0,arms[i]?-side*22:0);
            workVertices[i]=limb?pivot+turn*(vertices[i]-pivot):vertices[i];
            if(i<normals.Length)workNormals[i]=limb?turn*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
    public void SamplePortalKick(float progress){
        if(!visual)return;
        float weight=Mathf.Sin(Mathf.Clamp01(progress)*Mathf.PI);
        visual.localRotation=restRotation*Quaternion.Euler(-6*weight,0,0);
        if(!posed)return;
        var pivot=new Vector3(.13f,.34f,0);var turn=Quaternion.Euler(-55*weight,0,0);
        for(int i=0;i<vertices.Length;i++){
            bool foot=legs[i]&&vertices[i].x>0;
            workVertices[i]=foot?pivot+turn*(vertices[i]-pivot):vertices[i];
            if(i<normals.Length)workNormals[i]=foot?turn*normals[i]:normals[i];
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
}
}
