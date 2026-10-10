using System.Collections;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    public struct ExitPlan {
        public Vector3 player,magnet,kicked;
        public bool kick,bounce;
        public MagnetPiece receiver;
        public MagnetProduct product;
    }
    public static bool PlanExit(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,out ExitPlan plan){
        plan=new ExitPlan();if(!target||!target.CanArrive||target.Cargo||Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1)return false;
        var board=target.board;var cell=target.Cell+direction;GridTile tile;Vector3 top;
        if(!cargo){if(!target.TryExitLanding(direction,out tile,out top))return false;plan.player=top;return true;}
        if(board.IsWreckCell(cell)||BlackHolePortal.At(board,cell))return false;
        var receiver=board.MagnetAt(cell);
        tile=board.TileAt(cell);
        if(tile&&tile.blocked){
            if(!BlackHolePortal.TrySurface(board,cell,out tile,out top))return false;
            var source=cargo.storedInPortal;if(!source)return false;
            var returnCell=new Vector2Int(Mathf.RoundToInt(cargo.portalEntryPosition.x/source.board.cellSize),Mathf.RoundToInt(cargo.portalEntryPosition.z/source.board.cellSize));
            var ground=source.board.TileAt(returnCell);
            if(!ground||ground.blocked||source.board.MagnetAt(returnCell)||source.board.IsWreckCell(returnCell)||BlackHolePortal.At(source.board,returnCell))return false;
            plan.bounce=true;plan.player=top;
            plan.magnet=StoneContact(target,tile,cargo,direction);return true;
        }
        if(receiver){
            if(cargo.combined||Mathf.Abs(receiver.transform.position.y-target.tile.surfaceHeight)>.01f)return false;
            var product=GridPlayground.Recipe(cargo,cargo.Pose,receiver,tile&&!tile.blocked,direction);
            // Only a completed walking surface can safely receive the following player.
            if(product!=MagnetProduct.WideBar&&product!=MagnetProduct.Ring&&product!=MagnetProduct.Bridge)return false;
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
                yield return ThrowCargo(cargo,portalPosition,plan.magnet,pose,scale,plan.bounce?.42f:.7f,plan.bounce?.32f:.65f);
                // Stone contact reverses immediately; clear landings briefly settle first.
                if(!plan.bounce)yield return new WaitForSecondsRealtime(.12f);
                if(plan.bounce){
                    yield return BounceCargo(cargo,source,target,plan.magnet,portalPosition,pose,scale);
                    yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
                }else if(plan.receiver){
                    yield return board.AssemblePortalCargo(cargo,plan.receiver,direction,plan.product);
                    GridTile landingTile;Vector3 landing;
                    if(!BlackHolePortal.TrySurface(board,target.Cell+direction,out landingTile,out landing))throw new System.InvalidOperationException("Assembly has no landing surface");
                    plan.player=landing;yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
                }else if(plan.kick){
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
                }else{
                    GridTile tile;Vector3 top;
                    if(BlackHolePortal.TrySurface(board,target.Cell+direction,out tile,out top))plan.player=top;
                    // TrySurface chooses actual solid top triangles, avoiding U/ring holes.
                    yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
                }
            }else yield return EjectPlayer(player,portalPosition,plan.player,playerScale);
            player.position=plan.player;board.CompletePortalLanding(target.Cell+direction);
        }finally{if(player){player.localScale=playerScale;var pose=player.GetComponent<PlayerPushPose>();if(pose)pose.End();}if(board)board.SetPortalBusy(false);}
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
    IEnumerator ThrowCargo(MagnetPiece cargo,Vector3 start,Vector3 end,Quaternion pose,Vector3 scale,float seconds,float arc){
        cargo.geometry.localScale=scale;
        for(float t=0;t<seconds;t+=Time.unscaledDeltaTime){
            float a=Mathf.Clamp01(t/seconds);cargo.transform.position=Vector3.Lerp(start,end,a);
            cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);
            float lift=arc*Mathf.Sin(a*Mathf.PI)-.35f*Mathf.Max(0,1-a/.2f);
            cargo.geometry.position+=Vector3.up*lift;yield return null;
        }
        cargo.transform.position=end;cargo.geometry.rotation=pose;cargo.geometry.localScale=scale;MagnetVisuals.Ground(cargo);
    }
    IEnumerator BounceCargo(MagnetPiece cargo,BlackHolePortal source,BlackHolePortal target,Vector3 hit,Vector3 hole,Quaternion pose,Vector3 scale){
        // One immediate reversal into the hole, at full scale throughout.
        for(float t=0;t<.34f;t+=Time.unscaledDeltaTime){
            float a=Mathf.Clamp01(t/.34f);cargo.transform.position=Vector3.Lerp(hit,hole,1-Mathf.Pow(1-a,2));
            cargo.geometry.localScale=scale;cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);
            cargo.geometry.position+=Vector3.up*(.16f*Mathf.Sin(a*Mathf.PI)-2f*Mathf.Pow(a,3));yield return null;
        }
        cargo.gameObject.SetActive(false);
        if(source.board!=target.board){
            var carried=target.board.magnets.Where(m=>m&&m.transform.IsChildOf(cargo.transform)).ToArray();
            target.board.ReleasePortalGeometry(carried);target.board.magnets=target.board.magnets.Except(carried).ToArray();
            cargo.transform.SetParent(source.board.transform,true);source.board.magnets=source.board.magnets.Concat(carried).ToArray();
        }
        var binding=cargo.GetComponent<IslandSurfaceAnchor>();var owner=source.tile.GetComponentInParent<IslandSurfaceAnchor>();
        if(binding&&owner){binding.center=owner.center;binding.Apply();}
        var start=new Vector3(source.tile.transform.position.x,source.tile.surfaceHeight,source.tile.transform.position.z);
        cargo.transform.position=start;cargo.gameObject.SetActive(true);
        yield return ThrowCargo(cargo,start,cargo.portalEntryPosition,pose,scale,.7f,.65f);
        if(source.board!=target.board)source.board.CaptureInitialState();
    }
}
}
