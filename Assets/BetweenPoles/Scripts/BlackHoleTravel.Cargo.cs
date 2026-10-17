using System.Collections;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    public struct ExitPlan {
        public Vector3 player,magnet,kicked;
        public bool kick,bounce,unstableLanding,failedExit,kickRebound,separateCargo,floatRing;
        public MagnetPiece receiver;
        public MagnetPiece displaced;
        public MagnetPiece exitKickTarget;
        public MagnetPiece kickReceiver;
        public MagnetPiece kickDisplaced;
        public Vector3 kickDisplacedPosition;
        public MagnetProduct kickProduct;
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
        return PlanExitCore(target,direction,cargo,out plan,false);
    }
    static bool PlanExitCore(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,out ExitPlan plan,bool cargoOnly){
        plan=new ExitPlan();if(!target||!target.CanArrive||(cargo&&target.Cargo&&target.Cargo!=cargo)||Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1)return false;
        var board=target.board;var cell=target.Cell+direction;GridTile tile;Vector3 top;
        float floorHeight;bool hasFloor=board.TryPortalFloor(cell,out floorHeight);
        if(cargo&&cargo.product==MagnetProduct.Ring&&cargo.portalRollSpeed>0){
            if(!board.CanPortalRingEnter(cargo,target.Cell,target.tile.surfaceHeight,cell))return false;
            plan.magnet=new Vector3(cell.x*board.cellSize,target.tile.surfaceHeight,cell.y*board.cellSize);
            plan.player=plan.magnet;return true;
        }
        if(!cargo){
            if(!target.TryExitLanding(direction,out tile,out top))return false;
            var obstacle=board.PortalObstacle(cell);
            if(obstacle&&!obstacle.combined&&obstacle.product==MagnetProduct.None&&!hasFloor){
                plan.player=top;plan.unstableLanding=true;return true;
            }
            bool upright=obstacle&&obstacle.CanBePushed&&(obstacle.product==MagnetProduct.Ring
                ?!MagnetPiece.FlatU(obstacle.Pose):obstacle.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(obstacle.Pose));
            if(upright){
                // The actor lands in the vacated cell, not on the upright top.
                if(!hasFloor||board.IsWreckCell(cell)||BlackHolePortal.At(board,cell)
                    ||Mathf.Abs(floorHeight-target.tile.surfaceHeight)>.01f
                    ||Mathf.Abs(obstacle.transform.position.y-floorHeight)>.01f)return false;
                if(!PlanExitKick(board,obstacle,cell,direction,floorHeight,ref plan))return false;
                plan.exitKickTarget=obstacle;plan.kick=true;plan.magnet=obstacle.transform.position;
                plan.player=new Vector3(cell.x*board.cellSize,floorHeight,cell.y*board.cellSize);
            }else plan.player=top;
            return true;
        }
        if(board.IsWreckCell(cell))return false;
        var receivingPortal=BlackHolePortal.At(board,cell);
        if(receivingPortal){
            if(!receivingPortal.CanArrive||Mathf.Abs(receivingPortal.tile.surfaceHeight-target.tile.surfaceHeight)>.01f)return false;
            plan.magnet=new Vector3(cell.x*board.cellSize,receivingPortal.tile.surfaceHeight,cell.y*board.cellSize);
            plan.player=plan.magnet;return true;
        }
        var receiver=board.PortalObstacle(cell);
        tile=board.TileAt(cell);
        if(tile&&tile.blocked){
            if(!BlackHolePortal.TrySurface(board,cell,out tile,out top))return false;
            if(!PlanCargoBounce(cargo,out plan))return false;
            plan.magnet=StoneContact(target,tile,cargo,direction);return true;
        }
        if(receiver){
            if(!cargo.combined&&!receiver.combined&&cargo.north==receiver.north){
                var next=cell+direction;var ground=board.TileAt(next);
                if(!hasFloor||Mathf.Abs(floorHeight-target.tile.surfaceHeight)>.01f
                    ||Mathf.Abs(receiver.transform.position.y-floorHeight)>.01f)return false;
                if(ground&&(ground.blocked||ground.surfaceHeight>floorHeight+.01f)){
                    if(!PlanCargoBounce(cargo,out plan))return false;
                    plan.magnet=MagnetContact(target,receiver,cargo,direction);return true;
                }
                float nextFloorHeight;
                if(board.PortalObstacle(next)||board.IsWreckCell(next)||BlackHolePortal.At(board,next)
                    ||(board.TryPortalFloor(next,out nextFloorHeight)&&Mathf.Abs(nextFloorHeight-floorHeight)>.01f))return false;
                // The arriving upright piece would need this same cell for its subsequent kick.
                if(!cargoOnly&&cargo.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(cargo.Pose))return false;
                plan.displaced=receiver;
                plan.displacedPosition=receiver.transform.position+new Vector3(direction.x,0,direction.y)*board.cellSize;
                plan.magnet=new Vector3(cell.x*board.cellSize,floorHeight,cell.y*board.cellSize);
                plan.player=plan.magnet;return true;
            }
            if(cargo.combined||Mathf.Abs(receiver.transform.position.y-target.tile.surfaceHeight)>.01f)return false;
            var product=PortalCargoRecipe(cargo,receiver,hasFloor,direction);
            // A cross can receive an airborne player even though it is not a walking bridge.
            if(product!=MagnetProduct.WideBar&&product!=MagnetProduct.Cross&&product!=MagnetProduct.Ring&&product!=MagnetProduct.Bridge
                &&product!=MagnetProduct.ThickU&&product!=MagnetProduct.CrossU)return false;
            if(product==MagnetProduct.Bridge){
                var second=cell+receiver.bridgeDirection;var ground=board.TileAt(second);var occupant=board.MagnetAt(second);
                if(second==target.Cell||(ground&&(ground.blocked||Mathf.Abs(ground.surfaceHeight-receiver.transform.position.y)>.01f))||(occupant&&occupant!=receiver))return false;
            }
            plan.receiver=receiver;plan.product=product;plan.magnet=target.tile.transform.position;plan.magnet.y=target.tile.surfaceHeight;return true;
        }
        tile=board.TileAt(cell);
        if(tile&&tile.blocked)return false;
        float height=hasFloor?floorHeight:target.tile.surfaceHeight;
        if(Mathf.Abs(height-target.tile.surfaceHeight)>.01f)return false;
        plan.magnet=new Vector3(cell.x*board.cellSize,height,cell.y*board.cellSize);
        plan.kick=cargo.product==MagnetProduct.Ring?!MagnetPiece.FlatU(cargo.Pose):cargo.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(cargo.Pose);
        plan.player=plan.magnet;
        if(!hasFloor){
            // A closed ring becomes a stable floating platform before the actor
            // follows. It does not need a second cell for an upright kick.
            if(cargo.product==MagnetProduct.Ring){plan.kick=false;plan.floatRing=true;return true;}
            // A single material can leave the portal into space; the actor cannot stay on it.
            if(cargo.combined||cargo.product!=MagnetProduct.None)return false;
            plan.unstableLanding=true;
            if(!plan.kick)return true;
        }
        if(cargoOnly){plan.kick=false;plan.unstableLanding=false;return true;}
        if(plan.kick){
            if(!PlanExitKick(board,cargo,cell,direction,height,ref plan))return false;
        }
        return true;
    }
    static MagnetProduct PortalCargoRecipe(MagnetPiece cargo,MagnetPiece receiver,bool supported,Vector2Int direction){
        // Entry already performed the push flip. A throw preserves that pose;
        // same-facing U sockets stack instead of doing another flip-catch roll.
        if(!cargo.combined&&!receiver.combined&&cargo.north!=receiver.north
            &&cargo.product==MagnetProduct.None&&receiver.product==MagnetProduct.None
            &&cargo.shape==MagnetShape.Horseshoe&&receiver.shape==MagnetShape.Horseshoe
            &&MagnetPiece.FlatU(cargo.Pose)&&MagnetPiece.FlatU(receiver.Pose)
            &&Vector3.Dot(cargo.Pose*Vector3.forward,receiver.Pose*Vector3.forward)>.95f)
            return MagnetProduct.ThickU;
        return GridPlayground.Recipe(cargo,cargo.Pose,receiver,supported,direction);
    }
    static bool PlanExitKick(GridPlayground board,MagnetPiece cargo,Vector2Int cell,Vector2Int direction,float height,ref ExitPlan plan){
        // A flat ring can be reported as a walkable deck. During a toppling
        // impact it is a body to displace, never a surface to stack onto.
        var flatReceiver=board.PortalObstacle(cell+direction);
        if(cargo.product==MagnetProduct.Ring&&!MagnetPiece.FlatU(cargo.Pose)
            &&flatReceiver&&flatReceiver.product==MagnetProduct.Ring&&MagnetPiece.FlatU(flatReceiver.Pose)
            &&Mathf.Abs(flatReceiver.transform.position.y-height)<.01f){
            var receiverTile=board.TileAt(cell+direction);
            if(receiverTile&&(receiverTile.blocked||Mathf.Abs(receiverTile.surfaceHeight-height)>.01f))return false;
            var beyondCell=cell+direction*2;
            if(board.MagnetAt(beyondCell))return false;
            var beyond=board.InspectMagnetDestination(cell+direction,height,beyondCell,flatReceiver);
            if(beyond.kind!=GridPlayground.MagnetLandingKind.Ground&&beyond.kind!=GridPlayground.MagnetLandingKind.Deck
                &&beyond.kind!=GridPlayground.MagnetLandingKind.Space)return false;
            plan.kicked=flatReceiver.transform.position;
            plan.kickDisplaced=flatReceiver;plan.kickDisplacedPosition=beyond.position;return true;
        }
        var arrival=board.InspectMagnetDestination(cell,height,cell+direction,cargo);
        plan.kicked=arrival.position;
        if(arrival.kind==GridPlayground.MagnetLandingKind.Magnet){
            var receiver=arrival.obstacle;
            // The impact performs one normal flip of the movable receiver.
            // Validate its entire next cell before any animation.
            if(cargo.product==MagnetProduct.Ring&&!MagnetPiece.FlatU(cargo.Pose)
                &&receiver
                &&receiver.CanBePushed&&Mathf.Abs(receiver.transform.position.y-height)<.01f){
                var beyond=board.InspectMagnetDestination(cell+direction,height,cell+direction*2,receiver);
                if(beyond.kind!=GridPlayground.MagnetLandingKind.Ground&&beyond.kind!=GridPlayground.MagnetLandingKind.Deck
                    &&beyond.kind!=GridPlayground.MagnetLandingKind.Space)return false;
                plan.kickDisplaced=receiver;plan.kickDisplacedPosition=beyond.position;return true;
            }
            if(cargo.product!=MagnetProduct.None||cargo.shape!=MagnetShape.Bar
                ||!MagnetPiece.VerticalBar(cargo.Pose)||!receiver||receiver.shape!=MagnetShape.Bar
                ||receiver.combined||receiver.product!=MagnetProduct.None||receiver.north==cargo.north
                ||MagnetPiece.VerticalBar(receiver.Pose)||Mathf.Abs(receiver.transform.position.y-height)>.01f)return false;
            float degrees;var fallen=MagnetPiece.Rolled(cargo.shape,cargo.Pose,direction,out degrees);
            var product=GridPlayground.Recipe(cargo,fallen,receiver,board.TileAt(cell+direction),direction);
            if(product!=MagnetProduct.WideBar)return false;
            plan.kickReceiver=receiver;plan.kickProduct=product;return true;
        }
        if(arrival.kind!=GridPlayground.MagnetLandingKind.Blocked)return arrival.kind!=GridPlayground.MagnetLandingKind.Magnet;
        var solid=board.TileAt(cell+direction);
        // A solid next cell stops the tip, but does not prevent the initial ejection.
        plan.kickRebound=cargo.product==MagnetProduct.None&&cargo.shape==MagnetShape.Bar
            &&MagnetPiece.VerticalBar(cargo.Pose)&&solid&&(solid.blocked||solid.surfaceHeight>height+.01f);
        return plan.kickRebound;
    }
    static float ImpactPushDegrees(MagnetPiece magnet,Vector2Int direction){
        if(magnet.product!=MagnetProduct.Ring){
            float degrees;MagnetPiece.Rolled(magnet.shape,magnet.Pose,direction,out degrees);return degrees;
        }
        var forward=new Vector3(direction.x,0,direction.y);
        return MagnetPiece.FlatU(magnet.Pose)||Mathf.Abs(Vector3.Dot(magnet.Pose*Vector3.up,forward))>.95f?90:180;
    }
    static void ApplyImpactPush(MagnetPiece magnet,Vector3 start,Vector3 end,Quaternion pose,Vector3 axis,float degrees,float progress){
        magnet.transform.position=Vector3.Lerp(start,end,progress);
        magnet.geometry.rotation=Quaternion.AngleAxis(degrees*progress,axis)*pose;
        MagnetVisuals.Ground(magnet);
        if(magnet.product==MagnetProduct.Ring)magnet.walkable=progress>=1&&MagnetPiece.FlatU(magnet.Pose);
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
                if(p.y<=portal.tile.surfaceHeight+.02f||Mathf.Abs(delta.x)>portal.board.cellSize*.6f||Mathf.Abs(delta.z)>portal.board.cellSize*.6f)continue;
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
                var receivingPortal=BlackHolePortal.At(board,target.Cell+direction);
                if(receivingPortal&&receivingPortal.CanArrive&&cargo.portalRollSpeed<=0){
                    yield return ThrowCargoIntoPortal(cargo,portalPosition,receivingPortal,pose,scale);
                    if(!plan.separateCargo)yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
                    yield break;
                }
                if(cargo.product==MagnetProduct.Ring&&cargo.portalRollSpeed>0){
                    yield return board.RollRingFromPortal(cargo,target,direction);
                    yield break;
                }
                float landingHeight;
                bool slide=!plan.bounce&&board.TryPortalFloor(target.Cell+direction,out landingHeight);
                bool uprightThrow=!plan.bounce&&cargo.product==MagnetProduct.Ring&&!MagnetPiece.FlatU(pose);
                yield return ThrowCargo(cargo,portalPosition,plan.magnet,pose,scale,
                    plan.bounce?.28f:uprightThrow?.38f:.55f,plan.bounce?.24f:uprightThrow?.24f:.38f,
                    plan.displaced,plan.displacedPosition,slide,!plan.bounce,uprightThrow);
                if(plan.floatRing)yield return board.FloatPortalRing(cargo);
                // Stone contact reverses immediately; clear landings briefly settle first.
                if(!plan.bounce)yield return AnimationWait(.12f);
                if(plan.separateCargo){
                    if(plan.receiver)yield return board.AssemblePortalCargo(cargo,plan.receiver,direction,plan.product);
                    else yield return board.SettleMagnetArrival(cargo,portalPosition,true);
                    yield break;
                }
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
        var portalAudio=PortalAudio.For(player);if(portalAudio)portalAudio.Exit(false);
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
            bool landingPlayed=false;
            for(float t=0;t<1.05f;t+=AnimationDeltaTime){
                PlayPlayerLandingAhead(portalAudio,t,1.05f,ref landingPlayed);
                float a=Mathf.Clamp01(t/1.05f);player.localScale=scale*Mathf.Clamp01(a*5);
                player.position=ThrownPosition(hole,landing,a);
                // Blend into the same prone pose used by the recovery, without a pose reset.
                pose.SampleUnstablePortalArrival(a);yield return null;
            }
            player.localScale=scale;player.position=landing;
            PlayPlayerLandingAhead(portalAudio,1.05f,1.05f,ref landingPlayed);
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
            bool returningSound=false;
            for(float t=0;t<flightSeconds;t+=AnimationDeltaTime){
                if(!returningSound&&t>=flightSeconds*.7f){returningSound=true;if(portalAudio)portalAudio.Enter(false,flightSeconds-t,true);}
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
            board.AnimatePortalMagnet(magnet,false);pose.End();if(portalAudio)portalAudio.Stop(false);
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
                    // Continue the outward quarter-turn. For a ring lifted into the
                    // portal this lands on its opposite face; aligning its normal
                    // with world up would tip it backwards into the emerging actor.
                    var fallen=Quaternion.AngleAxis(90,axis)*pose;
                    var kickPose=PlayerPushPose.Begin(player);
                    var portalAudio=PortalAudio.For(player);if(portalAudio)portalAudio.Exit(false);
                    var landing=PortalProneLanding(player,kickPose,plan.player,playerScale);
                    // The same docking animation used by a normal push tips the bar
                    // into its receiver while the actor lands in the vacated cell.
                    var assembly=plan.kickReceiver?board.AssemblePortalCargo(cargo,plan.kickReceiver,direction,plan.kickProduct):null;
                    var motions=new System.Collections.Generic.Stack<IEnumerator>();
                    var displacedStart=plan.kickDisplaced?plan.kickDisplaced.transform.position:Vector3.zero;
                    var displacedPose=plan.kickDisplaced?plan.kickDisplaced.Pose:Quaternion.identity;
                    float displacedDegrees=plan.kickDisplaced?ImpactPushDegrees(plan.kickDisplaced,direction):0;
                    // The falling ring presses the flat ring ahead along the
                    // ground; it must remain flat in its own next grid cell.
                    if(plan.kickDisplaced&&plan.kickDisplaced.product==MagnetProduct.Ring
                        &&MagnetPiece.FlatU(displacedPose))displacedDegrees=0;
                    if(assembly!=null)motions.Push(assembly);
                    bool landingPlayed=false;
                    for(float t=0;t<.8f;t+=AnimationDeltaTime){
                        PlayPlayerLandingAhead(portalAudio,t,.8f,ref landingPlayed);
                        float a=Mathf.SmoothStep(0,1,t/.8f);
                        if(assembly!=null){
                            while(motions.Count>0){var motion=motions.Peek();if(!motion.MoveNext()){motions.Pop();continue;}if(motion.Current is IEnumerator child){motions.Push(child);continue;}break;}
                        }else{
                        // Let the actor approach before the ring responds to impact.
                        float fall=cargo.product==MagnetProduct.Ring
                            ?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,a)):a;
                        cargo.transform.position=Vector3.Lerp(plan.magnet,plan.kicked,fall);
                        cargo.geometry.rotation=Quaternion.AngleAxis(90*fall,axis)*pose;MagnetVisuals.Ground(cargo);
                        if(plan.kickDisplaced){
                            ApplyImpactPush(plan.kickDisplaced,displacedStart,plan.kickDisplacedPosition,displacedPose,axis,displacedDegrees,fall);
                        }
                        }
                        kickPose.SamplePortalFlight(a);
                        player.position=ThrownPosition(portalPosition,landing,a);yield return null;
                    }
                    if(assembly==null){
                    cargo.transform.position=plan.kicked;cargo.geometry.rotation=fallen;MagnetVisuals.Ground(cargo);
                    if(cargo.product==MagnetProduct.Ring)cargo.walkable=MagnetPiece.FlatU(cargo.Pose);
                    if(plan.kickDisplaced){
                        ApplyImpactPush(plan.kickDisplaced,displacedStart,plan.kickDisplacedPosition,displacedPose,axis,displacedDegrees,1);
                    }
                    }
                    player.position=landing;kickPose.SamplePortalProne();
                    PlayPlayerLandingAhead(portalAudio,.8f,.8f,ref landingPlayed);
                    while(motions.Count>0){var motion=motions.Peek();if(!motion.MoveNext()){motions.Pop();continue;}if(motion.Current is IEnumerator child){motions.Push(child);continue;}yield return null;}
                    if(plan.kickDisplaced)yield return board.SettleMagnetArrival(plan.kickDisplaced,displacedStart,true);
                    if(assembly==null)yield return board.SettleMagnetArrival(cargo,plan.magnet,true);
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
        var portalAudio=PortalAudio.For(player);if(portalAudio)portalAudio.Exit(false);
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
            bool returningSound=false;
            for(float t=0;t<.95f;t+=AnimationDeltaTime){
                if(!returningSound&&t>=.65f){returningSound=true;if(portalAudio)portalAudio.Enter(false,.95f-t,true);}
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
        var portalAudio=PortalAudio.For(player);if(portalAudio)portalAudio.Exit(false);
        try{
            // The actor must reach the upright piece before it starts sliding or tipping.
            for(float t=0;t<.52f;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/.52f);
                player.localScale=scale*Mathf.SmoothStep(0,1,Mathf.Clamp01(a/.25f));
                player.position=Vector3.Lerp(hole,contact,a)+Vector3.up*(.65f*Mathf.Sin(a*Mathf.PI));
                pose.SamplePortalFlight(a*.5f);yield return null;
            }
            player.position=contact;player.localScale=scale;
            bool returningSound=false;
            for(float t=0;t<.85f;t+=AnimationDeltaTime){
                if(!returningSound&&t>=.58f){returningSound=true;if(portalAudio)portalAudio.Enter(false,.85f-t,true);}
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
                yield return ThrowCargo(cargo,hole,hole+axis*(board.cellSize*.35f),savedPose,savedScale,.5f,.9f,landingSound:false);
            }
            var portalAudio=PortalAudio.For(player);if(portalAudio)portalAudio.Exit(false);
            bool returningSound=false;
            for(float t=0;t<1.15f;t+=AnimationDeltaTime){
                if(!returningSound&&t>=1.15f*.65f){
                    returningSound=true;if(portalAudio){portalAudio.Enter(false,1.15f-t,true);if(cargo)portalAudio.Enter(true,1.15f-t,true);}
                }
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
        var portalAudio=PortalAudio.For(player);if(portalAudio)portalAudio.Exit(false);
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
            bool returningSound=false;
            for(float t=0;t<diveSeconds;t+=AnimationDeltaTime){
                if(!returningSound&&t>=diveSeconds*.65f){returningSound=true;if(portalAudio)portalAudio.Enter(false,diveSeconds-t,true);}
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
    IEnumerator ThrowCargoIntoPortal(MagnetPiece cargo,Vector3 start,BlackHolePortal receiving,Quaternion pose,Vector3 scale){
        var end=new Vector3(receiving.Cell.x*receiving.board.cellSize,receiving.tile.surfaceHeight,receiving.Cell.y*receiving.board.cellSize);
        var audio=PortalAudio.For(receiving.board.player);if(audio)audio.Exit(true);
        bool entering=false;
        const float seconds=.45f;
        for(float elapsed=0;elapsed<seconds;){
            elapsed=Mathf.Min(seconds,elapsed+AnimationDeltaTime);float p=elapsed/seconds;
            if(!entering&&p>=.65f){entering=true;if(audio)audio.Enter(true,seconds*.35f,true);}
            cargo.transform.position=Vector3.Lerp(start,end,p);
            cargo.geometry.rotation=pose;
            float sink=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,p));
            cargo.geometry.localScale=scale*Mathf.Clamp01(p/.15f)*(1-sink);
            MagnetVisuals.Ground(cargo);cargo.geometry.position+=Vector3.up*(.3f*4*p*(1-p)-.2f*sink);
            if(elapsed<seconds)yield return null;
        }
        receiving.board.StoreMagnetInPortal(cargo,receiving,start,scale);
    }
    IEnumerator EjectPlayer(Transform player,Vector3 start,Vector3 end,Vector3 scale){
        var board=origin?origin.board:null;
        var cell=board?new Vector2Int(Mathf.RoundToInt(end.x/board.cellSize),Mathf.RoundToInt(end.z/board.cellSize)):Vector2Int.zero;
        var receiving=board?BlackHolePortal.At(board,cell):null;
        if(receiving&&receiving.CanArrive&&(end-start).sqrMagnitude>.01f
            &&Mathf.Abs(end.y-receiving.tile.surfaceHeight)<.01f){
            yield return EjectIntoPortal(player,start,end,scale,receiving);
            yield break;
        }
        var flight=PlayerPushPose.Begin(player);
        var portalAudio=PortalAudio.For(player);if(portalAudio)portalAudio.Exit(false);
        var landing=PortalProneLanding(player,flight,end,scale);
        bool landingPlayed=false;
        for(float t=0;t<1.05f;t+=AnimationDeltaTime){
            PlayPlayerLandingAhead(portalAudio,t,1.05f,ref landingPlayed);
            float a=t/1.05f;player.localScale=scale*Mathf.Lerp(.05f,1,Mathf.Clamp01(a*5));
            player.position=ThrownPosition(start,landing,a);flight.SamplePortalFlight(a);yield return null;
        }
        player.localScale=scale;player.position=landing;
        PlayPlayerLandingAhead(portalAudio,1.05f,1.05f,ref landingPlayed);
        yield return RecoverPlayer(player,end);
    }
    IEnumerator EjectIntoPortal(Transform player,Vector3 start,Vector3 end,Vector3 scale,BlackHolePortal receiving){
        var flight=PlayerPushPose.Begin(player);var audio=PortalAudio.For(player);
        if(audio)audio.Exit(false);
        bool entering=false;
        const float duration=.85f;
        try{
            for(float elapsed=0;elapsed<duration;){
                elapsed=Mathf.Min(duration,elapsed+AnimationDeltaTime);
                float p=elapsed/duration;
                if(!entering&&p>=.65f){entering=true;if(audio)audio.Enter(false,duration*.35f,true);}
                // Continue the flight straight into the mouth, never sampling
                // the prone landing or recovery poses on the receiving tile.
                player.position=Vector3.Lerp(start,end-Vector3.up*.25f,p)+Vector3.up*(1.2f*4*p*(1-p));
                float emerge=Mathf.Clamp01(p/.18f);
                float sink=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,1,p));
                player.localScale=scale*emerge*(1-sink);
                flight.SamplePortalLedgeDive(p);
                if(elapsed<duration)yield return null;
            }
            var delta=end-start;
            chainedEntry=receiving;
            chainedEntryDirection=Mathf.Abs(delta.x)>Mathf.Abs(delta.z)
                ?new Vector2Int(delta.x>0?1:-1,0):new Vector2Int(0,delta.z>0?1:-1);
        }finally{flight.End();player.position=end;player.localScale=Vector3.zero;}
    }
    // Follow animation time (including pause/skip), with one short output-latency lead.
    static void PlayPlayerLandingAhead(PortalAudio audio,float elapsed,float duration,ref bool played){
        if(played||elapsed<duration-.10f)return;
        played=true;if(audio)audio.Land(false);
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
    IEnumerator ThrowCargo(MagnetPiece cargo,Vector3 start,Vector3 end,Quaternion pose,Vector3 scale,float seconds,float arc,MagnetPiece displaced=null,Vector3 displacedEnd=default(Vector3),bool slide=false,bool landingSound=true,bool uprightThrow=false){
        var portalAudio=PortalAudio.For(origin&&origin.board?origin.board.player:null);
        if(portalAudio)portalAudio.Exit(true);
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
            // Rings burst outward then ease off the launch impulse, retaining
            // forward motion through the apex instead of hanging in place.
            float horizontal=cargo.product==MagnetProduct.Ring?GridPlayground.PortalRingThrowProgress(a):a;
            cargo.transform.position=Vector3.Lerp(start,touchdown,horizontal);
            if(displaced)displaced.transform.position=Vector3.Lerp(displacedStart,displacedEnd,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.65f,a)));
            // A short, low forward throw reads as an impulse. Keep the lean
            // through flight, then straighten quickly just before touchdown.
            float lean=(uprightThrow?24:12)*Mathf.Sin(Mathf.PI*Mathf.Clamp01(a/(uprightThrow?.16f:.22f))*.5f)
                *(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(uprightThrow?.82f:.72f,1,a)));
            cargo.geometry.rotation=Quaternion.AngleAxis(lean,tiltAxis)*pose;MagnetVisuals.Ground(cargo);
            // One ballistic parabola gives a continuous upward impulse and
            // constant downward acceleration, without a pause at the apex.
            float airborneY=Mathf.Lerp(start.y-.2f,end.y,a)+4*arc*a*(1-a);
            float lift=airborneY-cargo.transform.position.y;
            cargo.geometry.position+=Vector3.up*lift;yield return null;
        }
        // Contact precedes the new ground-slide phase. Bounces have no floor landing.
        if(portalAudio){if(landingSound)portalAudio.Land(true);else portalAudio.Stop(true);}
        if(slide){
            cargo.transform.position=touchdown;cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);
            // Ground friction removes the remaining forward momentum within this cell.
            float slideSeconds=uprightThrow?.10f:.18f;
            for(float t=0;t<slideSeconds;t+=AnimationDeltaTime){
                float a=Mathf.Clamp01(t/slideSeconds),travel=1-(1-a)*(1-a);
                cargo.transform.position=Vector3.Lerp(touchdown,end,travel);
                MagnetVisuals.Ground(cargo);yield return null;
            }
        }
        if(displaced)displaced.transform.position=displacedEnd;
        cargo.transform.position=end;cargo.geometry.rotation=pose;cargo.geometry.localScale=scale;MagnetVisuals.Ground(cargo);
    }
    IEnumerator BounceCargo(MagnetPiece cargo,BlackHolePortal source,BlackHolePortal target,Vector3 hit,Vector3 hole,Quaternion pose,Vector3 scale,Vector3 playerScale,bool storeAtMouth=false){
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
        // Meet the emerging actor on a short rebound, not a high vertical lob.
        // Both bodies share gravity and reach the contact height on one clock.
        const float reboundSeconds=.36f,returnSeconds=.34f;
        float gravity=target.board.cellSize*18f;
        float velocity=(contactWorldY-launchBottom+.5f*gravity*reboundSeconds*reboundSeconds)/reboundSeconds;
        float impactTime=-1;
        float launchDelay=0;
        float playerVelocity=(emergedY-hiddenY+.5f*gravity*reboundSeconds*reboundSeconds)/reboundSeconds;
        float impactPlayerHeight=0;
        var portalAudio=PortalAudio.For(player);bool playerSound=false,returnSound=false;
        try{
            // Slow the whole collision clock equally to preserve the meeting
            // point and continuous rebound trajectory.
            for(float t=0;;t+=AnimationDeltaTime*.85f){
                if(!playerSound&&t>=launchDelay){playerSound=true;if(portalAudio)portalAudio.Exit(false);}
                if(!returnSound&&impactTime>=0&&t>=impactTime+.1f){
                    returnSound=true;if(portalAudio){portalAudio.Enter(false,returnSeconds,true);portalAudio.Enter(true,returnSeconds,true);}
                }
                if(impactTime>=0&&t>=impactTime+returnSeconds)break;
                float impact=impactTime<0?0:Mathf.SmoothStep(0,1,Mathf.InverseLerp(impactTime,impactTime+.08f,t));
                float sink=impactTime<0?0:Mathf.SmoothStep(0,1,Mathf.InverseLerp(impactTime+.04f,impactTime+returnSeconds,t));
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
                    float fallTime=t-impactTime;
                    float downwardSpeed=Mathf.Max(target.board.cellSize*1.2f,gravity*impactTime-velocity);
                    player.position=hole+Vector3.up*(impactPlayerHeight-downwardSpeed*fallTime-.5f*gravity*fallTime*fallTime);
                    reaction.SamplePortalHeadImpact(impact,sink);
                }
                // Horizontal rebound continues until impact; no apex parking.
                float approach=Mathf.Clamp01(t/reboundSeconds);
                cargo.transform.position=Vector3.Lerp(hit,hole,approach);
                cargo.geometry.localScale=scale*remaining;cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);
                float top=TransitMeshHeight(playerMeshes,vertices,true,player.position.y+bodyTop);
                float bottom=TransitMeshHeight(cargoMeshes,vertices,false,hole.y);
                float ballisticBottom=launchBottom+velocity*t-.5f*gravity*t*t;
                // Use actual vertices: renderer bounds are inflated for island rendering.
                // On impact the cargo follows the posed actor, never sinking independently.
                float contactY=Mathf.Max(top,hole.y)+clearance*remaining;
                if(impactTime<0&&t>=reboundSeconds&&ballisticBottom<=contactY){
                    impactTime=t;
                    impactPlayerHeight=player.position.y-hole.y;
                }
                float cargoY=impactTime<0?ballisticBottom:contactY;
                cargo.geometry.position+=Vector3.up*(cargoY-bottom);
                yield return null;
            }
        }finally{reaction.End();player.localScale=Vector3.zero;cargo.geometry.localScale=Vector3.zero;}
        cargo.gameObject.SetActive(false);
        if(storeAtMouth){
            cargo.storedInPortal=target;cargo.transform.position=hole;
            cargo.geometry.rotation=pose;cargo.geometry.localScale=scale;MagnetVisuals.Ground(cargo);
            yield break;
        }
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
