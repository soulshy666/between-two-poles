using UnityEngine;
namespace BetweenPoles {
public sealed partial class PlayerPushPose {
    const float FirstIdleDelay=5f,RepeatIdleDelay=10f,IdleDuration=6f;
    float idleWait,idleElapsed;
    bool idlePlaying;
    bool hasStartedIdleSpacewalk;
    const float BreathingPeriod=3.2f,BreathingRise=.035f;
    float breathingElapsed;
    bool breathing;
    Vector3 idleRootPosition;
    Quaternion idleRootRotation;
    public static void EnsureIdle(Transform player){GetPose(player);}
    public void CancelIdle(){
        idleWait=0;
        if(idlePlaying)End();
    }
    void StopBreathing(){
        if(breathing)ApplyPose(0,0,false);
        breathing=false;breathingElapsed=0;
    }
    void OnApplicationFocus(bool focused){if(!focused){CancelIdle();StopBreathing();}}
    void UpdateIdle(){
        var panel=board?board.GetComponent<MagnetDebugPanel>():null;
        if(!Application.isPlaying||!Application.isFocused||!board||!board.isActiveAndEnabled
            ||board.Busy||board.OpeningCinematic||Time.timeScale<=0||pushing||walking||balancing||pushingStone
            ||(panel&&panel.enabled&&panel.IsOpen)||BlackHoleTravel.InTransit||BlackHoleTravel.Selecting
            ||ChapterRecoilTravel.Active){
            CancelIdle();StopBreathing();return;
        }
        bool input=Input.anyKey||Input.mouseScrollDelta.sqrMagnitude>0;
        if(input)CancelIdle();
        if(idlePlaying){
            if((transform.position-idleRootPosition).sqrMagnitude>.000001f
                ||Quaternion.Angle(transform.rotation,idleRootRotation)>.1f){CancelIdle();return;}
            idleElapsed+=Time.deltaTime;
            if(idleElapsed>=IdleDuration){End();return;}
            SampleIdleSpacewalk(idleElapsed);return;
        }
        if(!input)idleWait+=Time.deltaTime;
        float idleDelay=hasStartedIdleSpacewalk?RepeatIdleDelay:FirstIdleDelay;
        if(idleWait<idleDelay){
            Initialize();breathing=true;breathingElapsed+=Time.deltaTime;
            float blend=IdlePhase(breathingElapsed,0,.25f)*(1-IdlePhase(idleWait,idleDelay-.35f,idleDelay));
            SampleBreathing(breathingElapsed,blend);return;
        }
        StopBreathing();
        Initialize();idlePlaying=true;idleElapsed=0;
        // Input and animation completion reset the timer, but not the first-use flag.
        hasStartedIdleSpacewalk=true;
        idleRootPosition=transform.position;idleRootRotation=transform.rotation;
        SampleIdleSpacewalk(0);
    }
    static float IdlePhase(float time,float begin,float end){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(begin,end,time));}
    void SampleBreathing(float seconds,float weight){
        if(!visual)return;
        visual.localPosition=restPosition;visual.localRotation=restRotation;
        if(!posed)return;
        float breath=(1-Mathf.Cos(seconds*Mathf.PI*2/BreathingPeriod))*.5f*weight;
        float rise=BreathingRise*breath;
        for(int i=0;i<vertices.Length;i++){
            Vector3 v=vertices[i],n=i<normals.Length?normals[i]:Vector3.up;
            if(arms[i]){
                float side=Mathf.Sign(v.x);
                var shoulder=new Vector3(side*.235f,.635f,0);
                var q=Quaternion.Euler(-7f*breath,0,side*5f*breath);
                v=shoulder+q*(v-shoulder);n=q*n;
            }
            // Raise the upper body gently while keeping the soles planted.
            float support=Mathf.Clamp01(vertices[i].y/.34f);
            v.y+=rise*support;
            if(vertices[i].y>0&&vertices[i].y<.34f){n.y/=1+rise/.34f;n.Normalize();}
            workVertices[i]=v;if(i<normals.Length)workNormals[i]=n;
        }
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
    // Visual-only: a planted anticipation, weightless backflip, one-knee landing,
    // brief hold and recovery. No grid movement, Busy flag, landing event or sound.
    public void SampleIdleSpacewalk(float seconds){
        if(!visual)return;
        float t=Mathf.Clamp(seconds,0,IdleDuration);
        float crouch=IdlePhase(t,0,.30f)*(1-IdlePhase(t,.30f,.65f));
        float takeoff=IdlePhase(t,.30f,1.15f),descent=IdlePhase(t,3.8f,4.75f);
        float air=takeoff*(1-descent);
        float flip=IdlePhase(t,.85f,4.15f);
        float kneel=IdlePhase(t,4.15f,4.75f)*(1-IdlePhase(t,5.15f,6f));
        float stride=Mathf.Sin((t-.65f)*Mathf.PI*1.3f)*air;
        Quaternion tumble=Quaternion.Euler(-360*flip+14*crouch+18*kneel,0,0);
        visual.localRotation=restRotation*tumble;
        Vector3 pivot=Vector3.Scale(new Vector3(0,.46f,0),visual.localScale);
        visual.localPosition=restPosition+restRotation*(pivot-tumble*pivot)
            +Vector3.up*(1.25f*air-.055f*crouch);
        if(!posed)return;
        float lowest=float.PositiveInfinity;
        for(int i=0;i<vertices.Length;i++){
            float side=Mathf.Sign(vertices[i].x);Vector3 v=vertices[i],n=i<normals.Length?normals[i]:Vector3.up;
            if(arms[i]){
                float pitch=(-30+24*Mathf.Sin(t*2.2f+side))*air-32*kneel-18*crouch;
                var q=Quaternion.Euler(pitch,0,side*(32*air+10*kneel));
                var shoulder=new Vector3(side*.235f,.635f,0);
                v=shoulder+q*(v-shoulder);n=q*n;
            }else if(legs[i]){
                // A continuous knee weight avoids splitting coincident mesh vertices.
                float hipAngle=-32*crouch+36*stride*side+(side>0?-78:12)*kneel;
                float kneeAngle=45*crouch+(18+25*Mathf.Max(0,stride*side))*air+(side>0?88:100)*kneel;
                float lower=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.23f,v.y));
                var knee=new Vector3(side*.097f,.19f,0);
                var bend=Quaternion.Euler(kneeAngle*lower,0,0);
                v=knee+bend*(v-knee);n=bend*n;
                var hip=new Vector3(side*.097f,.34f,0);var q=Quaternion.Euler(hipAngle,0,0);
                v=hip+q*(v-hip);n=q*n;
            }
            workVertices[i]=v;if(i<normals.Length)workNormals[i]=n;
            if(kneel>0)lowest=Mathf.Min(lowest,transform.InverseTransformPoint(body.transform.TransformPoint(v)).y);
        }
        // Lower onto the supporting knee/foot, never through the support plane.
        if(kneel>0&&!float.IsInfinity(lowest))visual.localPosition+=Vector3.up*(restPosition.y-lowest)*kneel;
        posed.vertices=workVertices;if(normals.Length==vertices.Length)posed.normals=workNormals;
    }
}
}
