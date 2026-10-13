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
        SamplePortalLimbs(-65*tuck,-65*tuck,55*tuck,55*tuck,22*tuck);
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
        // Right foot pushes off, left knee leads; the opposite arm leads with it.
        // Match the movement's .18 takeoff and .83 touchdown instead of pumping
        // both legs together. Lower joints bend before hip/shoulder rotation.
        float stride=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.38f,p))
            *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,.90f,p)));
        if(posed)for(int i=0;i<vertices.Length;i++){
            var v=vertices[i];var n=i<normals.Length?normals[i]:Vector3.up;
            bool arm=arms[i],leg=legs[i];float side=Mathf.Sign(v.x);
            if(arm||leg){
                var joint=new Vector3(side*(arm?.235f:.097f),arm?.49f:.19f,0);
                float lower=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(arm?.46f:.15f,arm?.53f:.23f,v.y));
                float bend=arm?(side>0?-85:-16)*stride:(side<0?78:24)*stride+30*crouch;
                var q=Quaternion.Euler(bend*lower,0,0);
                v=joint+q*(v-joint);n=q*n;
                var pivot=new Vector3(side*(arm?.235f:.097f),arm?.635f:.34f,0);
                float pitch=arm?(side>0?-65:30)*stride:(side<0?-72:25)*stride-18*crouch;
                q=Quaternion.Euler(pitch,0,arm?side*8*stride:0);
                v=pivot+q*(v-pivot);n=q*n;
            }
            workVertices[i]=v;if(i<normals.Length)workNormals[i]=n;
        }
        if(posed){posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;}
        if(visual){visual.localPosition=restPosition+Vector3.down*(.065f*crouch);visual.localRotation=restRotation*Quaternion.Euler(8*crouch+6*stride,0,-2*stride);}
    }
    public void SamplePortalFlight(float progress){
        SampleUnstablePortalArrival(progress);
    }
    public void SamplePortalHeadImpact(float impact,float sink){
        SamplePortalLimbs(-35-20*impact,-35-20*impact,10*impact,10*impact);
        if(visual){
            visual.localRotation=restRotation*Quaternion.Euler(10*impact,0,0);
            visual.localPosition=restPosition+Vector3.down*(.08f*impact);
        }
    }
    public void SampleUnstablePortalArrival(float progress){
        float p=Mathf.Clamp01(progress),phase=p*Mathf.PI*8;
        float prone=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,p));
        float flail=Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.12f))*(1-prone);
        // Keep the thrown flail until the final approach, then meet the spring's prone pose exactly.
        SamplePortalLimbs(flail*(-65+55*Mathf.Sin(phase)),flail*(-65+55*Mathf.Sin(phase+Mathf.PI)),
            flail*50*Mathf.Sin(phase+1),flail*50*Mathf.Sin(phase+1+Mathf.PI),46*flail,true);
        BlendPortalSprawl(prone);
        if(visual){
            visual.localRotation=restRotation*Quaternion.Euler(88*prone+flail*(-18+12*Mathf.Sin(phase*.5f)),
                8*Mathf.Sin(phase*.6f)*flail,12*Mathf.Sin(phase)*flail);
            visual.localPosition=restPosition+Vector3.up*(.17f*prone);
        }
    }
    public void SamplePortalProne(){SampleUnstablePortalArrival(1);}
    public void SamplePortalRecovery(float rise){
        rise=Mathf.Clamp01(rise);
        SampleCrashPose(1-rise,Mathf.Sin(rise*Mathf.PI));
        BlendPortalSprawl(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(rise/.65f)));
    }
    // A sprawled impact pose, shared by touchdown, compression and spring release.
    void BlendPortalSprawl(float weight){
        if(!posed||weight<=0)return;
        for(int i=0;i<vertices.Length;i++){
            var v=vertices[i];var n=i<normals.Length?normals[i]:Vector3.up;
            float side=Mathf.Sign(v.x);
            if(arms[i]||legs[i]){
                bool arm=arms[i];
                var joint=arm?new Vector3(side*.235f,.49f,0):new Vector3(side*.097f,.19f,0);
                float lower=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(arm?.46f:.15f,arm?.53f:.23f,v.y));
                float bend=arm?(side>0?70:-55):(side>0?-38:8);
                var q=Quaternion.Euler(0,0,bend*lower);
                v=joint+q*(v-joint);n=q*n;
                var pivot=arm?new Vector3(side*.235f,.635f,0):new Vector3(side*.097f,.34f,0);
                float spread=arm?(side>0?105:-70):(side>0?38:-15);
                q=Quaternion.Euler(0,0,spread);
                v=pivot+q*(v-pivot);n=q*n;
            }else if(mapHead[i]){
                var neck=new Vector3(0,.74f,0);var q=Quaternion.Euler(0,65,0);
                v=neck+q*(v-neck);n=q*n;
            }
            workVertices[i]=Vector3.Lerp(workVertices[i],v,weight);
            if(i<normals.Length)workNormals[i]=Vector3.Lerp(workNormals[i],n,weight).normalized;
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
    public void SampleUnstablePortalBalance(float sway,float weight){
        SamplePortalLimbs(-45*weight-25*sway,-45*weight+25*sway,14*weight+12*sway,14*weight-12*sway,32*weight);
        if(visual){
            visual.localPosition=restPosition+Vector3.down*(.035f*weight);
            visual.localRotation=restRotation*Quaternion.Euler(8*weight,0,12*sway);
        }
    }
    public void SamplePortalSpringFlight(float progress){
        float p=Mathf.Clamp01(progress),flail=Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.24f));
        float phase=p*Mathf.PI*7;
        // Uneven attempts to regain balance continue until the actor disappears into the hole.
        SamplePortalLimbs(flail*(-65+60*Mathf.Sin(phase)),flail*(-55+65*Mathf.Sin(phase*.83f+2)),
            flail*(15+55*Mathf.Sin(phase*.78f+1)),flail*(5+60*Mathf.Sin(phase*.93f+3.4f)),
            flail*(46+8*Mathf.Sin(phase*.6f)),true);
        BlendPortalSprawl(1-flail);
        if(visual){
            float pitch=Mathf.Lerp(88,-30+38*Mathf.Sin(p*Mathf.PI*2.4f),flail);
            float yaw=flail*(22*Mathf.Sin(p*Mathf.PI*2.1f)+15*p);
            float roll=flail*(30*Mathf.Sin(p*Mathf.PI*3.2f)+18*p);
            var tumble=Quaternion.Euler(pitch,yaw,roll);
            visual.localRotation=restRotation*tumble;
            // Rotate around the torso in flight rather than swinging the whole body around its feet.
            var pivot=Vector3.up*.48f;
            visual.localPosition=restPosition+Vector3.up*(.17f*(1-flail))
                +restRotation*(pivot-tumble*pivot)*flail;
        }
    }
    public void SamplePortalMagnetPress(float compression){
        SamplePortalSpringPress(compression);
        if(visual)visual.localRotation=restRotation*Quaternion.Euler(Mathf.Lerp(-18,50,compression),7.6f*(1-compression),0);
    }
    public void SamplePortalSpringPress(float compression){
        float load=Mathf.Clamp01(compression);
        SamplePortalLimbs(-65-20*load,-65-20*load,42-12*load,-42+60*load,46-28*load,true);
        if(visual){
            visual.localPosition=restPosition;
            visual.localRotation=restRotation*Quaternion.Euler(Mathf.Lerp(-18,16,load),7.6f*(1-load),0);
        }
    }
    public void SamplePortalPanicReach(float progress,float reach){
        float phase=progress*Mathf.PI*6,panic=1-reach;
        SamplePortalLimbs(Mathf.Lerp(-65+65*Mathf.Sin(phase),-165,reach),
            Mathf.Lerp(-55+70*Mathf.Sin(phase+2),-165,reach),
            Mathf.Lerp(55*Mathf.Sin(phase+1),25,reach),Mathf.Lerp(60*Mathf.Sin(phase+3),25,reach),
            Mathf.Lerp(46,12,reach),true);
        if(visual){
            visual.localPosition=restPosition;
            visual.localRotation=restRotation*Quaternion.Euler(Mathf.Lerp(-15,8,reach),
                16*Mathf.Sin(phase*.5f)*panic,18*Mathf.Sin(phase)*panic);
        }
    }
    public void SamplePortalLedgeEffort(float load,float pull){
        // Tuck against the wall, then drive both arms down and kick the legs to launch.
        SamplePortalLimbs(Mathf.Lerp(-165,-35,pull),Mathf.Lerp(-165,-35,pull),
            Mathf.Lerp(25+42*load,-18,pull),Mathf.Lerp(25+32*load,-12,pull),12,true);
        if(visual){
            visual.localPosition=restPosition;
            visual.localRotation=restRotation*Quaternion.Euler(Mathf.Lerp(8+10*load,-16,pull),0,0);
        }
    }
    public void SamplePortalLedgeDive(float progress){
        float tuck=Mathf.Sin(progress*Mathf.PI);
        SamplePortalLimbs(-35-85*tuck,-35-85*tuck,Mathf.Lerp(-18,55,tuck),Mathf.Lerp(-12,48,tuck),12+10*tuck,true);
        if(visual){visual.localPosition=restPosition;visual.localRotation=restRotation*Quaternion.Euler(-16+65*tuck,0,0);}
    }
    public void SamplePortalLedge(float hang,float wobble){
        float sway=Mathf.Sin(wobble*Mathf.PI*5)*Mathf.Sin(wobble*Mathf.PI);
        SamplePortalLimbs(Mathf.Lerp(-55-25*sway,-165,hang),Mathf.Lerp(-55+25*sway,-165,hang),25*hang,25*hang);
        if(visual){
            visual.localPosition=restPosition;
            visual.localRotation=restRotation*Quaternion.Euler(8*hang,0,18*sway*(1-hang));
        }
    }
    void SamplePortalLimbs(float leftArm,float rightArm,float leftLeg,float rightLeg,float armSpread=22,bool outwardArms=false){
        if(!posed)return;
        for(int i=0;i<vertices.Length;i++){
            float side=Mathf.Sign(vertices[i].x);bool limb=arms[i]||legs[i];
            var pivot=arms[i]?new Vector3(side*.235f,.635f,0):new Vector3(side*.097f,.34f,0);
            float angle=arms[i]?(side<0?leftArm:rightArm):(side<0?leftLeg:rightLeg);
            // With arms hanging below the shoulder, positive Z opens the right arm.
            // Flight spreads outward before pitching, keeping the hands clear of the chest.
            var turn=Quaternion.Euler(angle,0,arms[i]?side*(outwardArms?armSpread:-armSpread):0);
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
