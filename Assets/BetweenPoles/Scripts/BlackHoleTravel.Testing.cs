using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    BlackHolePortal[] testPortals;
    BlackHolePortal testDestination,previewPortal;
    bool testCancel;
    string testMessage;
    IEnumerator TestTravel(BlackHolePortal source){
        origin=source;Selecting=true;busy=true;var board=source.board;
        bool wasEnabled=board.enabled;board.enabled=false;
        var playerScale=board.player.localScale;
        try{
            entryDirection=source.EntryDirection;
            if(Mathf.Abs(entryDirection.x)+Mathf.Abs(entryDirection.y)!=1){
                var forward=board.player.forward;
                entryDirection=Mathf.Abs(forward.x)>Mathf.Abs(forward.z)?new Vector2Int(forward.x>=0?1:-1,0):new Vector2Int(0,forward.z>=0?1:-1);
            }
            entryFacing=Quaternion.LookRotation(new Vector3(entryDirection.x,0,entryDirection.y));
            board.RecordPortalJourney();
            yield return effect.Absorb(BlackHoleScreenEffect.ScreenCenter(source),2.8f,source);
            board.player.localScale=Vector3.zero;
            testPortals=MagnetTestLayouts.Portals(board);testDestination=null;previewPortal=null;testCancel=false;
            BeginNavigation(board);
            yield return effect.FadeToNavigation();
            testMessage=testPortals.Length<2?"至少需要两个黑洞；按 Esc 返回入口。":"移动镜头寻找黑洞，点击出口标记，再确认传送。";
            busy=false;
            while(!testDestination){
                if(Input.GetKeyDown(KeyCode.Escape))SelectTestPortal(source,true);
                UpdateNavigation();yield return null;
            }
            busy=true;var destination=testDestination;
            ExitPlan plan;var direction=testCancel?-entryDirection:entryDirection;
            var cargo=testCancel?null:source.Cargo;
            // Cancellation leaves stored material in the original portal.
            bool valid=testCancel?CancelPlan(source,direction,out plan):PlanExit(destination,direction,cargo,out plan);
            if(!valid)yield break;
            yield return effect.Absorb(new Vector2(.5f,.5f),.95f);
            EndNavigation();destination.Disarm();
            board.player.SetPositionAndRotation(new Vector3(destination.tile.transform.position.x,destination.tile.surfaceHeight,destination.tile.transform.position.z),entryFacing);
            board.CompletePortalLanding(destination.Cell);
            yield return null;yield return effect.Emerge(destination);
            board.player.localScale=playerScale;
            yield return Eject(destination,direction,cargo,plan);
        }finally{
            EndNavigation();testPortals=null;testDestination=null;previewPortal=null;Selecting=false;busy=false;
            if(board&&board.player)board.player.localScale=playerScale;
            if(effect)effect.Hide();if(board)board.enabled=wasEnabled;
        }
    }
    bool CancelPlan(BlackHolePortal source,Vector2Int direction,out ExitPlan plan){
        plan=new ExitPlan();GridTile tile;Vector3 landing;
        if(!BlackHolePortal.TrySurface(source.board,source.Cell+direction,out tile,out landing))return false;
        plan.player=landing;return true;
    }
    bool SelectTestPortal(BlackHolePortal portal,bool cancel){
        ExitPlan plan;
        bool valid=portal&&(cancel?CancelPlan(portal,-entryDirection,out plan):portal!=origin&&PlanExit(portal,entryDirection,origin.Cargo,out plan));
        if(!valid){testMessage="出口空间不足：检查黑洞、出洞格及竖直磁铁向前倒下的一格。";return false;}
        testDestination=portal;testCancel=cancel;return true;
    }
}
}
