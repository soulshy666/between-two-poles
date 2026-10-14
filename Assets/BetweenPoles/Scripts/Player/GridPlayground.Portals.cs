using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    IEnumerator JumpIntoPortal(BlackHolePortal portal,Vector2Int direction){
        yield return BlackHoleTravel.PlayAnimation("player-entry",JumpIntoPortalAnimation(portal,direction));
        ClearMovementInput();portal.Enter(direction);
    }
    IEnumerator JumpIntoPortalAnimation(BlackHolePortal portal,Vector2Int direction){
        var start=player.position;var end=Position(portal.Cell,portal.tile.surfaceHeight);
        var facing=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        var pose=PlayerPushPose.Begin(player);
        for(float t=0;t<.36f;){
            t+=MovementDeltaTime*BlackHoleTravel.AnimationSpeed;
            float a=Mathf.Clamp01(t/.36f);var position=Vector3.Lerp(start,end,a);
            player.rotation=Quaternion.RotateTowards(player.rotation,facing,180f/Mathf.Max(.085f,turnSeconds)*MovementDeltaTime);
            position.y+=Mathf.Sin(a*Mathf.PI)*.4f;player.position=position;pose.SamplePortalEntry(a);yield return null;
        }
        player.position=end;pose.SamplePortalEntry(1);NotifyLanding(portal.Cell);Busy=false;

    }
    bool BeginPortalPush(MagnetPiece magnet,BlackHolePortal portal,Vector2Int dir){
        var destination=InspectMagnetDestination(Cell(magnet.transform),magnet.transform.position.y,portal.Cell,magnet);
        if(destination.kind!=MagnetLandingKind.Portal)return Reject("黑洞入口被占据或高度不一致");
        Busy=true;StartMovement(PushIntoPortal(magnet,portal,dir));LastRule="磁铁翻滚一格进入黑洞，保存翻滚后的姿态；再走入黑洞选择出口";return true;
    }
    public IEnumerator SettleMagnetArrival(MagnetPiece magnet,Vector3 previousPosition,bool unscaled=false){
        var cell=Cell(magnet.transform);var portal=BlackHolePortal.At(this,cell);
        // Also covers rings kicked from portal exits into empty space.
        if(!portal&&SpaceRing(magnet)&&!Deck(cell,magnet)){
            if(!MagnetPiece.FlatU(magnet.Pose)){
                var start=magnet.Pose;
                var flat=Quaternion.FromToRotation(start*Vector3.up,Vector3.up)*start;
                for(float t=0;t<.2f;t+=unscaled?BlackHoleTravel.AnimationDeltaTime:MovementDeltaTime){
                    magnet.geometry.rotation=Quaternion.Slerp(start,flat,Mathf.SmoothStep(0,1,t/.2f));
                    MagnetVisuals.Ground(magnet);yield return null;
                }
                magnet.geometry.rotation=flat;
            }
            MagnetVisuals.Ground(magnet);magnet.walkable=true;
            magnet.geometry.position+=Vector3.up*(magnet.transform.position.y-MagnetGeometryTop(magnet));
        }
        if(!portal)yield break; // Ground/deck heights and space hovering use the shared surface rules.
        if(portal.Cargo&&portal.Cargo!=magnet)throw new System.InvalidOperationException("Magnet destination became occupied during movement");
        var scale=magnet.geometry.localScale;
        for(float t=0;t<.28f;t+=unscaled?BlackHoleTravel.AnimationDeltaTime:MovementDeltaTime){
            float a=Mathf.SmoothStep(0,1,t/.28f);
            magnet.geometry.localScale=scale*(1-a);MagnetVisuals.Ground(magnet);
            magnet.geometry.position-=Vector3.up*(.3f*a);yield return null;
        }
        StoreMagnetInPortal(magnet,portal,previousPosition,scale);
    }
    void StoreMagnetInPortal(MagnetPiece magnet,BlackHolePortal portal,Vector3 previousPosition,Vector3 scale){
        magnet.portalEntryPosition=previousPosition;magnet.storedInPortal=portal;
        magnet.gameObject.SetActive(false);magnet.geometry.localScale=scale;MagnetVisuals.Ground(magnet);
    }
    IEnumerator PushIntoPortal(MagnetPiece magnet,BlackHolePortal portal,Vector2Int dir){
        yield return BlackHoleTravel.PlayAnimation("cargo-entry/"+magnet.shape+"/"+magnet.product+"/"+MagnetPiece.VerticalBar(magnet.Pose),PushIntoPortalAnimation(magnet,portal,dir));
        ClearMovementInput();
    }
    IEnumerator PushIntoPortalAnimation(MagnetPiece magnet,BlackHolePortal portal,Vector2Int dir){
        Vector3 start=magnet.transform.position,playerStart=player.position;
        magnet.portalEntryPosition=start;
        var vacated=Cell(magnet.transform);var end=Position(portal.Cell,portal.tile.surfaceHeight);
        var playerEnd=PushPlayerEnd(vacated,magnet);var pose=magnet.Pose;var scale=magnet.geometry.localScale;
        float degrees;var arrival=MagnetPiece.Rolled(magnet.shape,pose,dir,out degrees);
        var axis=new Vector3(dir.y,0,-dir.x);
        if(magnet.product==MagnetProduct.Ring){
            var forward=new Vector3(dir.x,0,dir.y);
            bool tip=MagnetPiece.FlatU(pose)||Mathf.Abs(Vector3.Dot(pose*Vector3.up,forward))>.95f;
            var bounds=AssemblyBounds(AssemblyBodies(magnet.geometry));
            degrees=tip?90:cellSize/Mathf.Max(.1f,bounds.size.y*.5f)*Mathf.Rad2Deg;
            arrival=Quaternion.AngleAxis(degrees,axis)*pose;
        }
        player.rotation=Quaternion.LookRotation(new Vector3(dir.x,0,dir.y));var push=PlayerPushPose.Begin(player);
        for(float t=0;t<.65f;t+=MovementDeltaTime*BlackHoleTravel.AnimationSpeed){
            float a=Mathf.SmoothStep(0,1,t/.65f);magnet.transform.position=Vector3.Lerp(start,end,a);
            magnet.geometry.rotation=Quaternion.AngleAxis(degrees*Mathf.Clamp01(a/.75f),axis)*pose;magnet.geometry.localScale=scale*Mathf.Lerp(1,.05f,Mathf.SmoothStep(0,1,Mathf.Clamp01((a-.75f)/.25f)));
            MagnetVisuals.Ground(magnet);player.position=Vector3.Lerp(playerStart,playerEnd,a);push.Sample(a);yield return null;
        }
        magnet.transform.position=end;magnet.geometry.localScale=scale;magnet.geometry.rotation=arrival;MagnetVisuals.Ground(magnet);
        if(magnet.product==MagnetProduct.Ring)magnet.walkable=MagnetPiece.FlatU(arrival);
        StoreMagnetInPortal(magnet,portal,start,scale);
        push.End();player.position=playerEnd;NotifyLanding(vacated);Busy=false;
    }
    // TryStep already saved the state before JumpIntoPortal. The entire journey
    // belongs to that step; a snapshot on the portal would re-enter selection on undo.
    public void RecordPortalJourney(){ClearMovementInput();}
    public void CompletePortalLanding(Vector2Int cell){ClearMovementInput();NotifyLanding(cell);}
    public void SetPortalBusy(bool value){Busy=value;ClearMovementInput();}
    public void ReleasePortalGeometry(MagnetPiece[] carried){foreach(var piece in carried)if(piece&&piece.geometry)recordedGeometry.Remove(piece.geometry);}
}
}
