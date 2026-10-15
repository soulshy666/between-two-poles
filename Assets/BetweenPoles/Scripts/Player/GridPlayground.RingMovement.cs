using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    bool RingCanEnter(MagnetPiece ring,Vector2Int from,float height,Vector2Int cell,out float landing){
        // Only a settled flat space ring acts as terrain. All other magnetic
        // bodies, including walkable bridges and wide bars, stop a rolling ring.
        foreach(var other in magnets){
            if(!other||other==ring||!other.enabled||!other.gameObject.activeInHierarchy||!other.Occupies(cell,cellSize))continue;
            bool floor=SpaceRing(other)&&other.walkable&&MagnetPiece.FlatU(other.Pose)
                &&!MagnetStillAnimating(other)&&SameHeight(DeckHeight(other),height);
            if(!floor){landing=height;return false;}
        }
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
        rollingRingShove=!tip;
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
        bool detached=false;
        ring.walkable=false;player.rotation=Quaternion.LookRotation(forward);
        bool lifting=tip&&!falling&&!slideOff;
        var style=lifting?PlayerPushPose.FlowPushStyle.LiftBar:PlayerPushPose.FlowPushStyle.ShoveBar;
        var pushPose=PlayerPushPose.BeginFlowPush(player,cellSize,style);
        float inheritedRock=TakeBarRock(ring,axis);
        // One continuous interpolation prevents a stop/start at every grid line.
        float elapsed=0;
        while(tip?pushFlowProgress<1:elapsed<seconds){
            float remainingTime=0;
            float dt=tip?AdvancePushFlow(1/seconds,out remainingTime):detached?Time.deltaTime:MovementDeltaTime;
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
            if(!detached)player.position=Vector3.Lerp(playerStart,playerEnd,pushProgress);
            if(!releasedPush){
                pushPose.AdvanceFlowPush(Vector3.Distance(previous,player.position),dt,
                    lifting?pushProgress:ShoveBarPoseProgress(pushProgress));
                if(!tip&&pushProgress>=1){
                    pushPose.ReleaseFlowPush();releasedPush=true;
                    if(!recordingPush&&!finishingMovement&&!BlackHolePortal.At(this,origin+dir*cells)){
                        pendingRingRelease=PrepareLiveRing(ring,origin,dir,cells,end,intoSpace?floatingPose:rolledPose,intoSpace);
                        detached=true;
                        EndPushInterrupt();player.position=playerEnd;NotifyLanding(origin);Busy=false;
                    }
                }
            }
            if(tip&&pushFlowProgress>=1){pushFlowCarryTime=Mathf.Max(0,dt-remainingTime);break;}
            elapsed+=dt;
            // Even a short roll must yield once so the driver can transfer ownership.
            if(detached&&pendingRingRelease!=null){yield return null;continue;}
            if(!tip&&elapsed>=seconds)break;
            yield return null;
        }
        ring.transform.position=end;
        if(!slideOff){ring.geometry.rotation=rolledPose;MagnetVisuals.Ground(ring);}
        ring.walkable=MagnetPiece.FlatU(ring.Pose);
        if(intoSpace){
            if(!detached)pushPose.ReleaseFlowPush();
            yield return RingFloatAfterLeavingShore(ring,ring.Pose,floatingPose,detached);
        }
        if(tip&&!intoSpace&&!BlackHolePortal.At(this,origin+dir*cells)&&!recordingPush&&!finishingMovement){
            if(falling)StartBarTouchdown(ring,axis,0);
            else StartBarRock(ring,axis);
        }
        float ignoredHeight;
        if(!tip&&!intoSpace&&!BlackHolePortal.At(this,origin+dir*cells)&&!BlackHolePortal.At(this,origin+dir*(cells+1))
            &&!RingCanEnter(ring,origin+dir*cells,end.y,origin+dir*(cells+1),out ignoredHeight))
            yield return RingStoneRebound(ring,forward,axis,ringSpeed,realtime:detached,transferMomentum:true);
        if(detached)yield break;
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
    IEnumerator RingFloatAfterLeavingShore(MagnetPiece ring,Quaternion upright,Quaternion flat,bool realtime=false){
        // Translation has fully cleared the bank before any sinking or flattening.
        float shore=ring.transform.position.y,initialTop=MagnetGeometryTop(ring);
        float submergedTop=shore-cellSize*.16f;
        const float sinkSeconds=.26f,turnSeconds=.30f,riseSeconds=.34f;
        float duration=sinkSeconds+turnSeconds+riseSeconds;
        ring.walkable=false;
        for(float elapsed=0;elapsed<duration;){
            elapsed=Mathf.Min(duration,elapsed+(realtime?Time.deltaTime:MovementDeltaTime));
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
    IEnumerator RingStoneRebound(MagnetPiece ring,Vector3 forward,Vector3 axis,float speed,PlayerPushPose shove=null,bool realtime=false,bool transferMomentum=false){
        // Only the displayed body crosses the stopping point; occupancy and the
        // final coloured face remain the result of the validated grid roll.
        var position=ring.geometry.position;var rotation=ring.Pose;
        var ahead=Cell(ring.transform)+new Vector2Int(Mathf.RoundToInt(forward.x),Mathf.RoundToInt(forward.z));
        var obstacle=Piece(ahead,ring);if(!obstacle)obstacle=TransportObstacle(ahead,ring);var tile=Tile(ahead);
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
        bool contacted=false;
        try{
            for(float elapsed=0;elapsed<duration;){
                float dt=Mathf.Min(duration-elapsed,realtime?Time.deltaTime:MovementDeltaTime);elapsed+=dt;
                if(shove)shove.AdvanceFlowPush(cellSize*dt/duration,dt,ShoveBarPoseProgress(elapsed/duration));
                float offset;
                if(elapsed<contactSeconds){float p=elapsed/contactSeconds;offset=distance*p*(2-p);}
                else{float p=(elapsed-contactSeconds)/returnSeconds;offset=distance*Mathf.Pow(1-p,3);}
                ring.geometry.rotation=Quaternion.AngleAxis(180*offset/cellSize,axis)*rotation;
                MagnetVisuals.Ground(ring);
                ring.geometry.position+=forward*offset;
                if(!contacted&&elapsed>=contactSeconds){
                    contacted=true;
                    if(transferMomentum&&!recordingPush&&!finishingMovement)
                        TryTransferRingMomentum(ring,obstacle,new Vector2Int(Mathf.RoundToInt(forward.x),Mathf.RoundToInt(forward.z)),speed);
                }
                if(elapsed>=duration)break;
                yield return null;
            }
        }finally{
            if(ring&&ring.geometry){ring.geometry.SetPositionAndRotation(position,rotation);}
        }
    }
    void TryTransferRingMomentum(MagnetPiece source,MagnetPiece target,Vector2Int direction,float speed){
        var forward=new Vector3(direction.x,0,direction.y);
        if(target&&target.product==MagnetProduct.None&&!target.combined&&target.shape==MagnetShape.Bar
            &&MagnetPiece.VerticalBar(target.Pose)){
            TryToppleBarFromRing(source,target,direction);
            return;
        }
        if(!target||target.product!=MagnetProduct.Ring||MagnetPiece.FlatU(target.Pose)
            ||MagnetStillAnimating(target)||!Supported(target)
            ||!SameHeight(source.transform.position.y,target.transform.position.y))return;
        if(Mathf.Abs(Vector3.Dot(target.Pose*Vector3.up,forward))>.95f){
            TryToppleBarFromRing(source,target,direction);
            return;
        }
        if(Mathf.Abs(Vector3.Dot(target.Pose*Vector3.up,forward))>.05f)return;
        var origin=Cell(target.transform);
        var path=new System.Collections.Generic.List<Vector3>{target.transform.position};
        float height=target.transform.position.y;
        // Portal entry needs its own cargo transaction; keep this transfer on the board.
        for(int i=1;i<=tiles.Length+magnets.Length*2;i++){
            var previous=origin+direction*(i-1);var next=origin+direction*i;float landing;
            if(BlackHolePortal.At(this,next)||!RingCanEnter(target,previous,height,next,out landing))break;
            path.Add(Position(next,landing));height=landing;
            if(!Floor(next)&&!Deck(next,target))break;
        }
        int cells=path.Count-1;
        if(cells==0){
            // A blocked receiver still reacts to impact, without changing its grid
            // occupancy or propagating momentum through the obstacle behind it.
            var reaction=new LiveRing{piece=target,destination=SavePiece(target),direction=direction};
            reaction.route.Add(origin);liveRings.Add(reaction);
            var blockedActions=new System.Collections.Generic.Stack<IEnumerator>();
            blockedActions.Push(RingStoneRebound(target,forward,new Vector3(direction.y,0,-direction.x),speed,realtime:true));
            reaction.playback=StartCoroutine(ContinueLiveRing(reaction,blockedActions));
            return;
        }
        var pose=target.Pose;var axis=new Vector3(direction.y,0,-direction.x);
        var endPose=Quaternion.AngleAxis(180*cells,axis)*pose;
        bool space=!Tile(origin+direction*cells)&&!Deck(origin+direction*cells,target);
        var flat=Quaternion.FromToRotation(endPose*Vector3.up,Vector3.up)*endPose;
        var flight=PrepareLiveRing(target,origin,direction,cells,path[cells],space?flat:endPose,space);
        var actions=new System.Collections.Generic.Stack<IEnumerator>();
        actions.Push(RollTransferredRing(target,direction,path.ToArray(),pose,endPose,flat,space,speed));
        target.walkable=false;
        flight.playback=StartCoroutine(ContinueLiveRing(flight,actions));
    }
    void TryToppleBarFromRing(MagnetPiece source,MagnetPiece bar,Vector2Int direction){
        if(MagnetStillAnimating(bar)||!Supported(bar)
            ||!SameHeight(source.transform.position.y,bar.transform.position.y))return;
        var origin=Cell(bar.transform);var next=origin+direction;
        var start=bar.transform.position;var pose=bar.Pose;
        var axis=new Vector3(direction.y,0,-direction.x);
        float landing=start.y;
        bool canFall=next!=PlayerCell&&!BlackHolePortal.At(this,next)&&!Piece(next,bar)
            &&TravelSurface(origin,start.y,next,bar,out landing)&&!TransportObstacle(next,bar);
        LiveRing reaction;
        var actions=new System.Collections.Generic.Stack<IEnumerator>();
        if(canFall){
            var end=Position(next,landing);var endPose=Quaternion.AngleAxis(90,axis)*pose;
            bool isRing=bar.product==MagnetProduct.Ring;
            bool space=isRing&&!Tile(next)&&!Deck(next,bar);
            reaction=PrepareLiveRing(bar,origin,direction,1,end,endPose,space);
            // Reuse the independent impact playback/undo lifecycle, not ring walkability.
            reaction.destination.walkable=isRing;
            reaction.route.Add(origin);
            actions.Push(AnimateRingBarImpact(bar,start,end,pose,axis,true));
        }else{
            reaction=new LiveRing{piece=bar,destination=SavePiece(bar),direction=direction};
            reaction.route.Add(origin);liveRings.Add(reaction);
            actions.Push(AnimateRingBarImpact(bar,start,start,pose,axis,false));
        }
        reaction.playback=StartCoroutine(ContinueLiveRing(reaction,actions));
    }
    IEnumerator AnimateRingBarImpact(MagnetPiece bar,Vector3 start,Vector3 end,Quaternion pose,Vector3 axis,bool falling){
        float seconds=falling?Mathf.Max(.3f,barPushSeconds):.48f;
        for(float elapsed=0;elapsed<seconds;){
            elapsed=Mathf.Min(seconds,elapsed+Time.deltaTime);
            float p=elapsed/seconds;
            float angle;
            if(falling){
                float fall=p*p;
                angle=90*fall;
                bar.transform.position=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,fall));
            }else{
                // First lean with the impact, then swing back with decaying amplitude.
                angle=7*Mathf.Sin(p*Mathf.PI*3)*(1-p)*(1-p);
            }
            bar.geometry.rotation=Quaternion.AngleAxis(angle,axis)*pose;
            MagnetVisuals.Ground(bar);
            yield return null;
        }
        bar.transform.position=end;
        bar.geometry.rotation=falling?Quaternion.AngleAxis(90,axis)*pose:pose;
        MagnetVisuals.Ground(bar);
        if(falling&&bar.product==MagnetProduct.Ring&&SpaceRing(bar)&&!Deck(Cell(bar.transform),bar)){
            var flat=bar.Pose;
            yield return RingFloatAfterLeavingShore(bar,flat,flat,true);
        }
    }
    IEnumerator RollTransferredRing(MagnetPiece ring,Vector2Int direction,Vector3[] path,Quaternion pose,Quaternion endPose,Quaternion flat,bool space,float speed){
        int cells=path.Length-1;var axis=new Vector3(direction.y,0,-direction.x);
        float seconds=cells*cellSize/Mathf.Max(.01f,speed);
        for(float elapsed=0;elapsed<seconds;){
            elapsed=Mathf.Min(seconds,elapsed+Time.deltaTime);
            float distance=elapsed/seconds*cells;int segment=Mathf.Min(cells-1,Mathf.FloorToInt(distance));
            ring.transform.position=Vector3.Lerp(path[segment],path[segment+1],distance-segment);
            ring.geometry.rotation=Quaternion.AngleAxis(180*distance,axis)*pose;MagnetVisuals.Ground(ring);
            yield return null;
        }
        ring.transform.position=path[cells];ring.geometry.rotation=endPose;MagnetVisuals.Ground(ring);
        if(space)yield return RingFloatAfterLeavingShore(ring,endPose,flat,true);
        else{
            float height;var end=Cell(ring.transform);
            if(!RingCanEnter(ring,end,path[cells].y,end+direction,out height))
                yield return RingStoneRebound(ring,new Vector3(direction.x,0,direction.y),axis,speed,realtime:true,transferMomentum:true);
        }
    }
}
}
