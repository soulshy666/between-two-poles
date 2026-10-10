using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    IEnumerator JumpIntoPortal(BlackHolePortal portal,Vector2Int direction){
        var start=player.position;var end=Position(portal.Cell,portal.tile.surfaceHeight);
        yield return TurnPlayer(new Vector3(direction.x,0,direction.y));
        var pose=PlayerPushPose.Begin(player);
        for(float t=0;t<.95f;t+=MovementDeltaTime){
            float a=Mathf.Clamp01(t/.95f);var position=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,a));
            position.y+=Mathf.Sin(a*Mathf.PI)*.85f;player.position=position;pose.SamplePortalEntry(a);yield return null;
        }
        player.position=end;pose.SamplePortalEntry(1);NotifyLanding(portal.Cell);Busy=false;
        portal.Enter(direction);
    }
    bool BeginPortalPush(MagnetPiece magnet,BlackHolePortal portal,Vector2Int dir){
        if(portal.Cargo)return Reject("黑洞中已有一块磁铁，请先进入黑洞完成传送");
        if(!portal.CanArrive||portal.tile.blocked||!SameHeight(magnet.transform.position.y,portal.tile.surfaceHeight))return Reject("黑洞入口被占据或高度不一致");
        Busy=true;StartMovement(PushIntoPortal(magnet,portal,dir));LastRule="磁铁保持姿态进入黑洞；再走入黑洞选择出口";return true;
    }
    IEnumerator PushIntoPortal(MagnetPiece magnet,BlackHolePortal portal,Vector2Int dir){
        Vector3 start=magnet.transform.position,playerStart=player.position;
        magnet.portalEntryPosition=start;
        var vacated=Cell(magnet.transform);var end=Position(portal.Cell,portal.tile.surfaceHeight);
        var playerEnd=PushPlayerEnd(vacated,magnet);var pose=magnet.Pose;var scale=magnet.geometry.localScale;
        player.rotation=Quaternion.LookRotation(new Vector3(dir.x,0,dir.y));var push=PlayerPushPose.Begin(player);
        for(float t=0;t<.65f;t+=MovementDeltaTime){
            float a=Mathf.SmoothStep(0,1,t/.65f);magnet.transform.position=Vector3.Lerp(start,end,a);
            magnet.geometry.rotation=pose;magnet.geometry.localScale=scale*Mathf.Lerp(1,.05f,Mathf.SmoothStep(0,1,Mathf.Clamp01((a-.45f)/.55f)));
            MagnetVisuals.Ground(magnet);player.position=Vector3.Lerp(playerStart,playerEnd,a);push.Sample(a);yield return null;
        }
        magnet.transform.position=end;magnet.geometry.localScale=scale;magnet.geometry.rotation=pose;MagnetVisuals.Ground(magnet);
        magnet.storedInPortal=portal;magnet.gameObject.SetActive(false);
        push.End();player.position=playerEnd;NotifyLanding(vacated);Busy=false;
    }
    public void RecordPortalJourney(){RecordHistory(SaveWorld());ClearMovementInput();}
    public void CompletePortalLanding(Vector2Int cell){ClearMovementInput();NotifyLanding(cell);}
    public void SetPortalBusy(bool value){Busy=value;ClearMovementInput();}
    public void ReleasePortalGeometry(MagnetPiece[] carried){foreach(var piece in carried)if(piece&&piece.geometry)recordedGeometry.Remove(piece.geometry);}
}
}
