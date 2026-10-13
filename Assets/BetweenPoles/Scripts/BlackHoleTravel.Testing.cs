using System.Linq;
using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    BlackHolePortal[] testPortals;
    BlackHolePortal testDestination,previewPortal;
    string testMessage;
    IEnumerator TestTravel(BlackHolePortal source){
        origin=source;Selecting=true;busy=true;var board=source.board;
        bool wasEnabled=board.enabled;board.BeginPortalMagnetPresentation();board.enabled=false;
        var playerScale=board.player.localScale;
        bool undoRequested=false;
        try{
            board.BeginPortalMagnetPresentation();
            entryDirection=source.EntryDirection;
            if(Mathf.Abs(entryDirection.x)+Mathf.Abs(entryDirection.y)!=1){
                var forward=board.player.forward;
                entryDirection=Mathf.Abs(forward.x)>Mathf.Abs(forward.z)?new Vector2Int(forward.x>=0?1:-1,0):new Vector2Int(0,forward.z>=0?1:-1);
            }
            entryFacing=Quaternion.LookRotation(new Vector3(entryDirection.x,0,entryDirection.y));
            board.RecordPortalJourney();
            source.board.SetPortalBusy(true);
        var entryPose=source.board.player.GetComponent<PlayerPushPose>();if(entryPose)entryPose.End();
        source.board.SetPortalBusy(false);
            board.player.localScale=Vector3.zero;
            testPortals=board.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BlackHolePortal>()).Where(p=>p.board==board&&VisitedRoom(p.Room)).ToArray();testDestination=null;previewPortal=null;
            BeginNavigation(board);

            testMessage="左键拖拽查看已探索的小岛，滚轮缩放，点击黑洞直接从那里出来。";
            busy=false;
            while(!testDestination){
                if(Input.GetKeyDown(KeyCode.Z)){undoRequested=true;yield break;}
                UpdateNavigation();yield return null;
            }
            busy=true;var destination=testDestination;
            ExitPlan plan;var direction=entryDirection;
            var cargo=source.Cargo;
            bool valid=PlanExit(destination,direction,cargo,out plan);
            if(!valid)yield break;

            EndNavigation();destination.Disarm();
            board.player.SetPositionAndRotation(new Vector3(destination.tile.transform.position.x,destination.tile.surfaceHeight,destination.tile.transform.position.z),entryFacing);
            board.CompletePortalLanding(destination.Cell);
            yield return WaitForExitView(destination);
            board.player.localScale=playerScale;
            yield return Eject(destination,direction,cargo,plan);
            if(plan.unstableLanding){
                // Return to the original entrance automatically, without reopening
                // navigation. Reverse the original entry direction to reach its shore.
                board.player.localScale=Vector3.zero;
                var returnDirection=-entryDirection;
                var returnStart=new Vector3(source.tile.transform.position.x,source.tile.surfaceHeight,source.tile.transform.position.z);
                source.Disarm();
                board.player.SetPositionAndRotation(returnStart,Quaternion.LookRotation(new Vector3(returnDirection.x,0,returnDirection.y)));
                board.CompletePortalLanding(source.Cell);
                yield return WaitForExitView(source);
                // Eject captures the actor's current scale as its full-size pose.
                // Restore it after the hidden camera transfer, exactly as for a
                // manually selected exit, or the entire return animation is invisible.
                board.player.localScale=playerScale;
                ExitPlan returnPlan;
                if(PlanExit(source,returnDirection,null,out returnPlan)&&!returnPlan.unstableLanding){
                    yield return Eject(source,returnDirection,null,returnPlan);
                }else{
                    // If the former entry side is no longer safe, recover on the
                    // entrance tile itself instead of repeating a failed landing.
                    board.SetPortalBusy(true);
                    try{yield return EjectPlayer(board.player,returnStart,returnStart,playerScale);}
                    finally{board.SetPortalBusy(false);}
                    board.CompletePortalLanding(source.Cell);
                }
            }
        }finally{
            EndNavigation();testPortals=null;testDestination=null;previewPortal=null;Selecting=false;busy=false;
            if(board&&board.player)board.player.localScale=playerScale;
            if(board){board.enabled=wasEnabled;board.EndPortalMagnetPresentation();}
            if(undoRequested&&board)board.UndoStep();
        }
    }
    bool SelectTestPortal(BlackHolePortal portal){
        ExitPlan plan;
        bool valid=portal&&PlanExit(portal,entryDirection,origin.Cargo,out plan);
        if(!valid){testMessage="出口无法安全落脚或组合：检查出口、竖直磁铁倒下的一格，以及磁铁弹回入口的位置。";return false;}
        testDestination=portal;return true;
    }
}
}
