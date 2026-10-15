using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    bool RingCanEnter(MagnetPiece ring,Vector2Int from,float height,Vector2Int cell,out float landing){
        return TravelSurface(from,height,cell,ring,out landing)
            &&!TransportObstacle(cell,ring)&&cell!=PlayerCell;
    }
    bool BeginPushRing(MagnetPiece ring,Vector2Int dir,Vector2Int origin){
        if(!Supported(ring))return Reject("边缘外的磁铁无法直接推动");
        var portal=BlackHolePortal.At(this,origin+dir);if(portal)return BeginPortalPush(ring,portal,dir);
        bool flat=MagnetPiece.FlatU(ring.Pose);
        Vector3 forward=new Vector3(dir.x,0,dir.y);
        // A push along the wheel axle tips the upright ring onto its flat face.
        // A push along the circumference keeps it upright and rolling.
        bool tip=flat||Mathf.Abs(Vector3.Dot(ring.Pose*Vector3.up,forward))>.95f;
        float height;
        if(!RingCanEnter(ring,origin,ring.transform.position.y,origin+dir,out height)){
            Busy=true;StartMovement(BlockedRingShove(ring,dir));LastRule="圆环受阻，推一下后回弹";return true;
        }
        int cells=1;
        // Roll off the last supported cell, then hover at the first gap.
        if(!tip)while(cells<tiles.Length+magnets.Length*2){
            var cell=origin+dir*cells;float nextHeight;
            if(BlackHolePortal.At(this,cell))break;
            if(!(Floor(cell)||Deck(cell,ring))||!RingCanEnter(ring,cell,height,cell+dir,out nextHeight))break;
            height=nextHeight;cells++;
        }
        Busy=true;StartMovement(MoveRing(ring,dir,origin,cells,tip));
        LastRule=!Tile(origin+dir*cells)?"圆环离岛后放平，板面与岸边齐平，可从四个方向行走":flat?"圆环向前一格并推立":tip?"圆环向推动方向倒下一格，恢复平躺":"圆环连续滚动，在障碍前停下";
        return true;
    }
    IEnumerator MoveRing(MagnetPiece ring,Vector2Int dir,Vector2Int origin,int cells,bool tip){
        BeginPushInterrupt(dir);
        // Keep the shared push interruption and visible catch-up path available.
        pushHoverParticipants.Add(ring);
        Vector3 start=ring.transform.position;
        var path=new Vector3[cells+1];path[0]=start;
        for(int i=1;i<=cells;i++){
            float height;TravelSurface(origin+dir*(i-1),path[i-1].y,origin+dir*i,ring,out height);
            path[i]=Position(origin+dir*i,height);
        }
        Vector3 end=path[cells];
        Vector3 playerStart=player.position,playerEnd=PushPlayerEnd(origin,ring);
        Vector3 forward=new Vector3(dir.x,0,dir.y),axis=new Vector3(dir.y,0,-dir.x);
        Quaternion pose=ring.Pose;
        // Grid rotation is deliberate: every cell swaps the two coloured halves.
        float degrees=tip?90:180f*cells;
        bool falling=tip&&!MagnetPiece.FlatU(pose);
        bool intoSpace=!Tile(origin+dir*cells)&&!Deck(origin+dir*cells,ring);
        bool slideOff=intoSpace&&tip&&MagnetPiece.FlatU(pose);
        if(slideOff)degrees=0;
        var rolledPose=Quaternion.AngleAxis(degrees,axis)*pose;
        var floatingPose=slideOff?pose:Quaternion.FromToRotation(rolledPose*Vector3.up,Vector3.up)*rolledPose;
        var slideBounds=AssemblyBounds(AssemblyBodies(ring.geometry));
        float slideRadius=Mathf.Abs(forward.x)*slideBounds.extents.x+Mathf.Abs(forward.z)*slideBounds.extents.z;
        // Cruise faster than even the player's capped rapid-tap walking speed.
        // Rotation and translation share one progress value, with no per-cell pause.
        float ringSpeed=WalkSpeedLimit*1.08f;
        // A short roll must leave time for the same full shove as a normal bar.
        // Account for the acceleration ramp when limiting its cruise speed.
        float shoveSeconds=Mathf.Max(.01f,barPushSeconds);
        float minimumCruise=shoveSeconds-.5f*Mathf.Min(.14f,shoveSeconds/1.5f);
        ringSpeed=Mathf.Min(ringSpeed,cellSize*cells/minimumCruise);
        float cruiseSeconds=cellSize*cells/ringSpeed;
        float accelerationSeconds=Mathf.Min(.14f,cruiseSeconds);
        float seconds=tip?Mathf.Max(.01f,barPushSeconds):cruiseSeconds+accelerationSeconds*.5f;
        if(tip){flowingPush=true;pushFlowProgress=0;if(pushFlowSpeed<=0)pushFlowSpeed=1/seconds;}
        float playerSeconds=shoveSeconds;
        bool releasedPush=false;
        ring.walkable=false;player.rotation=Quaternion.LookRotation(forward);
        bool lifting=tip&&!falling&&!slideOff;
        var style=lifting?PlayerPushPose.FlowPushStyle.LiftBar:PlayerPushPose.FlowPushStyle.ShoveBar;
        var pushPose=PlayerPushPose.BeginFlowPush(player,cellSize,style);
        float inheritedRock=TakeBarRock(ring,axis);
        // One continuous interpolation prevents a stop/start at every grid line.
        float elapsed=0;
        while(tip?pushFlowProgress<1:elapsed<seconds){
            float remainingTime=0;
            float dt=tip?AdvancePushFlow(1/seconds,out remainingTime):MovementDeltaTime;
            float sample=tip?pushFlowProgress*seconds:Mathf.Min(seconds,elapsed+dt);
            float progress=Mathf.Clamp01(sample/seconds);
            if(tip)progress=falling?progress*progress:progress*progress*(2.47f-1.47f*progress);
            else{
                // Integral of a smooth speed ramp (smoothstep), then constant cruise.
                float u=Mathf.Clamp01(sample/accelerationSeconds);
                float travelled=sample<accelerationSeconds
                    ?ringSpeed*accelerationSeconds*(u*u*u-.5f*u*u*u*u)
                    :ringSpeed*(sample-.5f*accelerationSeconds);
                progress=Mathf.Clamp01(travelled/(cellSize*cells));
            }
            float distance=progress*cells;int segment=Mathf.Min(cells-1,Mathf.FloorToInt(distance));
            ring.transform.position=Vector3.Lerp(path[segment],path[segment+1],distance-segment);
            ring.geometry.rotation=Quaternion.AngleAxis(degrees*progress,axis)*pose;
            MagnetVisuals.Ground(ring);
            if(slideOff){
                float travel=Vector3.Dot(ring.transform.position-start,forward);
                float exposed=Mathf.InverseLerp(cellSize*.5f-slideRadius,cellSize*.5f+slideRadius,travel);
                float tilt=24*Mathf.SmoothStep(0,1,exposed);
                var bounds=AssemblyBounds(AssemblyBodies(ring.geometry));
                float contact=Mathf.Clamp(cellSize*.5f-travel,-slideRadius,slideRadius);
                var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)+forward*contact;
                var turn=Quaternion.AngleAxis(tilt,axis);
                ring.geometry.position=pivot+turn*(ring.geometry.position-pivot);
                ring.geometry.rotation=turn*pose;
            }
            float pushProgress=tip?Mathf.Clamp01(sample/seconds):Mathf.Clamp01(sample/playerSeconds);
            float residual=inheritedRock*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(sample/.1f)));
            if(!intoSpace&&Mathf.Abs(residual)>.001f)ApplyBarRock(ring,axis,residual);
            var previous=player.position;
            player.position=Vector3.Lerp(playerStart,playerEnd,pushProgress);
            if(!releasedPush){
                pushPose.AdvanceFlowPush(Vector3.Distance(previous,player.position),dt,
                    lifting?pushProgress:ShoveBarPoseProgress(pushProgress));
                if(!tip&&pushProgress>=1){
                    pushPose.ReleaseFlowPush();releasedPush=true;
                    if(!recordingPush&&!finishingMovement&&!BlackHolePortal.At(this,origin+dir*cells)){
                        pendingRingRelease=new Vector2Int[cells];
                        for(int i=0;i<cells;i++)pendingRingRelease[i]=origin+dir*(i+1);
                    }
                }
            }
            if(tip&&pushFlowProgress>=1){pushFlowCarryTime=Mathf.Max(0,dt-remainingTime);break;}
            elapsed+=dt;
            if(!tip&&elapsed>=seconds)break;
            yield return null;
        }
        ring.transform.position=end;
        if(!slideOff){ring.geometry.rotation=rolledPose;MagnetVisuals.Ground(ring);}
        ring.walkable=MagnetPiece.FlatU(ring.Pose);
        if(intoSpace){
            pushPose.ReleaseFlowPush();
            yield return RingFloatAfterLeavingShore(ring,ring.Pose,floatingPose);
        }
        if(tip&&!intoSpace&&!BlackHolePortal.At(this,origin+dir*cells)&&!recordingPush&&!finishingMovement){
            if(falling)StartBarTouchdown(ring,axis,0);
            else StartBarRock(ring,axis);
        }
        float ignoredHeight;
        if(!tip&&!intoSpace&&!BlackHolePortal.At(this,origin+dir*cells)&&!BlackHolePortal.At(this,origin+dir*(cells+1))
            &&!RingCanEnter(ring,origin+dir*cells,end.y,origin+dir*(cells+1),out ignoredHeight))
            yield return RingStoneRebound(ring,forward,axis,ringSpeed);
        yield return SettleMagnetArrival(ring,path[cells-1]);
        EndPushInterrupt();player.position=playerEnd;NotifyLanding(origin);Busy=false;
        continuePushOnComplete=tip&&!recordingPush&&!finishingMovement;
    }
    IEnumerator BlockedRingShove(MagnetPiece ring,Vector2Int dir){
        BeginPushInterrupt(dir,true);
        var forward=new Vector3(dir.x,0,dir.y);
        player.rotation=Quaternion.LookRotation(forward);
        var pose=PlayerPushPose.BeginFlowPush(player,cellSize,PlayerPushPose.FlowPushStyle.ShoveBar);
        yield return RingStoneRebound(ring,forward,new Vector3(dir.y,0,-dir.x),cellSize*2,pose);
        pose.ReleaseFlowPush();EndPushInterrupt();Busy=false;

    }
    IEnumerator RingFloatAfterLeavingShore(MagnetPiece ring,Quaternion upright,Quaternion flat){
        // Translation has fully cleared the bank before any sinking or flattening.
        float shore=ring.transform.position.y,initialTop=MagnetGeometryTop(ring);
        float submergedTop=shore-cellSize*.16f;
        const float sinkSeconds=.26f,turnSeconds=.30f,riseSeconds=.34f;
        float duration=sinkSeconds+turnSeconds+riseSeconds;
        ring.walkable=false;
        for(float elapsed=0;elapsed<duration;){
            elapsed=Mathf.Min(duration,elapsed+MovementDeltaTime);
            float turn=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-sinkSeconds)/turnSeconds));
            ring.geometry.rotation=Quaternion.Slerp(upright,flat,turn);
            MagnetVisuals.Ground(ring);
            float top;
            if(elapsed<sinkSeconds){float p=elapsed/sinkSeconds;top=Mathf.Lerp(initialTop,submergedTop,p*p);}
            else top=Mathf.Lerp(submergedTop,shore,Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-sinkSeconds-turnSeconds)/riseSeconds)));
            ring.geometry.position+=Vector3.up*(top-MagnetGeometryTop(ring));
            if(elapsed>=duration)break;
            yield return null;
        }
        ring.geometry.rotation=flat;MagnetVisuals.Ground(ring);
        ring.geometry.position+=Vector3.up*(shore-MagnetGeometryTop(ring));
        ring.walkable=true;
    }
    IEnumerator RingStoneRebound(MagnetPiece ring,Vector3 forward,Vector3 axis,float speed,PlayerPushPose shove=null){
        // Only the displayed body crosses the stopping point; occupancy and the
        // final coloured face remain the result of the validated grid roll.
        var position=ring.geometry.position;var rotation=ring.Pose;
        var ahead=Cell(ring.transform)+new Vector2Int(Mathf.RoundToInt(forward.x),Mathf.RoundToInt(forward.z));
        var obstacle=TransportObstacle(ahead,ring);var tile=Tile(ahead);
        var bounds=AssemblyBounds(AssemblyBodies(ring.geometry));
        float radius=Mathf.Abs(forward.x)*bounds.extents.x+Mathf.Abs(forward.z)*bounds.extents.z;
        float near=cellSize*.5f;
        if(obstacle){
            var other=AssemblyBounds(AssemblyBodies(obstacle.geometry));
            near=Vector3.Dot(other.center-bounds.center,forward)-Mathf.Abs(forward.x)*other.extents.x-Mathf.Abs(forward.z)*other.extents.z;
        }else if(tile){
            // Measure the actual raised obstacle meshes; ground tiles have no height.
            foreach(var filter in tile.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh)
                foreach(var vertex in filter.sharedMesh.vertices){
                    var world=filter.transform.TransformPoint(vertex);
                    if(world.y>ring.transform.position.y+.05f)near=Mathf.Min(near,Vector3.Dot(world-bounds.center,forward));
                }
        }
        float distance=Mathf.Clamp(near-radius-.015f,0,cellSize*.24f);
        float contactSeconds=2*distance/Mathf.Max(.01f,speed),returnSeconds=.24f;
        // A blocked shove and its response share one clock. Contact coincides
        // with the normal shove's strongest effort, rather than following it.
        if(shove){contactSeconds=Mathf.Max(.01f,barPushSeconds)*.5f;returnSeconds=contactSeconds;}
        float duration=contactSeconds+returnSeconds;
        try{
            for(float elapsed=0;elapsed<duration;){
                float dt=Mathf.Min(duration-elapsed,MovementDeltaTime);elapsed+=dt;
                if(shove)shove.AdvanceFlowPush(cellSize*dt/duration,dt,ShoveBarPoseProgress(elapsed/duration));
                float offset;
                if(elapsed<contactSeconds){float p=elapsed/contactSeconds;offset=distance*p*(2-p);}
                else{float p=(elapsed-contactSeconds)/returnSeconds;offset=distance*Mathf.Pow(1-p,3);}
                ring.geometry.rotation=Quaternion.AngleAxis(180*offset/cellSize,axis)*rotation;
                MagnetVisuals.Ground(ring);
                ring.geometry.position+=forward*offset;
                if(elapsed>=duration)break;
                yield return null;
            }
        }finally{
            if(ring&&ring.geometry){ring.geometry.SetPositionAndRotation(position,rotation);}
        }
    }
}
}
