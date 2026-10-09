using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    public struct ExitPlan {
        public Vector3 player,magnet,kicked;
        public bool kick;
    }
    public static bool PlanExit(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,out ExitPlan plan){
        plan=new ExitPlan();if(!target||!target.CanArrive||target.Cargo||Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1)return false;
        var board=target.board;var cell=target.Cell+direction;GridTile tile;Vector3 top;
        if(!cargo){if(!target.TryExitLanding(direction,out tile,out top))return false;plan.player=top;return true;}
        if(board.MagnetAt(cell)||board.IsWreckCell(cell)||BlackHolePortal.At(board,cell))return false;
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
    IEnumerator Eject(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,ExitPlan plan){
        var board=target.board;var player=board.player;var playerScale=player.localScale;
        board.SetPortalBusy(true);
        var portalPosition=new Vector3(target.tile.transform.position.x,target.tile.surfaceHeight,target.tile.transform.position.z);
        try{
            if(cargo){
                player.localScale=Vector3.zero;
                var pose=cargo.Pose;var scale=cargo.geometry.localScale;
                cargo.storedInPortal=null;cargo.transform.position=portalPosition;
                var owner=target.tile.GetComponentInParent<IslandSurfaceAnchor>();var binding=cargo.GetComponent<IslandSurfaceAnchor>();
                if(binding&&owner){binding.center=owner.center;binding.Apply();}
                cargo.gameObject.SetActive(true);
                for(float t=0;t<.65f;t+=Time.unscaledDeltaTime){
                    float a=Mathf.SmoothStep(0,1,t/.65f);cargo.transform.position=Vector3.Lerp(portalPosition,plan.magnet,a);
                    cargo.geometry.localScale=scale*Mathf.Lerp(.05f,1,a);cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);yield return null;
                }
                cargo.transform.position=plan.magnet;cargo.geometry.localScale=scale;cargo.geometry.rotation=pose;MagnetVisuals.Ground(cargo);
                // Cargo is visibly in position before the player begins emerging.
                yield return new WaitForSecondsRealtime(.16f);
                if(plan.kick){
                    player.localScale=playerScale;var axis=new Vector3(direction.y,0,-direction.x);
                    var fallen=cargo.product==MagnetProduct.Ring?Quaternion.FromToRotation(pose*Vector3.up,Vector3.up)*pose:Quaternion.AngleAxis(90,axis)*pose;
                    var kickPose=PlayerPushPose.Begin(player);
                    for(float t=0;t<.8f;t+=Time.unscaledDeltaTime){
                        float a=Mathf.SmoothStep(0,1,t/.8f);
                        cargo.transform.position=Vector3.Lerp(plan.magnet,plan.kicked,a);
                        cargo.geometry.rotation=Quaternion.Slerp(pose,fallen,a);MagnetVisuals.Ground(cargo);kickPose.SamplePortalKick(a);
                        player.position=Vector3.Lerp(portalPosition,plan.player,a);yield return null;
                    }
                    kickPose.End();cargo.transform.position=plan.kicked;cargo.geometry.rotation=fallen;MagnetVisuals.Ground(cargo);
                    if(cargo.product==MagnetProduct.Ring)cargo.walkable=MagnetPiece.FlatU(cargo.Pose);
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
    IEnumerator EjectPlayer(Transform player,Vector3 start,Vector3 end,Vector3 scale){
        var raised=start;raised.y=Mathf.Max(start.y,end.y);
        for(float t=0;t<.6f;t+=Time.unscaledDeltaTime){
            float a=Mathf.Clamp01(t/.6f);player.localScale=scale*Mathf.Lerp(.05f,1,Mathf.Clamp01(a*2));
            player.position=a<.35f?Vector3.Lerp(start,raised,Mathf.SmoothStep(0,1,a/.35f)):Vector3.Lerp(raised,end,Mathf.SmoothStep(0,1,(a-.35f)/.65f));yield return null;
        }
        player.localScale=scale;player.position=end;
    }
}
}
