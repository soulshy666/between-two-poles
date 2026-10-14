using System.Collections;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    public struct ExitPlan {
        public Vector3 player,magnet,kicked;
        public bool kick,bounce,unstableLanding,failedExit,kickRebound;
        public MagnetPiece receiver;
        public MagnetPiece displaced;
        public MagnetPiece exitKickTarget;
        public Vector3 displacedPosition;
        public MagnetProduct product;
    }
    // A rejected physical landing still produces a journey: show the failure,
    // then return to the entrance. Navigation must never strand the actor.
    public static bool PlanJourneyExit(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,out ExitPlan plan){
        if(PlanExit(target,direction,cargo,out plan))return true;
        if(!target||!target.tile||!target.board||Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1)return false;
        plan=new ExitPlan{failedExit=true};return true;
    }
    public static bool PlanExit(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,out ExitPlan plan){
        plan=new ExitPlan();if(!target||!target.CanArrive||(target.Cargo&&target.Cargo!=cargo)||Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1)return false;
        var board=target.board;var cell=target.Cell+direction;GridTile tile;Vector3 top;
        if(!cargo){
            if(!target.TryExitLanding(direction,out tile,out top))return false;
            var obstacle=board.MagnetAt(cell);
            if(obstacle&&!obstacle.combined&&obstacle.product==MagnetProduct.None&&!board.TileAt(cell)){
                plan.player=top;plan.unstableLanding=true;return true;
            }
            bool upright=obstacle&&obstacle.CanBePushed&&(obstacle.product==MagnetProduct.Ring
                ?!MagnetPiece.FlatU(obstacle.Pose):obstacle.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(obstacle.Pose));
            if(upright){
                // The actor lands in the vacated cell, not on the upright top.
                if(!tile||tile.blocked||board.IsWreckCell(cell)||BlackHolePortal.At(board,cell)
                    ||Mathf.Abs(tile.surfaceHeight-target.tile.surfaceHeight)>.01f
                    ||Mathf.Abs(obstacle.transform.position.y-tile.surfaceHeight)>.01f)return false;
                if(!PlanExitKick(board,obstacle,cell,direction,tile.surfaceHeight,ref plan))return false;
                plan.exitKickTarget=obstacle;plan.kick=true;plan.magnet=obstacle.transform.position;
                plan.player=new Vector3(cell.x*board.cellSize,tile.surfaceHeight,cell.y*board.cellSize);
            }else plan.player=top;
            return true;
        }
        if(board.IsWreckCell(cell)||BlackHolePortal.At(board,cell))return false;
        var receiver=board.MagnetAt(cell);
        tile=board.TileAt(cell);
        if(tile&&tile.blocked){
            if(!BlackHolePortal.TrySurface(board,cell,out tile,out top))return false;
            if(!PlanCargoBounce(cargo,out plan))return false;
            plan.magnet=StoneContact(target,tile,cargo,direction);return true;
        }
        if(receiver){
            if(!cargo.combined&&!receiver.combined&&cargo.north==receiver.north){
                var next=cell+direction;var ground=board.TileAt(next);
                if(!tile||tile.blocked||Mathf.Abs(tile.surfaceHeight-target.tile.surfaceHeight)>.01f
                    ||Mathf.Abs(receiver.transform.position.y-tile.surfaceHeight)>.01f)return false;
                if(ground&&(ground.blocked||ground.surfaceHeight>tile.surfaceHeight+.01f)){
                    if(!PlanCargoBounce(cargo,out plan))return false;
                    plan.magnet=MagnetContact(target,receiver,cargo,direction);return true;
                }
                if(board.MagnetAt(next)||board.IsWreckCell(next)||BlackHolePortal.At(board,next)
                    ||(ground&&Mathf.Abs(ground.surfaceHeight-tile.surfaceHeight)>.01f))return false;
                // The arriving upright piece would need this same cell for its subsequent kick.
                if(cargo.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(cargo.Pose))return false;
                plan.displaced=receiver;
                plan.displacedPosition=receiver.transform.position+new Vector3(direction.x,0,direction.y)*board.cellSize;
                plan.magnet=new Vector3(cell.x*board.cellSize,tile.surfaceHeight,cell.y*board.cellSize);
                plan.player=plan.magnet;return true;
            }
            if(cargo.combined||Mathf.Abs(receiver.transform.position.y-target.tile.surfaceHeight)>.01f)return false;
            var product=GridPlayground.Recipe(cargo,cargo.Pose,receiver,tile&&!tile.blocked,direction);
            // A cross can receive an airborne player even though it is not a walking bridge.
            if(product!=MagnetProduct.WideBar&&product!=MagnetProduct.Cross&&product!=MagnetProduct.Ring&&product!=MagnetProduct.Bridge)return false;
            if(product==MagnetProduct.Bridge){
                var second=cell+receiver.bridgeDirection;var ground=board.TileAt(second);var occupant=board.MagnetAt(second);
                if(second==target.Cell||(ground&&(ground.blocked||Mathf.Abs(ground.surfaceHeight-receiver.transform.position.y)>.01f))||(occupant&&occupant!=receiver))return false;
            }
            plan.receiver=receiver;plan.product=product;plan.magnet=target.tile.transform.position;plan.magnet.y=target.tile.surfaceHeight;return true;
        }
        tile=board.TileAt(cell);
        if(tile&&tile.blocked)return false;
        float height=tile?tile.surfaceHeight:target.tile.surfaceHeight;
        if(Mathf.Abs(height-target.tile.surfaceHeight)>.01f)return false;
        plan.magnet=new Vector3(cell.x*board.cellSize,height,cell.y*board.cellSize);
        plan.kick=cargo.product==MagnetProduct.Ring?!MagnetPiece.FlatU(cargo.Pose):cargo.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(cargo.Pose);
        plan.player=plan.magnet;
        if(!tile){
            // A single material can leave the portal into space; the actor cannot stay on it.
            if(cargo.combined||cargo.product!=MagnetProduct.None)return false;
            plan.unstableLanding=true;
            if(!plan.kick)return true;
        }
        if(plan.kick){
            if(!PlanExitKick(board,cargo,cell,direction,height,ref plan))return false;
        }
        return true;
    }
    static bool PlanExitKick(GridPlayground board,MagnetPiece cargo,Vector2Int cell,Vector2Int direction,float height,ref ExitPlan plan){
        var arrival=board.InspectMagnetDestination(cell,height,cell+direction,cargo);
        plan.kicked=arrival.position;
        if(arrival.kind!=GridPlayground.MagnetLandingKind.Blocked)return arrival.kind!=GridPlayground.MagnetLandingKind.Magnet;
        var solid=board.TileAt(cell+direction);
        // A solid next cell stops the tip, but does not prevent the initial ejection.
        plan.kickRebound=cargo.product==MagnetProduct.None&&cargo.shape==MagnetShape.Bar
            &&MagnetPiece.VerticalBar(cargo.Pose)&&solid&&(solid.blocked||solid.surfaceHeight>height+.01f);
        return plan.kickRebound;
    }
    static bool PlanCargoBounce(MagnetPiece cargo,out ExitPlan plan){
        plan=new ExitPlan();
        var source=cargo.storedInPortal;if(!source)return false;
        var returnCell=new Vector2Int(Mathf.RoundToInt(cargo.portalEntryPosition.x/source.board.cellSize),Mathf.RoundToInt(cargo.portalEntryPosition.z/source.board.cellSize));
        var ground=source.board.TileAt(returnCell);
        if(!ground||ground.blocked||source.board.MagnetAt(returnCell)||source.board.IsWreckCell(returnCell)||BlackHolePortal.At(source.board,returnCell))return false;
        plan.kick=cargo.product==MagnetProduct.Ring?!MagnetPiece.FlatU(cargo.Pose):cargo.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(cargo.Pose);
        if(plan.kick){
            var back=returnCell-source.Cell;
            if(!source.board.TryPortalKickDestination(returnCell,back,ground.surfaceHeight,out plan.kicked))return false;
        }
        plan.bounce=true;plan.player=cargo.portalEntryPosition;
        return true;
    }
    static Vector3 MagnetContact(BlackHolePortal portal,MagnetPiece receiver,MagnetPiece cargo,Vector2Int direction){
        var axis=new Vector3(direction.x,0,direction.y);
        var hole=new Vector3(portal.tile.transform.position.x,portal.tile.surfaceHeight,portal.tile.transform.position.z);
        float leading=0,edge=float.PositiveInfinity;
        foreach(var f in cargo.geometry.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh&&f.sharedMesh.isReadable)
            foreach(var v in f.sharedMesh.vertices)leading=Mathf.Max(leading,Vector3.Dot(f.transform.TransformPoint(v)-cargo.transform.position,axis));
        foreach(var f in receiver.geometry.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh&&f.sharedMesh.isReadable)
            foreach(var v in f.sharedMesh.vertices)edge=Mathf.Min(edge,Vector3.Dot(f.transform.TransformPoint(v)-hole,axis));
        float distance=float.IsPositiveInfinity(edge)?portal.board.cellSize*.12f:Mathf.Clamp(edge-leading-.04f,0,portal.board.cellSize*.8f);
        return hole+axis*distance;
    }
    static Vector3 StoneContact(BlackHolePortal portal,GridTile stone,MagnetPiece cargo,Vector2Int direction){
        var axis=new Vector3(direction.x,0,direction.y);
        var hole=new Vector3(portal.tile.transform.position.x,portal.tile.surfaceHeight,portal.tile.transform.position.z);
        float leading=0,edge=float.PositiveInfinity;
        foreach(var f in cargo.geometry.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh&&f.sharedMesh.isReadable)
            foreach(var v in f.sharedMesh.vertices)leading=Mathf.Max(leading,Vector3.Dot(f.transform.TransformPoint(v)-cargo.transform.position,axis));
        var anchor=stone.GetComponentInParent<IslandSurfaceAnchor>();var root=anchor?anchor.transform:stone.transform;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh&&f.sharedMesh.isReadable&&!f.GetComponentInParent<MagnetPiece>()&&!f.GetComponentInParent<BlackHolePortal>())
            foreach(var v in f.sharedMesh.vertices){
                var p=f.transform.TransformPoint(v);var delta=p-stone.transform.position;
                if(p.y<=stone.surfaceHeight+.02f||Mathf.Abs(delta.x)>portal.board.cellSize*.6f||Mathf.Abs(delta.z)>portal.board.cellSize*.6f)continue;
                edge=Mathf.Min(edge,Vector3.Dot(p-hole,axis));
            }
        float distance=float.IsPositiveInfinity(edge)?portal.board.cellSize*.12f:Mathf.Clamp(edge-leading-.02f,0,portal.board.cellSize*.6f);
        return hole+axis*distance;
    }
    IEnumerator Eject(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,ExitPlan plan){
        var board=target.board;var player=board.player;var playerScale=player.localScale;
        board.SetPortalBusy(true);
        player.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        var portalPosition=new Vector3(target.tile.transform.position.x,target.tile.surfaceHeight,target.tile.transform.position.z);
        try{
            if(cargo){
                player.localScale=Vector3.zero;
                var source=cargo.storedInPortal;
                var pose=cargo.Pose;var scale=cargo.geometry.localScale;
                cargo.storedInPortal=null;cargo.transform.position=portalPosition;
                var owner=target.tile.GetComponentInParent<IslandSurfaceAnchor>();var binding=cargo.GetComponent<IslandSurfaceAnchor>();
                if(binding&&owner){binding.center=owner.center;binding.Apply();}
                cargo.gameObject.SetActive(true);
                var landingGround=board.TileAt(target.Cell+direction);
                bool slide=!plan.bounce&&landingGround&&!landingGround.blocked;
                yield return ThrowCargo(cargo,portalPosition,plan.magnet,pose,scale,plan.bounce?.28f:.55f,plan.bounce?.24f:.38f,plan.displaced,plan.displacedPosition,slide);
                // Stone contact reverses immediately; clear landings briefly settle first.
                if(!plan.bounce)yield return AnimationWait(.12f);
                if(plan.bounce){
                    yield return BounceCargo(cargo,source,target,plan.magnet,portalPosition,pose,scale,playerScale);
                    var returnCell=new Vector2Int(Mathf.RoundToInt(cargo.portalEntryPosition.x/board.cellSize),Mathf.RoundToInt(cargo.portalEntryPosition.z/board.cellSize));
                    GridTile returnTile;Vector3 returnTop;
                    if(!BlackHolePortal.TrySurface(source.board,returnCell,out returnTile,out returnTop))throw new System.InvalidOperationException("Returned cargo has no landing surface");
                    var returnStart=new Vector3(source.tile.transform.position.x,source.tile.surfaceHeight,source.tile.transform.position.z);
                    if(plan.kick){
                        var returnDirection=returnCell-source.Cell;
                        var returnPlan=new ExitPlan{magnet=cargo.portalEntryPosition,player=cargo.portalEntryPosition,kicked=plan.kicked,kick=true};
                        yield return KickExitPlayer(player,cargo,returnStart,returnDirection,returnPlan,pose,playerScale,source.board);
                    }else yield return EjectPlayer(player,returnStart,returnTop,playerScale);
                    board.CompletePortalLanding(returnCell);
                    yield break;
                }else if(plan.receiver){
                    yield return board.AssemblePortalCargo(cargo,plan.receiver,direction,plan.product);
                    GridTile landingTile;Vector3 landing;
                    if(!BlackHolePortal.TrySurface(board,target.Cell+direction,out landingTile,out landing))throw new System.InvalidOperationException("Assembly has no landing surface");
                    plan.player=landing;yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
                }else if(plan.kick){
                    yield return KickExitPlayer(player,cargo,portalPosition,direction,plan,pose,playerScale,board);
                    if(plan.unstableLanding||plan.kickRebound)yield break;
                }else{
                    if(plan.unstableLanding){
                        GridTile floatingTile;Vector3 floatingTop;
                        if(BlackHolePortal.TrySurface(board,target.Cell+direction,out floatingTile,out floatingTop))plan.player=floatingTop;
                        yield return UnstablePortalLanding(player,target,direction,portalPosition,plan.player,playerScale);
                        yield break;
                    }
                    GridTile tile;Vector3 top;
                    if(BlackHolePortal.TrySurface(board,target.Cell+direction,out tile,out top))plan.player=top;
                    // TrySurface chooses actual solid top triangles, avoiding U/ring holes.
                    yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
                }
            }else if(plan.unstableLanding){
                yield return UnstablePortalLanding(player,target,direction,portalPosition,plan.player,playerScale);
                yield break;
            }else if(plan.exitKickTarget){
                yield return KickExitPlayer(player,plan.exitKickTarget,portalPosition,direction,plan,plan.exitKickTarget.Pose,playerScale,board);
                if(plan.kickRebound)yield break;
            }else yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
            player.position=plan.player;board.CompletePortalLanding(target.Cell+direction);
        }finally{if(player){player.localScale=playerScale;var pose=player.GetComponent<PlayerPushPose>();if(pose)pose.End();}if(board)board.SetPortalBusy(false);}
    }
    IEnumerator UnstablePortalLanding(Transform player,BlackHolePortal portal,Vector2Int direction,Vector3 hole,Vector3 landing,Vector3 scale){
        var board=portal.board;var magnet=board.MagnetAt(portal.Cell+direction);
        var geometry=magnet.geometry;board.AnimatePortalMagnet(magnet,true);
        var restPosition=geometry.position;var restRotation=geometry.rotation;
        var outward=new Vector3(direction.x,0,direction.y);
        var pose=PlayerPushPose.Begin(player);
        // The root remains on its logical layer; feet follow the displayed top.
        float offset=board.PortalMagnetOffset(magnet);
        landing.y+=offset;
        // The prone mesh extends forward from its feet. Centre the entire body
        // over the span, rather than placing its feet in the centre of the gap.
        pose.SamplePortalProne();
        float back=float.PositiveInfinity,front=float.NegativeInfinity;
        foreach(var filter in player.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh&&filter.sharedMesh.isReadable){
            foreach(var vertex in filter.sharedMesh.vertices){
                float along=Vector3.Dot(filter.transform.TransformPoint(vertex)-player.position,outward);
                back=Mathf.Min(back,along);front=Mathf.Max(front,along);
            }
        }
        float bodyCentre=float.IsInfinity(back)?board.cellSize*.32f:(back+front)*.5f;
        float spanCentre=Vector3.Dot(magnet.transform.position-landing,outward);
        landing+=outward*(spanCentre-bodyCentre);
        pose.SampleUnstablePortalArrival(0);
        var launch=landing;
        try{
            geometry.position=restPosition+Vector3.up*offset;
            for(float t=0;t<1.05f;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/1.05f);player.localScale=scale*Mathf.Clamp01(a*5);
                player.position=ThrownPosition(hole,landing,a);
                // Blend into the same prone pose used by the recovery, without a pose reset.
                pose.SampleUnstablePortalArrival(a);yield return null;
            }
            player.localScale=scale;player.position=landing;
            // Impact and spring compression share one displacement: the prone
            // actor stays in contact with the magnetic surface until release.
            float depth=board.cellSize*.32f;
            for(float t=0;t<.3f;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/.3f),dip=-depth*Mathf.Sin(a*Mathf.PI*.5f);
                geometry.position=restPosition+Vector3.up*(offset+dip);
                player.position=landing+Vector3.up*dip;
                pose.SamplePortalProne();yield return null;
            }
            const float reboundSeconds=.18f;
            for(float t=0;t<reboundSeconds;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/reboundSeconds),dip=-depth*Mathf.Cos(a*Mathf.PI*.5f);
                geometry.position=restPosition+Vector3.up*(offset+dip);
                player.position=landing+Vector3.up*dip;
                pose.SamplePortalProne();yield return null;
            }
            launch=landing;player.position=launch;
            var facing=player.rotation;
            // Match the spring's upward release speed, then follow a ballistic
            // arc whose endpoint is inside the mouth of the same black hole.
            float velocity=depth*Mathf.PI/(2*reboundSeconds);
            const float flightSeconds=1.05f;
            var entry=hole-Vector3.up*.22f;
            float gravity=2*(launch.y+velocity*flightSeconds-entry.y)/(flightSeconds*flightSeconds);
            for(float t=0;t<flightSeconds;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/flightSeconds);
                player.position=Vector3.Lerp(launch,entry,a);
                var position=player.position;position.y=launch.y+velocity*t-.5f*gravity*t*t;player.position=position;
                player.rotation=Quaternion.Slerp(facing,Quaternion.LookRotation(-outward),Mathf.SmoothStep(0,1,Mathf.Clamp01(a/.35f)));
                pose.SamplePortalSpringFlight(a);
                player.localScale=scale*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.83f,1,a)));
                // The unloaded magnet overshoots and settles back into its hover.
                float spring=.13f*Mathf.Sin(t*18)*Mathf.Exp(-t*5);
                geometry.position=restPosition;
                float hover=board.PortalMagnetOffset(magnet);
                geometry.position=restPosition+Vector3.up*(Mathf.Lerp(offset,hover,Mathf.SmoothStep(0,1,a))+spring);
                yield return null;
            }
            player.position=hole;player.localScale=Vector3.zero;
        }finally{
            geometry.position=restPosition;geometry.rotation=restRotation;
            board.AnimatePortalMagnet(magnet,false);pose.End();
        }
    }
    IEnumerator KickExitPlayer(Transform player,MagnetPiece cargo,Vector3 portalPosition,Vector2Int direction,ExitPlan plan,Quaternion pose,Vector3 playerScale,GridPlayground board){
                    if(plan.kickRebound){
                        yield return BlockedKickRebound(player,cargo,portalPosition,direction,plan,pose,playerScale,board);yield break;
                    }
                    if(plan.unstableLanding){
                        yield return KickFloatingExit(player,cargo,portalPosition,direction,plan,pose,playerScale,board);yield break;
                    }
                    player.localScale=playerScale;var axis=new Vector3(direction.y,0,-direction.x);
                    var fallen=cargo.product==MagnetProduct.Ring?Quaternion.FromToRotation(pose*Vector3.up,Vector3.up)*pose:Quaternion.AngleAxis(90,axis)*pose;
                    var kickPose=PlayerPushPose.Begin(player);
                    var landing=PortalProneLanding(player,kickPose,plan.player,playerScale);
                    for(float t=0;t<.8f;t+=AnimationDeltaTime){
                        float a=Mathf.SmoothStep(0,1,t/.8f);
                        cargo.transform.position=Vector3.Lerp(plan.magnet,plan.kicked,a);
                        cargo.geometry.rotation=Quaternion.Slerp(pose,fallen,a);MagnetVisuals.Ground(cargo);kickPose.SamplePortalFlight(a);
                        player.position=ThrownPosition(portalPosition,landing,a);yield return null;
                    }
                    cargo.transform.position=plan.kicked;cargo.geometry.rotation=fallen;MagnetVisuals.Ground(cargo);
                    if(cargo.product==MagnetProduct.Ring)cargo.walkable=MagnetPiece.FlatU(cargo.Pose);


                    player.position=landing;kickPose.SamplePortalProne();
                    yield return board.SettleMagnetArrival(cargo,plan.magnet,true);
                    player.position=landing;yield return RecoverPlayer(player,plan.player);
    }
    IEnumerator BlockedKickRebound(Transform player,MagnetPiece cargo,Vector3 hole,Vector2Int direction,ExitPlan plan,Quaternion original,Vector3 scale,GridPlayground board){
        var outward=new Vector3(direction.x,0,direction.y);
        var contact=plan.magnet-outward*(board.cellSize*.3f)+Vector3.up*.1f;
        var shove=outward*(board.cellSize*.08f);
        var axis=new Vector3(direction.y,0,-direction.x);
        const float pressAngle=50f;
        var pressedContact=plan.magnet+shove+Quaternion.AngleAxis(pressAngle,axis)*(contact-plan.magnet);
        var pose=PlayerPushPose.Begin(player);board.AnimatePortalMagnet(cargo,true);
        try{
            for(float t=0;t<.7f;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/.7f);player.localScale=scale*Mathf.Clamp01(a*5);
                player.position=Vector3.Lerp(hole,contact,a)+Vector3.up*(.7f*Mathf.Sin(a*Mathf.PI));
                pose.SamplePortalFlight(a*.5f);yield return null;
            }
            for(float t=0;t<.8f;t+=AnimationDeltaTime){
                float a=Mathf.SmoothStep(0,1,t/.8f);
                cargo.transform.position=plan.magnet+shove*a;
                var tilt=Quaternion.AngleAxis(pressAngle*a,axis);
                cargo.geometry.rotation=tilt*original;MagnetVisuals.Ground(cargo);
                player.position=plan.magnet+shove*a+tilt*(contact-plan.magnet);
                pose.SamplePortalMagnetPress(a);yield return null;
            }
            // The leaning bar springs upright; the actor releases from the same contact pose.
            var launch=pressedContact;
            for(float t=0;t<.95f;t+=AnimationDeltaTime){
                float flight=Mathf.Clamp01(t/.95f),release=Mathf.Clamp01(t/.22f);
                cargo.transform.position=plan.magnet+shove*Mathf.Pow(1-release,2);
                cargo.geometry.rotation=Quaternion.AngleAxis(pressAngle*Mathf.Pow(1-release,2),axis)*original;MagnetVisuals.Ground(cargo);
                player.position=Vector3.Lerp(launch,hole-Vector3.up*.18f,flight)+Vector3.up*(.65f*Mathf.Sin(flight*Mathf.PI));
                player.localScale=scale*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1,flight)));
                pose.SamplePortalMagnetPress(1-release);yield return null;
            }
        }finally{
            cargo.transform.position=plan.magnet;cargo.geometry.rotation=original;MagnetVisuals.Ground(cargo);
            board.AnimatePortalMagnet(cargo,false);pose.End();player.position=hole;player.localScale=Vector3.zero;
        }
    }
    IEnumerator KickFloatingExit(Transform player,MagnetPiece cargo,Vector3 hole,Vector2Int direction,ExitPlan plan,Quaternion original,Vector3 scale,GridPlayground board){
        var outward=new Vector3(direction.x,0,direction.y);
        var fallen=Quaternion.AngleAxis(90,new Vector3(direction.y,0,-direction.x))*original;
        var contact=plan.magnet-outward*(board.cellSize*.27f)+Vector3.up*.15f;
        var pose=PlayerPushPose.Begin(player);board.AnimatePortalMagnet(cargo,true);
        try{
            // The actor must reach the upright piece before it starts sliding or tipping.
            for(float t=0;t<.52f;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/.52f);
                player.localScale=scale*Mathf.SmoothStep(0,1,Mathf.Clamp01(a/.25f));
                player.position=Vector3.Lerp(hole,contact,a)+Vector3.up*(.65f*Mathf.Sin(a*Mathf.PI));
                pose.SamplePortalFlight(a*.5f);yield return null;
            }
            player.position=contact;player.localScale=scale;
            for(float t=0;t<.85f;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/.85f),push=Mathf.SmoothStep(0,1,Mathf.Clamp01(a/.65f));
                cargo.transform.position=Vector3.Lerp(plan.magnet,plan.kicked,push);
                cargo.geometry.rotation=Quaternion.Slerp(original,fallen,push);MagnetVisuals.Ground(cargo);
                player.position=Vector3.Lerp(contact,hole-Vector3.up*.18f,a)+Vector3.up*(.8f*Mathf.Sin(a*Mathf.PI));
                pose.SamplePortalFlight(.5f*(1-a));
                player.localScale=scale*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,a)));
                yield return null;
            }
            cargo.transform.position=plan.kicked;cargo.geometry.rotation=fallen;MagnetVisuals.Ground(cargo);
            player.position=hole;player.localScale=Vector3.zero;
            yield return board.SettleMagnetArrival(cargo,plan.magnet,true);
        }finally{
            board.AnimatePortalMagnet(cargo,false);pose.End();
            player.position=hole;player.localScale=Vector3.zero;
        }
    }
    static Vector3 ThrownPosition(Vector3 start,Vector3 end,float progress){
        float a=Mathf.Clamp01(progress),horizontal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,1,a));
        var p=Vector3.Lerp(start,end,horizontal);
        float peak=Mathf.Max(start.y,end.y)+.95f;
        p.y=a<.45f?Mathf.Lerp(start.y-.25f,peak,1-Mathf.Pow(1-a/.45f,2)):Mathf.Lerp(peak,end.y,Mathf.Pow((a-.45f)/.55f,2));
        return p;
    }
    IEnumerator FailedPortalExit(BlackHolePortal portal,Vector2Int direction,MagnetPiece cargo,Vector3 scale){
        var board=portal.board;var player=board.player;
        var exitCell=portal.Cell+direction;
        if(!cargo&&!board.TileAt(exitCell)&&!board.MagnetAt(exitCell)&&!board.IsWreckCell(exitCell)){
            yield return EmptySpacePortalExit(portal,direction,scale);yield break;
        }
        var hole=new Vector3(portal.tile.transform.position.x,portal.tile.surfaceHeight,portal.tile.transform.position.z);
        var axis=new Vector3(direction.x,0,direction.y);
        var pose=PlayerPushPose.Begin(player);board.SetPortalBusy(true);
        var savedPosition=cargo?cargo.transform.position:Vector3.zero;
        var savedScale=cargo?cargo.geometry.localScale:Vector3.one;
        var savedPose=cargo?cargo.Pose:Quaternion.identity;
        bool savedActive=cargo&&cargo.gameObject.activeSelf;
        try{
            // Cargo rises first, strikes the obstructed exit, and falls back onto
            // the emerging actor. Never mutate or consume the receiving object.
            if(cargo){
                player.localScale=Vector3.zero;cargo.gameObject.SetActive(true);
                yield return ThrowCargo(cargo,hole,hole+axis*(board.cellSize*.35f),savedPose,savedScale,.5f,.9f);
            }
            for(float t=0;t<1.15f;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/1.15f);
                float sink=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,a));
                player.localScale=scale*(1-sink);
                player.position=hole+axis*(board.cellSize*.3f*Mathf.Sin(a*Mathf.PI))+Vector3.up*(.7f*Mathf.Sin(a*Mathf.PI)-.25f*sink);
                if(cargo){
                    float contact=Mathf.Clamp01(a/.65f);
                    cargo.transform.position=Vector3.Lerp(hole+axis*(board.cellSize*.35f),hole,contact)
                        +Vector3.up*(.7f+2.5f*contact-3.2f*contact*contact-.3f*sink);
                    cargo.geometry.localScale=savedScale*(1-sink);MagnetVisuals.Ground(cargo);
                    pose.SamplePortalHeadImpact(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.7f,a)),sink);
                }else pose.SamplePortalSpringFlight(a);
                yield return null;
            }
        }finally{
            if(cargo){cargo.transform.position=savedPosition;cargo.geometry.localScale=savedScale;cargo.geometry.rotation=savedPose;MagnetVisuals.Ground(cargo);cargo.gameObject.SetActive(savedActive);}
            pose.End();player.position=hole;player.localScale=Vector3.zero;board.SetPortalBusy(false);
        }
    }
    IEnumerator EmptySpacePortalExit(BlackHolePortal portal,Vector2Int direction,Vector3 scale){
        var board=portal.board;var player=board.player;
        var hole=new Vector3(portal.tile.transform.position.x,portal.tile.surfaceHeight,portal.tile.transform.position.z);
        var outward=new Vector3(direction.x,0,direction.y);
        var facingOut=Quaternion.LookRotation(outward);var facingIn=Quaternion.LookRotation(-outward);
        var lip=hole+outward*(board.cellSize*.5f);
        var hanging=lip+outward*.16f-Vector3.up*.82f;
        var caught=hanging+Vector3.up*.10f;
        var lifted=lip+outward*.16f+Vector3.up*.16f;
        const float reachSeconds=1.5f,catchSeconds=.55f,pullSeconds=.62f,diveSeconds=.9f;
        var pose=PlayerPushPose.Begin(player);board.SetPortalBusy(true);
        try{
            // Leave the mouth, turn back while airborne, then catch the shore below its lip.
            for(float t=0;t<reachSeconds;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/reachSeconds),turn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,.70f,a));
                player.localScale=scale*Mathf.SmoothStep(0,1,Mathf.Clamp01(a/.18f));
                player.position=Vector3.Lerp(hole,caught,a)+Vector3.up*(1.05f*Mathf.Sin(a*Mathf.PI));
                player.rotation=Quaternion.Slerp(facingOut,facingIn,turn);
                pose.SamplePortalPanicReach(a,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.52f,.94f,a)));
                yield return null;
            }
            player.localScale=scale;player.position=caught;player.rotation=facingIn;
            // Arrest the fall sharply, then gather the legs before the explosive pull.
            for(float t=0;t<catchSeconds;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/catchSeconds);
                player.position=Vector3.Lerp(caught,hanging,Mathf.Sin(Mathf.Min(1,a/.4f)*Mathf.PI*.5f));
                pose.SamplePortalLedgeEffort(Mathf.SmoothStep(0,1,a),0);yield return null;
            }
            for(float t=0;t<pullSeconds;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/pullSeconds),pull=a*a*a;
                player.position=Vector3.Lerp(hanging,lifted,pull);
                pose.SamplePortalLedgeEffort(1,pull);yield return null;
            }
            player.position=lifted;
            // Release above the lip before travelling inward, so the torso clears the island.
            for(float t=0;t<diveSeconds;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/diveSeconds);
                player.position=Vector3.Lerp(lifted,hole-Vector3.up*.18f,a)
                    +Vector3.up*(1.2f*4*a*(1-a));
                pose.SamplePortalLedgeDive(a);
                player.localScale=scale*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,a)));
                yield return null;
            }
        }finally{
            pose.End();player.position=hole;player.rotation=facingIn;player.localScale=Vector3.zero;
            board.SetPortalBusy(false);
        }
    }
    IEnumerator EjectPlayer(Transform player,Vector3 start,Vector3 end,Vector3 scale){
        var flight=PlayerPushPose.Begin(player);
        var landing=PortalProneLanding(player,flight,end,scale);
        for(float t=0;t<1.05f;t+=AnimationDeltaTime){
            float a=t/1.05f;player.localScale=scale*Mathf.Lerp(.05f,1,Mathf.Clamp01(a*5));
            player.position=ThrownPosition(start,landing,a);flight.SamplePortalFlight(a);yield return null;
        }
        player.localScale=scale;player.position=landing;yield return RecoverPlayer(player,end);
    }
    static Vector3 PortalProneLanding(Transform player,PlayerPushPose pose,Vector3 centre,Vector3 scale){
        var savedScale=player.localScale;player.localScale=scale;
        pose.SamplePortalProne();
        var forward=player.forward;forward.y=0;forward.Normalize();
        float back=float.PositiveInfinity,front=float.NegativeInfinity;
        foreach(var filter in player.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh&&filter.sharedMesh.isReadable){
            foreach(var vertex in filter.sharedMesh.vertices){
                float along=Vector3.Dot(filter.transform.TransformPoint(vertex)-player.position,forward);
                back=Mathf.Min(back,along);front=Mathf.Max(front,along);
            }
        }
        player.localScale=savedScale;pose.SamplePortalFlight(0);
        return centre-forward*(float.IsInfinity(back)?.48f:(back+front)*.5f);
    }
    IEnumerator RecoverPlayer(Transform player,Vector3 centre){
        var pose=PlayerPushPose.Begin(player);
        var landing=player.position;
        try{
            for(float t=0;t<1.25f;t+=AnimationDeltaTime){
                float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,1.25f,t));
                player.position=Vector3.Lerp(landing,centre,rise);
                pose.SamplePortalRecovery(rise);yield return null;
            }
        }finally{if(player)player.position=centre;if(pose)pose.End();}
    }
    IEnumerator ThrowCargo(MagnetPiece cargo,Vector3 start,Vector3 end,Quaternion pose,Vector3 scale,float seconds,float arc,MagnetPiece displaced=null,Vector3 displacedEnd=default(Vector3),bool slide=false){
        var displacedStart=displaced?displaced.transform.position:Vector3.zero;
        var forward=end-start;forward.y=0;
        float distance=forward.magnitude;forward=distance>.001f?forward/distance:Vector3.forward;
        var tiltAxis=Vector3.Cross(Vector3.up,forward);
        var touchdown=end-forward*(slide?distance*.18f:0);
        // The destination preview may have hidden these renderers while the
        // cargo still belonged to the entry island. The board stays disabled
        // throughout travel, so its normal visibility refresh cannot restore them.
        // Apply this for both the outgoing throw and a bounce back to the source.
        var visibility=new MaterialPropertyBlock();
        foreach(var renderer in cargo.GetComponentsInChildren<Renderer>(true)){
            renderer.enabled=true;
            renderer.GetPropertyBlock(visibility);
            visibility.SetFloat("_IslandWindowClipEnabled",0);
            renderer.SetPropertyBlock(visibility);
        }
        cargo.geometry.localScale=scale;
        for(float t=0;t<seconds;t+=AnimationDeltaTime){
            float a=Mathf.Clamp01(t/seconds);
            // Launch outward immediately. A thrown body keeps its horizontal
            // momentum until touchdown instead of easing to a stop in mid-air.
            float horizontal=a;
            cargo.transform.position=Vector3.Lerp(start,touchdown,horizontal);
            if(displaced)displaced.transform.position=Vector3.Lerp(displacedStart,displacedEnd,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.65f,a)));
            float lean=12*Mathf.Sin(Mathf.PI*Mathf.Clamp01(a/.22f)*.5f)
                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1,a)));
            cargo.geometry.rotation=Quaternion.AngleAxis(lean,tiltAxis)*pose;MagnetVisuals.Ground(cargo);
            // One ballistic parabola gives a continuous upward impulse and
            // constant downward acceleration, without a pause at the apex.
            float airborneY=Mathf.Lerp(start.y-.2f,end.y,a)+4*arc*a*(1-a);
            float lift=airborneY-cargo.transform.position.y;
            cargo.geometry.position+=Vector3.up*lift;yield return null;
        }
        if(slide){
            cargo.transform.position=touchdown;cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);
            // Ground friction removes the remaining forward momentum within this cell.
            for(float t=0;t<.18f;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/.18f),travel=1-(1-a)*(1-a);
                cargo.transform.position=Vector3.Lerp(touchdown,end,travel);
                MagnetVisuals.Ground(cargo);yield return null;
            }
        }
        if(displaced)displaced.transform.position=displacedEnd;
        cargo.transform.position=end;cargo.geometry.rotation=pose;cargo.geometry.localScale=scale;MagnetVisuals.Ground(cargo);
    }
    IEnumerator BounceCargo(MagnetPiece cargo,BlackHolePortal source,BlackHolePortal target,Vector3 hit,Vector3 hole,Quaternion pose,Vector3 scale,Vector3 playerScale){
        var player=target.board.player;
        player.localScale=playerScale;
        var reaction=PlayerPushPose.Begin(player);
        var playerMeshes=player.GetComponentsInChildren<MeshFilter>();
        var cargoMeshes=cargo.geometry.GetComponentsInChildren<MeshFilter>();
        var vertices=new System.Collections.Generic.List<Vector3>();
        reaction.SamplePortalHeadImpact(0,0);
        float bodyTop=TransitMeshHeight(playerMeshes,vertices,true,player.position.y+1)-player.position.y;
        float hiddenY=-bodyTop-.12f,emergedY=-bodyTop*.5f;
        float clearance=.015f*target.board.cellSize;
        float contactHeight=bodyTop+emergedY+clearance;
        // Keep the contact posture fixed and launch the bottom of the actual mesh.
        float launchBottom=TransitMeshHeight(cargoMeshes,vertices,false,hit.y);
        float contactWorldY=hole.y+contactHeight;
        float peakHeight=Mathf.Max(launchBottom,contactWorldY)+target.board.cellSize*1.1f;
        float gravity=target.board.cellSize*3.6f;
        float velocity=Mathf.Sqrt(2*gravity*(peakHeight-launchBottom));
        float impactTime=-1;
        // Start the player's ascent only after the magnet has passed its apex.
        // Its own apex exposes roughly half the body, so the descending cargo
        // catches it at the mouth instead of after a full-body launch.
        float launchDelay=velocity/gravity+.06f;
        float playerVelocity=Mathf.Sqrt(2*gravity*(emergedY-hiddenY+.08f));
        float impactPlayerHeight=0;
        try{
            for(float t=0;;t+=AnimationDeltaTime){
                if(impactTime>=0&&t>=impactTime+.85f)break;
                float impact=impactTime<0?0:Mathf.SmoothStep(0,1,Mathf.InverseLerp(impactTime,impactTime+.22f,t));
                float sink=impactTime<0?0:Mathf.SmoothStep(0,1,Mathf.InverseLerp(impactTime+.10f,impactTime+.85f,t));
                // Collapse toward the mouth instead of pushing full-size meshes
                // through the floor. Both visuals disappear before changing islands.
                float remaining=1-sink;
                player.localScale=playerScale*remaining;
                float airborneTime=Mathf.Max(0,t-launchDelay);
                if(impactTime<0){
                    // A real ascent, not a half-body reveal with a stationary hold.
                    player.position=hole+Vector3.up*(hiddenY+playerVelocity*airborneTime-.5f*gravity*airborneTime*airborneTime);
                    // Use the same neutral pose that begins the impact blend:
                    // switching from flailing flight limbs caused a one-frame jerk.
                    reaction.SamplePortalHeadImpact(0,0);
                }else{
                    // The impact immediately reverses the actor toward the mouth;
                    // shrinking and descent overlap instead of freezing at contact.
                    float returnProgress=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-impactTime)/.85f));
                    player.position=hole+Vector3.up*Mathf.Lerp(impactPlayerHeight,hiddenY,returnProgress);
                    reaction.SamplePortalHeadImpact(impact,sink);
                }
                // Arrive above the mouth at the apex, then fall straight onto the helmet.
                float approach=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/(velocity/gravity)));
                cargo.transform.position=Vector3.Lerp(hit,hole,approach);
                cargo.geometry.localScale=scale*remaining;cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);
                float top=TransitMeshHeight(playerMeshes,vertices,true,player.position.y+bodyTop);
                float bottom=TransitMeshHeight(cargoMeshes,vertices,false,hole.y);
                float ballisticBottom=launchBottom+velocity*t-.5f*gravity*t*t;
                // Use actual vertices: renderer bounds are inflated for island rendering.
                // On impact the cargo follows the posed actor, never sinking independently.
                float contactY=Mathf.Max(top,hole.y)+clearance*remaining;
                if(impactTime<0&&t>velocity/gravity&&ballisticBottom<=contactY){
                    impactTime=t;
                    impactPlayerHeight=player.position.y-hole.y;
                }
                float cargoY=impactTime<0?ballisticBottom:contactY;
                cargo.geometry.position+=Vector3.up*(cargoY-bottom);
                yield return null;
            }
        }finally{reaction.End();player.localScale=Vector3.zero;cargo.geometry.localScale=Vector3.zero;}
        cargo.gameObject.SetActive(false);
        if(source.board!=target.board){
            var carried=target.board.magnets.Where(m=>m&&m.transform.IsChildOf(cargo.transform)).ToArray();
            target.board.ReleasePortalGeometry(carried);target.board.magnets=target.board.magnets.Except(carried).ToArray();
            cargo.transform.SetParent(source.board.transform,true);source.board.magnets=source.board.magnets.Concat(carried).ToArray();
        }
        var binding=cargo.GetComponent<IslandSurfaceAnchor>();var owner=source.tile.GetComponentInParent<IslandSurfaceAnchor>();
        if(binding&&owner){binding.center=owner.center;binding.Apply();}
        var start=new Vector3(source.tile.transform.position.x,source.tile.surfaceHeight,source.tile.transform.position.z);
        source.Disarm();player.position=start;
        var returnDirection=(cargo.portalEntryPosition-start);returnDirection.y=0;
        if(returnDirection.sqrMagnitude>.001f)player.rotation=Quaternion.LookRotation(returnDirection);
        target.board.CompletePortalLanding(source.Cell);
        yield return WaitForExitView(source);
        cargo.transform.position=start;cargo.gameObject.SetActive(true);
        yield return ThrowCargo(cargo,start,cargo.portalEntryPosition,pose,scale,.65f,.45f);
        if(source.board!=target.board)source.board.CaptureInitialState();
    }
    static float TransitMeshHeight(MeshFilter[] meshes,System.Collections.Generic.List<Vector3> vertices,bool highest,float fallback){
        float height=highest?float.NegativeInfinity:float.PositiveInfinity;
        foreach(var filter in meshes){
            if(!filter||!filter.gameObject.activeInHierarchy||!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;
            filter.sharedMesh.GetVertices(vertices);
            foreach(var vertex in vertices){
                float y=filter.transform.TransformPoint(vertex).y;
                height=highest?Mathf.Max(height,y):Mathf.Min(height,y);
            }
        }
        return float.IsInfinity(height)?fallback:height;
    }
}
}
