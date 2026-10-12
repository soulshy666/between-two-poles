using System.Collections;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    public struct ExitPlan {
        public Vector3 player,magnet,kicked;
        public bool kick,bounce,unstableLanding;
        public MagnetPiece receiver;
        public MagnetPiece displaced;
        public MagnetPiece exitKickTarget;
        public Vector3 displacedPosition;
        public MagnetProduct product;
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
                var next=cell+direction;var ground=board.TileAt(next);
                if(board.MagnetAt(next)||board.IsWreckCell(next)||BlackHolePortal.At(board,next)
                    ||(ground&&(ground.blocked||Mathf.Abs(ground.surfaceHeight-tile.surfaceHeight)>.01f)))return false;
                plan.exitKickTarget=obstacle;plan.kick=true;plan.magnet=obstacle.transform.position;
                plan.player=new Vector3(cell.x*board.cellSize,tile.surfaceHeight,cell.y*board.cellSize);
                plan.kicked=new Vector3(next.x*board.cellSize,tile.surfaceHeight,next.y*board.cellSize);
            }else plan.player=top;
            return true;
        }
        if(board.IsWreckCell(cell)||BlackHolePortal.At(board,cell))return false;
        var receiver=board.MagnetAt(cell);
        tile=board.TileAt(cell);
        if(tile&&tile.blocked){
            if(!BlackHolePortal.TrySurface(board,cell,out tile,out top))return false;
            var source=cargo.storedInPortal;if(!source)return false;
            var returnCell=new Vector2Int(Mathf.RoundToInt(cargo.portalEntryPosition.x/source.board.cellSize),Mathf.RoundToInt(cargo.portalEntryPosition.z/source.board.cellSize));
            var ground=source.board.TileAt(returnCell);
            if(!ground||ground.blocked||source.board.MagnetAt(returnCell)||source.board.IsWreckCell(returnCell)||BlackHolePortal.At(source.board,returnCell))return false;
            plan.kick=cargo.product==MagnetProduct.Ring?!MagnetPiece.FlatU(cargo.Pose):cargo.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(cargo.Pose);
            if(plan.kick){
                var back=returnCell-source.Cell;var next=returnCell+back;var nextGround=source.board.TileAt(next);
                if(source.board.MagnetAt(next)||source.board.IsWreckCell(next)||BlackHolePortal.At(source.board,next)
                    ||(nextGround&&(nextGround.blocked||Mathf.Abs(nextGround.surfaceHeight-ground.surfaceHeight)>.01f)))return false;
                plan.kicked=new Vector3(next.x*source.board.cellSize,ground.surfaceHeight,next.y*source.board.cellSize);
            }
            plan.bounce=true;plan.player=cargo.portalEntryPosition;
            plan.magnet=StoneContact(target,tile,cargo,direction);return true;
        }
        if(receiver){
            if(!cargo.combined&&!receiver.combined&&cargo.north==receiver.north){
                var next=cell+direction;var ground=board.TileAt(next);
                if(!tile||tile.blocked||Mathf.Abs(tile.surfaceHeight-target.tile.surfaceHeight)>.01f
                    ||Mathf.Abs(receiver.transform.position.y-tile.surfaceHeight)>.01f
                    ||board.MagnetAt(next)||board.IsWreckCell(next)||BlackHolePortal.At(board,next)
                    ||(ground&&(ground.blocked||Mathf.Abs(ground.surfaceHeight-tile.surfaceHeight)>.01f)))return false;
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
        if(!tile||tile.blocked)return false;
        float height=tile?tile.surfaceHeight:target.tile.surfaceHeight;
        if(Mathf.Abs(height-target.tile.surfaceHeight)>.01f)return false;
        plan.magnet=new Vector3(cell.x*board.cellSize,height,cell.y*board.cellSize);
        plan.kick=cargo.product==MagnetProduct.Ring?!MagnetPiece.FlatU(cargo.Pose):cargo.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(cargo.Pose);
        plan.player=plan.magnet;
        if(plan.kick){
            if(!tile)return false; // The player must stand where the upright cargo first emerges.
            var next=cell+direction;var ground=board.TileAt(next);
            if(board.MagnetAt(next)||board.IsWreckCell(next)||BlackHolePortal.At(board,next)||(ground&&(ground.blocked||Mathf.Abs(ground.surfaceHeight-height)>.01f)))return false;
            plan.kicked=new Vector3(next.x*board.cellSize,height,next.y*board.cellSize);
        }
        return true;
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
                yield return ThrowCargo(cargo,portalPosition,plan.magnet,pose,scale,plan.bounce?.28f:1.05f,plan.bounce?.32f:1.5f,plan.displaced,plan.displacedPosition);
                // Stone contact reverses immediately; clear landings briefly settle first.
                if(!plan.bounce)yield return new WaitForSecondsRealtime(.12f);
                if(plan.bounce){
                    yield return BounceCargo(cargo,source,target,plan.magnet,portalPosition,pose,scale,playerScale);
                    var returnCell=new Vector2Int(Mathf.RoundToInt(cargo.portalEntryPosition.x/board.cellSize),Mathf.RoundToInt(cargo.portalEntryPosition.z/board.cellSize));
                    GridTile returnTile;Vector3 returnTop;
                    if(!BlackHolePortal.TrySurface(source.board,returnCell,out returnTile,out returnTop))throw new System.InvalidOperationException("Returned cargo has no landing surface");
                    var returnStart=new Vector3(source.tile.transform.position.x,source.tile.surfaceHeight,source.tile.transform.position.z);
                    if(plan.kick){
                        var returnDirection=returnCell-source.Cell;
                        var returnPlan=new ExitPlan{magnet=cargo.portalEntryPosition,player=cargo.portalEntryPosition,kicked=plan.kicked,kick=true};
                        yield return KickExitPlayer(player,cargo,returnStart,returnDirection,returnPlan,pose,playerScale);
                    }else yield return EjectPlayer(player,returnStart,returnTop,playerScale);
                    board.CompletePortalLanding(returnCell);
                    yield break;
                }else if(plan.receiver){
                    yield return board.AssemblePortalCargo(cargo,plan.receiver,direction,plan.product);
                    GridTile landingTile;Vector3 landing;
                    if(!BlackHolePortal.TrySurface(board,target.Cell+direction,out landingTile,out landing))throw new System.InvalidOperationException("Assembly has no landing surface");
                    plan.player=landing;yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
                }else if(plan.kick){
                    yield return KickExitPlayer(player,cargo,portalPosition,direction,plan,pose,playerScale);
                }else{
                    GridTile tile;Vector3 top;
                    if(BlackHolePortal.TrySurface(board,target.Cell+direction,out tile,out top))plan.player=top;
                    // TrySurface chooses actual solid top triangles, avoiding U/ring holes.
                    yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
                }
            }else if(plan.unstableLanding){
                yield return UnstablePortalLanding(player,target,direction,portalPosition,plan.player,playerScale);
                yield break;
            }else if(plan.exitKickTarget){
                yield return KickExitPlayer(player,plan.exitKickTarget,portalPosition,direction,plan,plan.exitKickTarget.Pose,playerScale);
            }else yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
            player.position=plan.player;board.CompletePortalLanding(target.Cell+direction);
        }finally{if(player){player.localScale=playerScale;var pose=player.GetComponent<PlayerPushPose>();if(pose)pose.End();}if(board)board.SetPortalBusy(false);}
    }
    IEnumerator UnstablePortalLanding(Transform player,BlackHolePortal portal,Vector2Int direction,Vector3 hole,Vector3 landing,Vector3 scale){
        var pose=PlayerPushPose.Begin(player);
        var outward=new Vector3(direction.x,0,direction.y);
        // Hands face the island when hanging; root is outside the lip, below its surface.
        var lip=hole+outward*(portal.board.cellSize*.5f);
        var hanging=lip+outward*.22f-Vector3.up*.82f;
        try{
            for(float t=0;t<1.05f;t+=Time.unscaledDeltaTime){
                float a=t/1.05f;player.localScale=scale*Mathf.Clamp01(a*5);
                player.position=ThrownPosition(hole,landing,a);pose.SamplePortalFlight(a);yield return null;
            }
            player.localScale=scale;player.position=landing;
            for(float t=0;t<1.1f;t+=Time.unscaledDeltaTime){pose.SamplePortalLedge(0,t/1.1f);yield return null;}
            var facing=player.rotation;
            for(float t=0;t<.5f;t+=Time.unscaledDeltaTime){
                float a=Mathf.Clamp01(t/.5f);
                player.position=Vector3.Lerp(landing,hanging,a*a);
                player.rotation=Quaternion.Slerp(facing,Quaternion.LookRotation(-outward),Mathf.Clamp01(a*2));
                pose.SamplePortalLedge(Mathf.SmoothStep(0,1,a),0);yield return null;
            }
            player.position=hanging;player.rotation=Quaternion.LookRotation(-outward);
            for(float t=0;t<.65f;t+=Time.unscaledDeltaTime){pose.SamplePortalLedge(1,0);yield return null;}
            var standing=lip-outward*.28f;
            for(float t=0;t<.95f;t+=Time.unscaledDeltaTime){
                float a=Mathf.SmoothStep(0,1,t/.95f);
                var lifted=hanging+Vector3.up*(hole.y-hanging.y+.12f);
                player.position=a<.65f?Vector3.Lerp(hanging,lifted,a/.65f):Vector3.Lerp(lifted,standing,(a-.65f)/.35f);
                pose.SamplePortalLedge(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,a)),0);yield return null;
            }
            player.position=standing;
            for(float t=0;t<.65f;t+=Time.unscaledDeltaTime){
                float a=Mathf.Clamp01(t/.65f);player.position=Vector3.Lerp(standing,hole,a)+Vector3.up*(.5f*Mathf.Sin(a*Mathf.PI));
                pose.SamplePortalEntry(a);yield return null;
            }
            player.position=hole;player.localScale=Vector3.zero;
        }finally{pose.End();}
    }
    IEnumerator KickExitPlayer(Transform player,MagnetPiece cargo,Vector3 portalPosition,Vector2Int direction,ExitPlan plan,Quaternion pose,Vector3 playerScale){
                    player.localScale=playerScale;var axis=new Vector3(direction.y,0,-direction.x);
                    var fallen=cargo.product==MagnetProduct.Ring?Quaternion.FromToRotation(pose*Vector3.up,Vector3.up)*pose:Quaternion.AngleAxis(90,axis)*pose;
                    var kickPose=PlayerPushPose.Begin(player);
                    for(float t=0;t<.8f;t+=Time.unscaledDeltaTime){
                        float a=Mathf.SmoothStep(0,1,t/.8f);
                        cargo.transform.position=Vector3.Lerp(plan.magnet,plan.kicked,a);
                        cargo.geometry.rotation=Quaternion.Slerp(pose,fallen,a);MagnetVisuals.Ground(cargo);kickPose.SamplePortalFlight(a);
                        player.position=ThrownPosition(portalPosition,plan.player,a);yield return null;
                    }
                    kickPose.End();cargo.transform.position=plan.kicked;cargo.geometry.rotation=fallen;MagnetVisuals.Ground(cargo);
                    if(cargo.product==MagnetProduct.Ring)cargo.walkable=MagnetPiece.FlatU(cargo.Pose);
                    player.position=plan.player;yield return RecoverPlayer(player);
    }
    static Vector3 ThrownPosition(Vector3 start,Vector3 end,float progress){
        float a=Mathf.Clamp01(progress),horizontal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,1,a));
        var p=Vector3.Lerp(start,end,horizontal);
        float peak=Mathf.Max(start.y,end.y)+.95f;
        p.y=a<.45f?Mathf.Lerp(start.y-.25f,peak,1-Mathf.Pow(1-a/.45f,2)):Mathf.Lerp(peak,end.y,Mathf.Pow((a-.45f)/.55f,2));
        return p;
    }
    IEnumerator EjectPlayer(Transform player,Vector3 start,Vector3 end,Vector3 scale){
        var flight=PlayerPushPose.Begin(player);
        for(float t=0;t<1.05f;t+=Time.unscaledDeltaTime){
            float a=t/1.05f;player.localScale=scale*Mathf.Lerp(.05f,1,Mathf.Clamp01(a*5));
            player.position=ThrownPosition(start,end,a);flight.SamplePortalFlight(a);yield return null;
        }
        player.localScale=scale;player.position=end;yield return RecoverPlayer(player);
    }
    IEnumerator RecoverPlayer(Transform player){
        var pose=PlayerPushPose.Begin(player);
        try{
            for(float t=0;t<1.25f;t+=Time.unscaledDeltaTime){
                float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,1.25f,t));
                pose.SampleCrashPose(1-rise,Mathf.Sin(rise*Mathf.PI));yield return null;
            }
        }finally{if(pose)pose.End();}
    }
    IEnumerator ThrowCargo(MagnetPiece cargo,Vector3 start,Vector3 end,Quaternion pose,Vector3 scale,float seconds,float arc,MagnetPiece displaced=null,Vector3 displacedEnd=default(Vector3)){
        var displacedStart=displaced?displaced.transform.position:Vector3.zero;
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
        for(float t=0;t<seconds;t+=Time.unscaledDeltaTime){
            float a=Mathf.Clamp01(t/seconds);
            // Emerge upward first, then travel out of the hole while gravity
            // accelerates the descent. Keep the logical base on the support plane.
            float horizontal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,1,a));
            cargo.transform.position=Vector3.Lerp(start,end,horizontal);
            if(displaced)displaced.transform.position=Vector3.Lerp(displacedStart,displacedEnd,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.65f,a)));
            cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);
            float peak=Mathf.Max(start.y,end.y)+arc;
            float airborneY=a<.42f
                ?Mathf.Lerp(start.y-.35f,peak,1-Mathf.Pow(1-a/.42f,2))
                :Mathf.Lerp(peak,end.y,Mathf.Pow((a-.42f)/.58f,2));
            float lift=airborneY-cargo.transform.position.y;
            cargo.geometry.position+=Vector3.up*lift;yield return null;
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
            for(float t=0;;t+=Time.unscaledDeltaTime){
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
        yield return ThrowCargo(cargo,start,cargo.portalEntryPosition,pose,scale,1.05f,1.5f);
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
