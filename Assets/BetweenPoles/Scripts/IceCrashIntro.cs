using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
[DisallowMultipleComponent]
public sealed class IceCrashIntro:MonoBehaviour {
    public GameObject shuttlePrefab;
    public bool playOnStart=true;
    [Min(.2f)] public float colorRevealSeconds=2.6f;
    public const float RevealBirth=.20f,RevealHold=.65f,RevealTail=.65f;
    float revealAge=-1;
    public float ColorRevealAge {get{return revealAge;}}
    public bool FreezeOpeningFrame {get{return Playing&&revealAge>=0&&revealAge<RevealBirth+RevealHold;}}
    public bool ColorRevealActive {get{return Playing&&revealAge<RevealBirth+RevealHold+colorRevealSeconds+RevealTail;}}
    public float ColorRevealProgress {get{return Mathf.Clamp01((revealAge-RevealBirth-RevealHold)/Mathf.Max(.2f,colorRevealSeconds));}}
    public const float ApproachTime=2f,OrbitTime=2f,DiveTime=4f,ImpactTime=5f,EjectEnd=5.95f,EndTime=13.1f;
    public bool Playing {get;private set;}
    public float PlaybackTime {get;private set;}
    public Vector3 CrashPoint {get;private set;}
    public Vector3 LandingPoint {get;private set;}
    GridPlayground board;PlayerPushPose pose;Transform island,ship,effects,pilotSeat,originalParent;
    Vector3 originalScale,ejectionOrigin;bool pilotAttached;
    Renderer[] actorRenderers,shipRenderers;bool[] actorVisible;
    MaterialPropertyBlock flightBlock;GridTile wreckTile;bool previousBlocked;
    public bool BlocksCell(Vector2Int cell){return impacted&&wreckTile&&Cell(wreckTile)==cell;}
    readonly List<Transform> flames=new List<Transform>(),smoke=new List<Transform>(),chips=new List<Transform>();
    Material smokeMaterial,iceMaterial;Vector3 originalPosition;Quaternion originalRotation;
    bool ready,impacted,finished;float smokeClock;
    void Awake(){
        board=GetComponent<GridPlayground>();
        if(!playOnStart||!board||gameObject.scene.name!="Chapter01_IceWorld"||ChapterAtlas.DestinationIsland>0||ChapterRecoilTravel.Active)return;
        Playing=true;board.SetOpeningCinematic(true);
        foreach(var world in FindObjectsOfType<PixelWorldCamera>())if(world.gameObject.scene==gameObject.scene)world.SetOpeningReveal(this);
    }
    IEnumerator Start(){
        if(!Playing)yield break;
        // Let the imported room window and camera select their initial island first.
        yield return null;yield return null;
        if(!Setup()){Playing=false;board.SetOpeningCinematic(false);yield break;}
        ready=true;Sample(0);
    }
    bool Setup(){
        originalPosition=board.player.position;originalRotation=board.player.rotation;
        originalParent=board.player.parent;originalScale=board.player.localScale;
        var spawn=board.TileAt(board.PlayerCell);var anchor=spawn?spawn.GetComponentInParent<IslandSurfaceAnchor>():null;
        island=anchor?anchor.center:null;if(!island)return false;
        GridTile crash=null,land=null;
        foreach(var t in board.tiles){
            if(!t||t.blocked||Owner(t)!=island||board.MagnetAt(Cell(t)))continue;
            if(!crash||t.transform.position.x<crash.transform.position.x-.01f||Mathf.Abs(t.transform.position.x-crash.transform.position.x)<.01f&&Mathf.Abs(t.transform.position.z-originalPosition.z)<Mathf.Abs(crash.transform.position.z-originalPosition.z))crash=t;
        }
        if(!crash)return false;
        float best=float.PositiveInfinity;
        foreach(var t in board.tiles){
            if(!t||t.blocked||Owner(t)!=island||board.MagnetAt(Cell(t))||t.GetComponentInChildren<BlackHolePortal>(true)||t.transform.position.x<=crash.transform.position.x+.1f)continue;
            float score=(t.transform.position-crash.transform.position).sqrMagnitude+Mathf.Abs(t.transform.position.z-crash.transform.position.z)*5;
            if(score<best){best=score;land=t;}
        }
        if(!land){Debug.LogWarning("Ice crash intro skipped: no clear landing cell to the right.",this);return false;}
        wreckTile=crash;previousBlocked=crash.blocked;
        CrashPoint=new Vector3(crash.transform.position.x,crash.surfaceHeight,crash.transform.position.z);
        LandingPoint=new Vector3(land.transform.position.x,land.surfaceHeight,land.transform.position.z);
        ship=(shuttlePrefab?Instantiate(shuttlePrefab):CrashShuttleModel.Create()).transform;ship.name="开场坠毁飞船（残骸）";ship.SetParent(transform,true);ship.localScale=Vector3.one*board.cellSize*.60f;
        foreach(var t in ship.GetComponentsInChildren<Transform>())if(t.name=="Thruster flame")flames.Add(t);
        var binding=ship.gameObject.AddComponent<IslandSurfaceAnchor>();binding.center=island;binding.Apply();
        effects=new GameObject("飞船冒烟与碎冰").transform;effects.SetParent(transform,false);
        smokeMaterial=Material("Wreck smoke",new Color(.22f,.25f,.28f));iceMaterial=Material("Crash ice chips",new Color(.62f,.86f,.93f));
        for(int i=0;i<16;i++)smoke.Add(Particle("Smoke puff",smokeMaterial));
        for(int i=0;i<9;i++)chips.Add(Particle("Ice chip",iceMaterial));
        binding=effects.gameObject.AddComponent<IslandSurfaceAnchor>();binding.center=island;binding.Apply();
        var window=board.GetComponent<FiveIslandWindow>();
        if(window)foreach(var room in window.rooms)if(room.center==island){var list=new List<Renderer>(room.surfaces);list.AddRange(ship.GetComponentsInChildren<Renderer>());list.AddRange(effects.GetComponentsInChildren<Renderer>());room.surfaces=list.ToArray();break;}
        actorRenderers=board.player.GetComponentsInChildren<Renderer>(true);actorVisible=new bool[actorRenderers.Length];for(int i=0;i<actorRenderers.Length;i++){actorVisible[i]=actorRenderers[i].enabled;actorRenderers[i].enabled=false;}
        pilotSeat=new GameObject("驾驶舱座位").transform;pilotSeat.SetParent(ship,false);
        pilotSeat.localPosition=new Vector3(0,-.34f,.26f);
        shipRenderers=ship.GetComponentsInChildren<Renderer>(true);
        pose=PlayerPushPose.CrashPose(board.player);return true;
    }
    Transform Owner(GridTile t){var a=t.GetComponentInParent<IslandSurfaceAnchor>();return a?a.center:null;}
    Vector2Int Cell(GridTile t){return new Vector2Int(Mathf.RoundToInt(t.transform.position.x/board.cellSize),Mathf.RoundToInt(t.transform.position.z/board.cellSize));}
    static Material Material(string name,Color color){var m=new Material(Shader.Find("BetweenPoles/PaintedIceTrial")){name=name,color=color};m.SetFloat("_Grid",0);m.SetFloat("_Painted",0);m.SetFloat("_Snow",0);return m;}
    Transform Particle(string name,Material material){
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(effects,false);g.transform.localScale=Vector3.zero;
        var c=g.GetComponent<Collider>();c.enabled=false;Destroy(c);
        var r=g.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.localBounds=new Bounds(Vector3.zero,Vector3.one*300);return g.transform;
    }
    void Update(){
        if(!ready)return;
        if(Playing){
            float step=Mathf.Min(Time.deltaTime,.05f);
            bool hold=FreezeOpeningFrame;
            if(revealAge>=0)revealAge+=step;
            if(!hold){
                float next=PlaybackTime+step;
                if(PlaybackTime<ImpactTime&&next>=ImpactTime)next=ImpactTime;
                PlaybackTime=next;Sample(PlaybackTime);
                if(PlaybackTime>=EndTime)Finish();
            }
        }
        else if(finished){smokeClock+=Time.deltaTime;Smoke(smokeClock);}
    }
    static float Ease(float x){return Mathf.SmoothStep(0,1,Mathf.Clamp01(x));}
    public void Sample(float time){
        if(!ready)return;
        if(time<ImpactTime)revealAge=-1;else if(revealAge<0)revealAge=0;
        float unit=board.cellSize;
        Vector3 impactPosition;Quaternion impactRotation;FallPose(ImpactTime-DiveTime,out impactPosition,out impactRotation);
        if(time<DiveTime){
            Vector3 point;Quaternion rotation;FlightPose(time,out point,out rotation);
            ship.SetPositionAndRotation(point,rotation);
        }else if(time<ImpactTime){
            Vector3 point;Quaternion rotation;FallPose(time-DiveTime,out point,out rotation);
            ship.SetPositionAndRotation(point,rotation);
        }else{
            if(!impacted){impacted=true;wreckTile.blocked=true;SetActorVisible(true);}
            float age=time-ImpactTime;
            ship.position=impactPosition+Vector3.up*(Mathf.Sin(age*28)*Mathf.Exp(-age*12)*.09f);
            ship.rotation=impactRotation*Quaternion.Euler(0,0,Mathf.Sin(age*32)*Mathf.Exp(-age*10)*8);
            Smoke(age);smokeClock=age;
            for(int i=0;i<chips.Count;i++){
                float a=i*2.399963f;var velocity=new Vector3(Mathf.Cos(a),1.2f+(i%3)*.35f,Mathf.Sin(a))*unit;
                var p=CrashPoint+velocity*age+Vector3.down*(age*age*3);
                p.y=Mathf.Max(CrashPoint.y+.015f,p.y);chips[i].position=p;
                chips[i].rotation=Quaternion.Euler(i*37+age*70,i*53,age*60);
                chips[i].localScale=Vector3.one*unit*.065f*(1-Ease((age-.5f)/.8f));
            }
        }
        SetSpaceFrame(shipRenderers,1,time<ImpactTime);
        float actorRigid=1-Ease((time-(EjectEnd-.30f))/.30f);
        SetSpaceFrame(actorRenderers,actorRigid,time<EjectEnd);
        foreach(var f in flames){f.gameObject.SetActive(time<ImpactTime);if(time<ImpactTime){var scale=f.localScale;float power=1-Ease((time-DiveTime)/.3f);scale.z=(.22f+.08f*Mathf.Sin(time*30))*power;f.localScale=scale;}}
        if(time<ImpactTime){
            SetActorVisible(true);AttachPilot();pose.SampleCrashSeat();return;
        }
        if(pilotAttached){ejectionOrigin=board.player.position;DetachPilot();}
        var face=Quaternion.LookRotation(Vector3.right);
        if(time<EjectEnd){
            float u=Mathf.Clamp01((time-ImpactTime)/.95f);
            var eject=ejectionOrigin;
            board.player.position=Vector3.Lerp(eject,LandingPoint,u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*unit*.55f);
            board.player.rotation=face*Quaternion.Euler(0,0,Mathf.Sin(u*Mathf.PI)*55);
            pose.SampleCrashPose(Ease(u),.3f+.5f*Mathf.Sin(u*Mathf.PI));
        }else{
            board.player.position=LandingPoint;board.player.rotation=face;
            float recovery=time-EjectEnd;
            float pushUp=Ease((recovery-.85f)/1.75f);
            float stand=Ease((recovery-4.4f)/2.3f);
            float support=Ease((recovery-.65f)/.65f)*(1-stand);
            float headTouch=Ease((recovery-2.6f)/.55f)*(1-Ease((recovery-3.8f)/.6f));
            float rub=Mathf.Sin((recovery-2.6f)*7)*headTouch;
            pose.SampleInjuredRecovery((1-.65f*pushUp)*(1-stand),support,headTouch,rub);

        }
    }
    const float LiftFailureSeconds=.30f;
    // Integral of the diminishing turning force, and its second integral.
    // This gives continuous position, velocity AND acceleration at power loss.
    static void TurnIntegrals(float time,out float first,out float second){
        float q=LiftFailureSeconds,t=Mathf.Max(0,time);
        if(t<q){first=t-t*t*t/(q*q)+.5f*t*t*t*t/(q*q*q);second=.5f*t*t-t*t*t*t/(4*q*q)+t*t*t*t*t/(10*q*q*q);}
        else{first=q*.5f;second=q*t*.5f-.15f*q*q;}
    }
    void FlightParameters(out Vector3 release,out Vector3 velocity,out Vector3 acceleration,out float radius,out float gravity){
        radius=board.cellSize*.85f;gravity=board.cellSize*6f;
        float omega=2*Mathf.PI/OrbitTime;
        velocity=Vector3.right*(radius*omega);acceleration=Vector3.forward*(radius*omega*omega);
        float t=ImpactTime-DiveTime,f,j;TurnIntegrals(t,out f,out j);
        Vector3 contact=CrashPoint+new Vector3(-.12f,board.cellSize*.68f,0);
        // Position the preceding orbit so its inertial fall meets the shore.
        // During the fall no steering, target interpolation or homing force is used.
        release=contact-velocity*t-acceleration*j+Vector3.up*gravity*(.5f*t*t-j);
    }
    void FallPose(float time,out Vector3 point,out Quaternion rotation){
        Vector3 release,velocity,acceleration;float radius,gravity;
        FlightParameters(out release,out velocity,out acceleration,out radius,out gravity);
        float f,j;TurnIntegrals(time,out f,out j);
        point=release+velocity*time+acceleration*j+Vector3.down*gravity*(.5f*time*time-j);
        Vector3 tangent=velocity+acceleration*f+Vector3.down*gravity*(time-f);
        float bank=Mathf.Atan2(acceleration.magnitude,gravity)*Mathf.Rad2Deg*(1-Ease(time/LiftFailureSeconds));
        rotation=Quaternion.LookRotation(tangent,Vector3.up)*Quaternion.Euler(0,0,bank);
    }
    void FlightPose(float time,out Vector3 point,out Quaternion rotation){
        Vector3 release,velocity,acceleration;float radius,gravity;
        FlightParameters(out release,out velocity,out acceleration,out radius,out gravity);
        float bank=Mathf.Atan2(acceleration.magnitude,gravity)*Mathf.Rad2Deg;
        if(time<ApproachTime){
            float u=Mathf.Clamp01(time/ApproachTime),T=ApproachTime;
            Vector3 start=release-velocity*(T*1.1f)+Vector3.up*board.cellSize;
            Vector3 initialVelocity=velocity+Vector3.down*(board.cellSize/T);
            Vector3 delta=release-start,v0=initialVelocity*T,v1=velocity*T,a1=acceleration*T*T;
            // Quintic Hermite joins the orbit with matching tangent and centripetal acceleration.
            Vector3 c3=10*delta-6*v0-4*v1+.5f*a1;
            Vector3 c4=-15*delta+8*v0+7*v1-a1;
            Vector3 c5=6*delta-3*v0-3*v1+.5f*a1;
            point=start+v0*u+c3*u*u*u+c4*u*u*u*u+c5*u*u*u*u*u;
            Vector3 tangent=(v0+3*c3*u*u+4*c4*u*u*u+5*c5*u*u*u*u)/T;
            rotation=Quaternion.LookRotation(tangent,Vector3.up)*Quaternion.Euler(0,0,bank*Ease(u));
            return;
        }
        float angle=-Mathf.PI*.5f+Mathf.Clamp01((time-ApproachTime)/OrbitTime)*Mathf.PI*2;
        Vector3 center=release+Vector3.forward*radius;
        point=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
        Vector3 direction=new Vector3(-Mathf.Sin(angle),0,Mathf.Cos(angle));
        rotation=Quaternion.LookRotation(direction,Vector3.up)*Quaternion.Euler(0,0,bank);
    }
    void SetSpaceFrame(Renderer[] renderers,float strength,bool airborne){
        if(renderers==null)return;if(flightBlock==null)flightBlock=new MaterialPropertyBlock();
        foreach(var r in renderers)if(r){r.GetPropertyBlock(flightBlock);flightBlock.SetFloat("_SpaceRigid",strength);flightBlock.SetFloat("_SpaceAirborne",airborne?1:0);flightBlock.SetVector("_SpaceAnchor",CrashPoint);r.SetPropertyBlock(flightBlock);}
    }
    void Smoke(float age){
        var vent=ship.TransformPoint(new Vector3(.10f,.08f,-.65f));
        for(int i=0;i<smoke.Count;i++){
            float birth=i*.115f;if(age<birth){smoke[i].localScale=Vector3.zero;continue;}
            float t=Mathf.Repeat(age-birth,2.5f),u=t/2.5f;
            smoke[i].position=vent+new Vector3(Mathf.Sin(t*1.7f+i*2.4f)*.08f+t*.065f,t*.48f,Mathf.Cos(i*2.4f)*.05f);
            smoke[i].rotation=Quaternion.Euler(i*31+t*12,i*47,25+t*8);
            smoke[i].localScale=Vector3.one*board.cellSize*(.065f+u*.17f)*(1-Ease((u-.7f)/.3f));
        }
    }
    void AttachPilot(){
        if(!pilotAttached){board.player.SetParent(pilotSeat,true);pilotAttached=true;}
        board.player.localPosition=Vector3.zero;board.player.localRotation=Quaternion.identity;SetCockpitClip(true);
    }
    void SetCockpitClip(bool enabled){
        if(actorRenderers==null||!ship)return;if(flightBlock==null)flightBlock=new MaterialPropertyBlock();
        var normal=ship.up;var rim=ship.TransformPoint(new Vector3(0,.30f,0));
        var plane=new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(normal,rim));
        foreach(var r in actorRenderers)if(r){r.GetPropertyBlock(flightBlock);flightBlock.SetFloat("_CockpitClip",enabled?1:0);flightBlock.SetVector("_CockpitPlane",plane);r.SetPropertyBlock(flightBlock);}
    }
    void DetachPilot(){
        if(!pilotAttached||!board||!board.player)return;
        SetCockpitClip(false);board.player.SetParent(originalParent,true);board.player.localScale=originalScale;pilotAttached=false;
    }
    void SetActorVisible(bool visible){if(actorRenderers==null)return;for(int i=0;i<actorRenderers.Length;i++)if(actorRenderers[i])actorRenderers[i].enabled=visible&&actorVisible[i];}
    void Finish(){
        DetachPilot();Playing=false;finished=true;pose.End();SetActorVisible(true);SetSpaceFrame(actorRenderers,0,false);
        board.player.position=LandingPoint;board.player.rotation=Quaternion.LookRotation(Vector3.right);
        board.FinishOpeningLanding();
    }
    void OnDisable(){
        DetachPilot();
        SetSpaceFrame(actorRenderers,0,false);
        if(Playing&&board){Playing=false;SetActorVisible(true);if(pose)pose.End();if(ready)board.player.SetPositionAndRotation(originalPosition,originalRotation);board.SetOpeningCinematic(false);}
    }
    void OnDestroy(){DetachPilot();if(wreckTile)wreckTile.blocked=previousBlocked;if(ship)Destroy(ship.gameObject);if(effects)Destroy(effects.gameObject);if(smokeMaterial)Destroy(smokeMaterial);if(iceMaterial)Destroy(iceMaterial);}
}
}
