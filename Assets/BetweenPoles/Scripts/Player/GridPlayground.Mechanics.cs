using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    public string LastRule {get;private set;}
    public Vector2Int PlayerCell {get{return Cell(player);}}
    public GridTile TileAt(Vector2Int p){return Tile(p);}
    public MagnetPiece MagnetAt(Vector2Int p){return Piece(p);}
    bool Reject(string why){LastRule=why;return false;}
    void ShowUnsafeDrop(Vector2Int direction){
        PlayerPushPose.BeginEdgeBalance(player,direction);
        // Fresh presses may retry; holding must not loop the stumble indefinitely.
        nextHeldStep=float.PositiveInfinity;
    }
    float GroundHeight(Vector2Int p){var t=Tile(p);return t?t.surfaceHeight:0;}
    // Only absorb floating-point drift; this is not an automatic step-up allowance.
    bool SameHeight(float a,float b){return Mathf.Abs(a-b)<.01f;}
    bool CanReachMagnet(MagnetPiece magnet){
        if(SameHeight(player.position.y,magnet.transform.position.y))return true;
        var from=Cell(player);var cell=Cell(magnet.transform);float contact;
        return TravelSurface(from,player.position.y,cell,magnet,out contact)
            &&SameHeight(contact,magnet.transform.position.y);
    }
    Vector2Int PushLanding(Vector2Int vacated,Vector2Int from){
        var moving=Piece(vacated);var deck=Deck(vacated,moving);float height;
        return (Floor(vacated)||deck)&&TravelSurface(from,player.position.y,vacated,moving,out height)?vacated:from;
    }
    Quaternion Yaw(Vector3 axis){axis.y=0;if(axis.sqrMagnitude<.01f)axis=Vector3.right;return Quaternion.FromToRotation(Vector3.right,axis.normalized);}
    Vector2Int Direction(Vector3 v){return Mathf.Abs(v.x)>Mathf.Abs(v.z)?new Vector2Int(v.x>=0?1:-1,0):new Vector2Int(0,v.z>=0?1:-1);}
    static Quaternion ReceiverLandingPose(MagnetPiece receiver,Vector2Int push) {
        if(receiver.shape!=MagnetShape.Bar||!MagnetPiece.VerticalBar(receiver.Pose))return receiver.Pose;
        // Tip the upright receiver away from the player, in the push direction.
        var axis=new Vector3(push.y,0,-push.x);
        return Quaternion.AngleAxis(90,axis)*receiver.Pose;
    }
    public static MagnetProduct Recipe(MagnetPiece incoming,Quaternion arrival,MagnetPiece target,bool receiverSupported,Vector2Int push) {
        // Docking to a U socket preserves the material's pre-push posture.
        // The remaining bridge socket accepts either pole; a thrown flat bar
        // aligns to the missing rail regardless of its horizontal approach axis.
        if(target.product==MagnetProduct.BridgeHalf)return incoming.shape==MagnetShape.Bar&&Mathf.Abs((incoming.Pose*Vector3.right).y)<.05f?MagnetProduct.Bridge:MagnetProduct.None;
        if(target.combined||incoming.north==target.north)return MagnetProduct.None;
        if(incoming.shape==target.shape){
            if(incoming.shape==MagnetShape.Horseshoe)return MagnetPiece.FlatU(incoming.Pose)&&MagnetPiece.FlatU(target.Pose)?MagnetProduct.Ring:MagnetProduct.None;
            // Decide from both pre-push postures, before any animation rotates them.
            bool receiverStanding=MagnetPiece.VerticalBar(target.Pose);
            bool incomingStanding=MagnetPiece.VerticalBar(incoming.Pose);
            // An upright incoming bar falls along the push beside a parallel
            // flat receiver, on land as well as over a gap.
            if(incomingStanding&&!receiverStanding
                &&Mathf.Abs(Vector3.Dot(new Vector3(push.x,0,push.y),target.Pose*Vector3.right))>.95f)
                return MagnetProduct.WideBar;
            // An axial flat rail docks beside the falling upright receiver,
            // both on supported ground and over a gap.
            if(receiverStanding&&!incomingStanding
                &&Mathf.Abs(Vector3.Dot(new Vector3(push.x,0,push.y),incoming.Pose*Vector3.right))>.95f)
                return MagnetProduct.WideBar;
            // Perpendicular flat bars can stack only when the receiving cell is a gap.
            if(!receiverStanding&&!incomingStanding&&!receiverSupported
                &&Mathf.Abs(Vector3.Dot(incoming.Pose*Vector3.right,target.Pose*Vector3.right))<.05f)
                return MagnetProduct.Cross;
            return receiverStanding==incomingStanding?MagnetProduct.WideBar:MagnetProduct.Cross;
        }
        // Mixed materials slide into the receiver; free-cell rolling must not
        // turn a flat bar into a lift or tip a flat U out of a valid recipe.
        Quaternion u=incoming.shape==MagnetShape.Horseshoe?incoming.Pose:target.Pose;
        Quaternion bar=incoming.shape==MagnetShape.Bar?incoming.Pose:target.Pose;
        if(!MagnetPiece.FlatU(u))return MagnetProduct.None;
        if(MagnetPiece.VerticalBar(bar))return MagnetProduct.Lift;
        return Mathf.Abs((bar*Vector3.right).y)<.05f?MagnetProduct.BridgeHalf:MagnetProduct.None;
    }
    public bool TryStep(Vector2Int dir) {
        var idlePose=player.GetComponent<PlayerPushPose>();if(idlePose)idlePose.CancelIdle();
        if(OpeningCinematic)return false;
        if(CanInterruptPush(dir))return InterruptPush(dir);
        if(TryReverseWalk(dir))return true;
        if(Busy||Mathf.Abs(dir.x)+Mathf.Abs(dir.y)!=1)return false;
        var edgePose=player.GetComponent<PlayerPushPose>();if(edgePose){edgePose.InterruptEdgeBalance(dir);edgePose.InterruptStonePush(dir);}
        var before=SaveWorld();
        bool accepted=TryStepCore(dir);
        if(accepted){RecordHistory(before);if(interruptiblePush)pushUndo=before;}
        return accepted;
    }
    bool TryStepCore(Vector2Int dir) {
        if(Busy||Mathf.Abs(dir.x)+Mathf.Abs(dir.y)!=1)return false;
        var from=Cell(player);var support=Deck(from);
        var next=from+dir;var tile=Tile(next);var m=Piece(next);
        var destinationDeck=Deck(next);
        if(tile&&destinationDeck&&destinationDeck.product==MagnetProduct.Bridge)
            return Reject("陆地上的两格桥是障碍，只能走上边缘外的桥段");
        if(IsWreckCell(next))return Reject("飞船残骸挡住了这个格子");
        if(m&&MagnetStillAnimating(m))return Reject("磁铁正在完成上一次推动，请稍候再接触");
        if(!BridgePassage(from,next))return Reject("宽条桥必须沿长边两端接通，只能从两端通过");
        RefreshPlayerSupport();
        bool deviceEntry=m&&(m.product==MagnetProduct.Lift||(m.product==MagnetProduct.Cross&&RotorActive(m)))
            &&(SameHeight(player.position.y,m.transform.position.y)
                ||SameHeight(player.position.y,m.transform.position.y+LayerHeight*(m.product==MagnetProduct.Lift?1:2)));
        if(standingSurface.valid&&standingSurface.canJump&&!deviceEntry&&!(m&&m.CanBePushed&&CanReachMagnet(m))){
            // Single flat pieces remain pushable at ground level, but their
            // solid tops can receive a jump from a higher surface.
            bool flatSingle=m&&!m.combined&&m.product==MagnetProduct.None
                &&(m.shape==MagnetShape.Bar?!MagnetPiece.VerticalBar(m.Pose):MagnetPiece.FlatU(m.Pose));
            GridTile destinationTile;Vector3 destinationTop;
            if(!BlackHolePortal.TrySurface(this,next,out destinationTile,out destinationTop)){
                if(!tile&&!m)ShowUnsafeDrop(dir);
                return Reject("没有可行走的支撑面");
            }
            if(EvaluateHeight(standingSurface.point.y,destinationTop.y,standingSurface.canJump,LayerHeight)==HeightMove.Blocked){
                if(destinationTop.y<standingSurface.point.y)ShowUnsafeDrop(dir);
                return Reject(destinationTop.y>standingSurface.point.y?"上跳高度不能超过一层":"下落高度必须小于一层");
            }
            var topPortal=BlackHolePortal.At(this,next);
            if(topPortal){Busy=true;StartMovement(JumpIntoPortal(topPortal,dir));LastRule="从物体顶面跳入黑洞";return true;}
            if(flatSingle&&!destinationTile){
                Busy=true;StartMovement(JumpOntoUnstableMagnet(m,destinationTop));
                LastRule="悬空单磁铁承重不稳，跳回原支撑并攀爬脱险";return true;
            }
            Busy=true;StartMovement(WalkOffObject(next,destinationTop));LastRule="在相邻顶面之间跳跃移动";return true;
        }
        if(tile&&tile.blocked){PlayerPushPose.BeginStonePush(player,tile,dir,DirectionPressed(dir));return Reject("石头推不动，用力推动后摊手");}
        if(m&&m.product==MagnetProduct.Ring&&CanReachMagnet(m))
            return BeginPushRing(m,dir,next);
        if(m&&m.product==MagnetProduct.Cross){
            if(RotorActive(m))return BeginLift(m,2,dir);
            if(!SameHeight(player.position.y,m.transform.position.y))return Reject("高度不一致");
            Busy=true;StartMovement(TurnCross(m));LastRule="推动十字：原地顺时针旋转";return true;
        }
        if(m&&m.product==MagnetProduct.Lift)return BeginLift(m,1,dir);
        if(m&&!m.walkable) {
            if(!m.CanBePushed)return Reject("只有单个磁铁可以推动；合成后固定在原格");
            if(!CanReachMagnet(m))return Reject("需要站在同一层推动");
            // A single already beyond the shore is out of the player's reach.
            // It can still receive another material pushed from a supported cell.
            if(!Supported(m))return Reject("边缘外的磁铁无法直接推动，请从岸上推来磁铁与它组合，或用同极磁铁将它排斥回陆地");
            var target=next+dir;var far=TransportObstacle(target,m);var land=Tile(target);float targetHeight;
            var portal=BlackHolePortal.At(this,target);if(portal)return BeginPortalPush(m,portal,dir);
            if(far&&MagnetStillAnimating(far))return Reject("目标磁铁正在完成上一次推动");
            if(land&&land.blocked)return Reject("目标格被石头占据");
            if(!BridgePassage(next,target,m))return Reject("宽条桥必须沿长边两端接通，只能从两端通过");
            if(!TravelSurface(next,m.transform.position.y,target,m,out targetHeight))return Reject("不能将磁铁推上高台");
            float degrees;var arrival=MagnetPiece.Rolled(m.shape,m.Pose,dir,out degrees);
            if(far){
                if(!SameHeight(far.transform.position.y,targetHeight))return Reject("磁铁高度不一致");
                // A bridge's second socket belongs to its U base, not the first rail.
                if(far.product!=MagnetProduct.BridgeHalf&&far.product!=MagnetProduct.None)return Reject("合成体不能被单块磁铁继续合成或排斥；圆环需由主角直接推动");
                if(far.product!=MagnetProduct.BridgeHalf&&far.north==m.north){
                    // A hovering receiver may return to a free, level shore. It
                    // still blocks pushes farther into open space or into a stone.
                    var edgeChain=CanRepelBackToLand(far,dir)?null:EdgeRepelChain(m,far,dir);
                    if(edgeChain!=null){
                        Busy=true;StartMovement(EdgeRepelFeedback(edgeChain,dir));
                        return Reject("边缘同极斥力回弹：无法继续向外推动");
                    }
                    var beyond=target+dir;var beyondTile=Tile(beyond);
                    float beyondHeight;
                    if(TravelSurface(target,far.transform.position.y,beyond,far,out beyondHeight)&&!TransportObstacle(beyond,far)){
                        var farPath=MagnetPiece.RollPath(far.shape,far.Pose,dir);
                        var nearPath=MagnetPiece.RollPath(m.shape,m.Pose,dir);
                        Vector2Int farEnd,nearEnd;
                        if(!ValidateRollPath(far,farPath,from,next,null,null,out farEnd)
                            ||!ValidateRollPath(m,nearPath,from,next,far,farEnd,out nearEnd))return false;
                        Busy=true;StartMovement(Repel(m,far,nearPath,farPath,PushLanding(next,from)));LastRule="同极排斥：各翻滚一格，空格中保持悬停";return true;
                    }
                    if(beyondTile&&beyondTile.blocked){
                        Busy=true;StartCoroutine(Recoil(m,dir,from));LastRule="远端受石头阻挡：磁铁弹回撞击主角，一起反冲";return true;
                    }
                    return Reject("远端没有落脚位，无法推动；反冲需要远端石头阻挡");
                }
                bool receiverSupported=Supported(far);
                var result=Recipe(m,arrival,far,receiverSupported,dir);
                if(result==MagnetProduct.None)return Reject("磁铁姿态不符合配方，保留原状态");
                var bridgeDir=far.bridgeDirection;
                if(result==MagnetProduct.BridgeHalf){
                    // The receiver fixes the anchor cell. The U's pre-push
                    // opening fixes the direction, regardless of which piece moves.
                    var u=m.shape==MagnetShape.Horseshoe?m:far;
                    bridgeDir=Direction(u.Pose*Vector3.forward);
                }
                var playerLanding=PushLanding(next,from);
                if(result==MagnetProduct.BridgeHalf||result==MagnetProduct.Bridge){
                    var second=Cell(far.transform)+bridgeDir;
                    var occupant=TransportObstacle(second,far);
                    var ground=Tile(second);
                    // The incoming material is consumed by this assembly, so its
                    // cell may become the second bridge cell. Keep the player at
                    // the push origin when that cell is not vacated after docking.
                    if((ground&&(ground.blocked||!SameHeight(ground.surfaceHeight,far.transform.position.y)))||(occupant&&occupant!=m)||second==from)return Reject("两格桥可位于普通同高地面，但不能覆盖石头、高差、其他材料或主角");
                    if(second==next)playerLanding=from;
                }
                Busy=true;StartMovement(Assemble(m,far,dir,arrival,result,playerLanding,bridgeDir));LastRule="合成："+ProductName(result);return true;
            }
            var path=MagnetPiece.RollPath(m.shape,m.Pose,dir);Vector2Int end;
            if(!ValidateRollPath(m,path,from,next,null,null,out end))return false;
            Busy=true;StartMovement(RollThenWalk(m,dir,target,PushLanding(next,from),path));LastRule=Deck(target,m)?"沿桥面推动磁铁":!land?"磁铁推出边缘一格悬停，等待组合或同极排斥回岸":m.shape==MagnetShape.Horseshoe?"U 型翻面一格，保持平躺":"翻滚推动一格";return true;
        }
        if(!Floor(next)&&!(m&&m.walkable)){
            if(Floor(from)&&!tile&&!m){
                ShowUnsafeDrop(dir);
            }
            return Reject("没有可行走的支撑面");
        }
        // A combined bar can be entered from base level only over a gap.
        // On an island it is a fixed raised obstacle; equal-height top access still works.
        if(m&&m.product==MagnetProduct.WideBar&&tile&&player.position.y<Height(next)-.01f)
            return Reject("岛上的合成条有高度，不能从地面直接踩上，也不能推动");
        if(EvaluateHeight(player.position.y,Height(next),false,LayerHeight)!=HeightMove.Walk)return Reject("地面不能直接起跳登高，高差需要磁流升降装置");
        var entryPortal=BlackHolePortal.At(this,next);
        if(entryPortal){Busy=true;StartMovement(JumpIntoPortal(entryPortal,dir));LastRule="跳入黑洞";return true;}
        Busy=true;StartMovement(MovePlayer(next));LastRule="行走";return true;
    }
    IEnumerator JumpOntoUnstableMagnet(MagnetPiece magnet,Vector3 landing){
        var start=player.position;var source=Cell(player);
        var outward=landing-start;outward.y=0;outward.Normalize();
        var geometry=magnet.geometry;var originalPosition=geometry.localPosition;var originalRotation=geometry.localRotation;
        var lip=start+outward*(cellSize*.4f);
        var hanging=lip+outward*.2f-Vector3.up*.82f;
        PlayerPushPose pose=null;
        try{
            yield return TurnPlayer(outward);pose=PlayerPushPose.Begin(player);
            for(float t=0;t<.9f;t+=MovementDeltaTime){
                float p=Mathf.Clamp01(t/.9f),a=Mathf.InverseLerp(.18f,.83f,p);
                player.position=Vector3.Lerp(start,landing,a)+Vector3.up*(.55f*Mathf.Sin(a*Mathf.PI));
                pose.SamplePlatformJump(p);yield return null;
            }
            player.position=landing;
            for(float t=0;t<1.2f;t+=MovementDeltaTime){
                float a=t/1.2f,wave=Mathf.Sin(a*Mathf.PI*6)*Mathf.Sin(a*Mathf.PI);
                geometry.localRotation=originalRotation*Quaternion.AngleAxis(12*wave,geometry.InverseTransformDirection(outward));
                geometry.localPosition=originalPosition-Vector3.up*(.09f*Mathf.Sin(a*Mathf.PI));
                player.position=landing-Vector3.up*(.09f*Mathf.Sin(a*Mathf.PI));
                pose.SamplePortalLedge(0,a);yield return null;
            }
            geometry.localPosition=originalPosition;geometry.localRotation=originalRotation;
            var facing=player.rotation;
            for(float t=0;t<.85f;t+=MovementDeltaTime){
                float a=Mathf.Clamp01(t/.85f);
                player.rotation=Quaternion.Slerp(facing,Quaternion.LookRotation(-outward),Mathf.Clamp01(a*3));
                player.position=Vector3.Lerp(landing,hanging,a)+Vector3.up*(Mathf.Max(.7f,start.y-landing.y+.35f)*Mathf.Sin(a*Mathf.PI));
                pose.SamplePortalLedge(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,.85f,a)),0);yield return null;
            }
            player.position=hanging;player.rotation=Quaternion.LookRotation(-outward);
            for(float t=0;t<.45f;t+=MovementDeltaTime){pose.SamplePortalLedge(1,0);yield return null;}
            var lifted=lip+outward*.2f+Vector3.up*.12f;
            for(float t=0;t<1f;t+=MovementDeltaTime){
                float a=Mathf.SmoothStep(0,1,t);
                player.position=a<.65f?Vector3.Lerp(hanging,lifted,a/.65f):Vector3.Lerp(lifted,start,(a-.65f)/.35f);
                pose.SamplePortalLedge(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,a)),0);yield return null;
            }
            player.position=start;NotifyLanding(source);
        }finally{
            if(geometry){geometry.localPosition=originalPosition;geometry.localRotation=originalRotation;}
            if(pose)pose.End();Busy=false;
            ClearMovementInput();nextHeldStep=float.PositiveInfinity;
        }
    }
    IEnumerator WalkOffObject(Vector2Int cell,Vector3 end){
        var start=player.position;var direction=new Vector3(cell.x*cellSize-start.x,0,cell.y*cellSize-start.z);
        yield return TurnPlayer(direction);
        var edge=end;edge.y=Mathf.Max(start.y,end.y);
        var sourceTile=Tile(Cell(player));var destinationTile=Tile(cell);
        if(!SameHeight(end.y,start.y)||(sourceTile&&sourceTile.blocked)||(destinationTile&&destinationTile.blocked)){
            var jump=PlayerPushPose.Begin(player);
            try{
                for(float t=0;t<.95f;t+=MovementDeltaTime){
                    float p=Mathf.Clamp01(t/.95f),a=Mathf.InverseLerp(.18f,.83f,p);
                    var point=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,a));
                    point.y=Mathf.Lerp(start.y,end.y,a)+Mathf.Sin(a*Mathf.PI)*.55f;
                    player.position=point;jump.SamplePlatformJump(p);yield return null;
                }
            }finally{jump.End();}
            player.position=end;NotifyLanding(cell);Busy=false;yield break;
        }
        var walkPose=PlayerPushPose.BeginWalk(player,cellSize);
        try{yield return Slide(player,edge,stepSeconds);}finally{walkPose.End();}
        if(!SameHeight(edge.y,end.y))yield return Slide(player,end,Mathf.Clamp(Mathf.Sqrt(Mathf.Abs(edge.y-end.y))*.16f,.12f,.5f));
        player.position=end;NotifyLanding(cell);Busy=false;
    }
    public static string ProductName(MagnetProduct p){switch(p){case MagnetProduct.WideBar:return "一格宽条";case MagnetProduct.Ring:return "闭合圆环";case MagnetProduct.Cross:return "叠放十字";case MagnetProduct.Lift:return "单层磁流升降台";case MagnetProduct.BridgeHalf:return "两格桥半成品";case MagnetProduct.Bridge:return "两格桥";default:return "基础磁铁";}}
    bool ValidateRollPath(MagnetPiece m,List<MagnetRollStep> path,Vector2Int playerFrom,Vector2Int playerLanding,MagnetPiece vacated,Vector2Int? reserved,out Vector2Int end){
        end=Cell(m.transform);float height=m.transform.position.y;
        foreach(var step in path){
            var source=end;end+=step.direction;
            var occupant=TransportObstacle(end,m);float landing;
            if(!TravelSurface(source,height,end,m,out landing))return Reject("磁铁不能进入石头或不同高度的地面格");
            if((occupant&&occupant!=vacated)||(reserved.HasValue&&end==reserved.Value)||end==playerFrom||end==playerLanding)
                return Reject("滚动路径被磁铁或主角占据，整次推动保持原状");
            height=landing;
        }
        return m.shape!=MagnetShape.Horseshoe||MagnetPiece.FlatU(path[path.Count-1].pose)||Reject("单个 U 型翻面后必须平躺");
    }
    IEnumerator RollPiece(MagnetPiece m,MagnetRollStep step,Vector2Int to,System.Action<float> follow=null,float seconds=0){
        Vector3 start=m.transform.position,end=Position(to,TransportHeight(m,to));Quaternion pose=m.Pose;var axis=new Vector3(step.direction.y,0,-step.direction.x);
        float duration=seconds>0?seconds:m.shape==MagnetShape.Horseshoe?uRollSeconds:stepSeconds;
        for(float t=0;t<duration;t+=MovementDeltaTime){float a=Mathf.SmoothStep(0,1,t/duration);m.transform.position=Vector3.Lerp(start,end,a);m.geometry.rotation=Quaternion.AngleAxis(step.degrees*a,axis)*pose;MagnetVisuals.Ground(m);follow?.Invoke(a);yield return null;}
        m.transform.position=end;m.geometry.rotation=step.pose;MagnetVisuals.Ground(m);follow?.Invoke(1);
        var settle=SettleMagnetArrival(m,start);
        while(settle.MoveNext())yield return null;
    }
    IEnumerator RollAlongPath(MagnetPiece m,List<MagnetRollStep> path){
        var cell=Cell(m.transform);
        foreach(var step in path){cell+=step.direction;yield return RollPiece(m,step,cell);if(m.storedInPortal)yield break;}
    }
    IEnumerator PushSingleBar(MagnetPiece m,Vector2Int dir,Vector2Int target,Vector2Int next){
        Vector3 start=m.transform.position,end=Position(target,TransportHeight(m,target));
        Vector3 playerStart=player.position,playerEnd=PushPlayerEnd(next,m);
        Vector3 forward=new Vector3(dir.x,0,dir.y),axis=new Vector3(dir.y,0,-dir.x);
        Quaternion facing=Quaternion.LookRotation(forward),pose=m.Pose;
        float degrees;Quaternion final=MagnetPiece.Rolled(m.shape,pose,dir,out degrees);
        // Share the movement curve so the hands and magnet advance together.
        float duration=Mathf.Max(.01f,barPushSeconds);
        var pushPose=PlayerPushPose.Begin(player);
        for(float elapsed=0;elapsed<duration;elapsed+=MovementDeltaTime){
            float p=Mathf.Clamp01((elapsed+MovementDeltaTime)/duration);
            float drive=MovementProgress(p);
            pushPose.Sample(p);
            player.rotation=facing;
            player.position=Vector3.Lerp(playerStart,playerEnd,drive);
            m.transform.position=Vector3.Lerp(start,end,drive);
            m.geometry.rotation=Quaternion.AngleAxis(degrees*drive,axis)*pose;
            MagnetVisuals.Ground(m);
            yield return null;
        }
        m.transform.position=end;m.geometry.rotation=final;MagnetVisuals.Ground(m);
        yield return SettleMagnetArrival(m,start);
        pushPose.End();player.SetPositionAndRotation(playerEnd,facing);NotifyLanding(next);
    }
    IEnumerator RollThenWalk(MagnetPiece m,Vector2Int dir,Vector2Int target,Vector2Int next,List<MagnetRollStep> path){
        BeginPushInterrupt(dir);
        pushHoverParticipants.Add(m);
        if(path.Count==1)yield return FlowingRoll(m,path[0],target,next);
        else if(m.shape==MagnetShape.Bar)yield return PushSingleBar(m,dir,target,next);
        else {
            Vector3 start=player.position,end=PushPlayerEnd(next,m);
            player.rotation=Quaternion.LookRotation(new Vector3(dir.x,0,dir.y));
            var pushPose=PlayerPushPose.Begin(player);var cell=Cell(m.transform);
            for(int i=0;i<path.Count;i++){
                cell+=path[i].direction;
                System.Action<float> follow=i==0?(System.Action<float>)(progress=>{player.position=Vector3.Lerp(start,end,progress);pushPose.Sample(progress);}):null;
                yield return RollPiece(m,path[i],cell,follow);
                if(i==0)pushPose.End();
            }
            pushPose.End();player.position=end;NotifyLanding(next);
        }
        bool flowed=flowingPush;
        EndPushInterrupt();Busy=false;
        continuePushOnComplete=flowed&&!recordingPush&&!finishingMovement;
    }
    IEnumerator FlowingRoll(MagnetPiece m,MagnetRollStep step,Vector2Int target,Vector2Int next){
        flowingPush=true;pushFlowProgress=0;
        float normalSpeed=1/Mathf.Max(.01f,m.shape==MagnetShape.Bar?barPushSeconds:uRollSeconds);
        if(pushFlowSpeed<=0)pushFlowSpeed=normalSpeed;
        var start=m.transform.position;var end=Position(target,TransportHeight(m,target));
        var actorStart=player.position;var actorEnd=PushPlayerEnd(next,m);
        var rotation=m.Pose;var axis=new Vector3(step.direction.y,0,-step.direction.x);
        player.rotation=Quaternion.LookRotation(new Vector3(step.direction.x,0,step.direction.y));
        var style=m.shape!=MagnetShape.Bar?PlayerPushPose.FlowPushStyle.Standard:
            MagnetPiece.VerticalBar(rotation)?PlayerPushPose.FlowPushStyle.ShoveBar:PlayerPushPose.FlowPushStyle.LiftBar;
        var pose=PlayerPushPose.BeginFlowPush(player,cellSize,style);
        while(pushFlowProgress<1){
            float dt=pushFlowCarryTime>=0&&!recordingPush&&!finishingMovement?pushFlowCarryTime:MovementDeltaTime;
            pushFlowCarryTime=-1;
            bool continuing=(bufferedSteps.Count>0&&bufferedSteps.Peek().direction==pushDirection
                    &&Time.unscaledTime-bufferedSteps.Peek().time<=InputBufferSeconds)
                ||(heldDirection==pushDirection&&DirectionHeld(heldDirection));
            if(Time.unscaledTime-pushFlowTapTime>.24f)pushFlowBoost=0;
            float speedLimit=normalSpeed*Mathf.Clamp(maxPushSpeedMultiplier,1f,1.5f);
            float desired=Mathf.Clamp(Mathf.Max(normalSpeed,pushFlowBoost),normalSpeed,speedLimit);
            if(recordingPush)desired=normalSpeed;
            // Only the last step settles. Intermediate cell centres are not brakes.
            if(!continuing&&!recordingPush)desired*=Mathf.Sqrt(Mathf.Clamp((1-pushFlowProgress)/.2f,.04f,1));
            pushFlowSpeed=Mathf.Min(speedLimit,Mathf.MoveTowards(pushFlowSpeed,desired,normalSpeed*3*dt/.07f));
            float remainingTime=(1-pushFlowProgress)/Mathf.Max(.001f,pushFlowSpeed);
            pushFlowProgress=Mathf.Min(1,pushFlowProgress+pushFlowSpeed*dt);
            var previous=player.position;
            player.position=Vector3.Lerp(actorStart,actorEnd,pushFlowProgress);
            m.transform.position=Vector3.Lerp(start,end,pushFlowProgress);
            m.geometry.rotation=Quaternion.AngleAxis(step.degrees*pushFlowProgress,axis)*rotation;
            MagnetVisuals.Ground(m);
            pose.AdvanceFlowPush(Vector3.Distance(previous,player.position),dt,pushFlowProgress);
            if(pushFlowProgress>=1){pushFlowCarryTime=Mathf.Max(0,dt-remainingTime);break;}
            yield return null;
        }
        m.transform.position=end;m.geometry.rotation=step.pose;MagnetVisuals.Ground(m);
        yield return SettleMagnetArrival(m,start);
        player.position=actorEnd;NotifyLanding(next);
        // The movement driver may start the next validated push in this frame.
        // PlayerPushPose releases the stance only when the chain actually stops.
    }
    // Flatten the existing roll animation so both paths share one movement clock.
    // This also lets interrupted pushes record and replay their remaining poses normally.
    IEnumerator RepelledPath(MagnetPiece piece,List<MagnetRollStep> path,float seconds,System.Action<float> follow=null){
        var cell=Cell(piece.transform);
        for(int i=0;i<path.Count;i++){
            cell+=path[i].direction;
            var roll=RollPiece(piece,path[i],cell,i==0?follow:null,seconds);
            while(roll.MoveNext())yield return null;
            if(piece.storedInPortal)yield break;
        }
    }
    IEnumerator Repel(MagnetPiece near,MagnetPiece far,List<MagnetRollStep> nearPath,List<MagnetRollStep> farPath,Vector2Int next){
        var direction=nearPath[0].direction;
        BeginPushInterrupt(direction);
        pushHoverParticipants.Add(near);pushHoverParticipants.Add(far);
        Vector3 playerStart=player.position,playerEnd=PushPlayerEnd(next,near);
        player.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        var pushPose=PlayerPushPose.Begin(player);
        // At a quarter of the near roll's time it has advanced about 16% of a cell.
        // The far magnet then accelerates away, with both arriving together.
        float seconds=Mathf.Max(.12f,near.shape==MagnetShape.Horseshoe||far.shape==MagnetShape.Horseshoe?uRollSeconds:barPushSeconds);
        float delay=seconds*.25f;
        System.Action<float> follow=progress=>{player.position=Vector3.Lerp(playerStart,playerEnd,progress);pushPose.Sample(progress);};
        var nearMotion=RepelledPath(near,nearPath,seconds,follow);
        var farMotion=RepelledPath(far,farPath,seconds-delay);
        bool nearMoving=true,farMoving=true;
        for(float elapsed=0;nearMoving||farMoving;elapsed+=MovementDeltaTime){
            if(nearMoving)nearMoving=nearMotion.MoveNext();
            if(farMoving&&elapsed>=delay)farMoving=farMotion.MoveNext();
            if(nearMoving||farMoving)yield return null;
        }
        pushPose.End();player.position=playerEnd;NotifyLanding(next);
        EndPushInterrupt();Busy=false;
    }
    List<MagnetPiece> EdgeRepelChain(MagnetPiece near,MagnetPiece far,Vector2Int direction){
        var chain=new List<MagnetPiece>{near};
        var current=far;
        while(current&&chain.Count<=magnets.Length){
            if(chain.Contains(current)||current.combined||current.product!=MagnetProduct.None
                ||current.north!=near.north||!SameHeight(current.transform.position.y,near.transform.position.y)
                ||MagnetStillAnimating(current))return null;
            chain.Add(current);
            if(!Supported(current))return chain;
            var cell=Cell(current.transform);var next=cell+direction;float height;
            if(!TravelSurface(cell,current.transform.position.y,next,current,out height)
                ||!SameHeight(height,near.transform.position.y))return null;
            current=TransportObstacle(next,current);
        }
        return null;
    }
    bool CanRepelBackToLand(MagnetPiece far,Vector2Int direction){
        if(Supported(far))return false;
        var from=Cell(far.transform);var to=from+direction;var tile=Tile(to);float height;
        return tile&&!tile.blocked&&SameHeight(tile.surfaceHeight,far.transform.position.y)
            &&!TransportObstacle(to,far)&&to!=PlayerCell
            &&TravelSurface(from,far.transform.position.y,to,far,out height)
            &&SameHeight(height,far.transform.position.y);
    }
    IEnumerator EdgeRepelFeedback(List<MagnetPiece> chain,Vector2Int direction){
        // A rejected push has no undo entry, but its visual remainder can still
        // be replayed independently when the player walks away.
        BeginPushInterrupt(direction,true);
        pushHoverParticipants.UnionWith(chain);
        // Only the geometry moves: occupancy, posture, history and player cell stay put.
        var forward=new Vector3(direction.x,0,direction.y);
        var geometry=chain.Select(piece=>piece.geometry).ToArray();
        var starts=geometry.Select(part=>part.localPosition).ToArray();
        var offsets=new Vector3[chain.Count];
        for(int i=0;i<chain.Count;i++)offsets[i]=geometry[i].parent.InverseTransformVector(
            forward*cellSize*Mathf.Lerp(.18f,.09f,(float)i/(chain.Count-1)));
        PlayerPushPose pose=null;
        try{
            yield return TurnPlayer(forward);
            pose=PlayerPushPose.Begin(player);
            const float delay=.10f,approachSeconds=.22f,returnSeconds=.23f;
            float returnStart=(chain.Count-1)*delay+approachSeconds+.04f;
            float seconds=returnStart+(chain.Count-1)*delay+returnSeconds;
            for(float elapsed=0;elapsed<seconds;elapsed+=MovementDeltaTime){
                for(int i=0;i<chain.Count;i++){
                    float approach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(i*delay,i*delay+approachSeconds,elapsed));
                    float back=returnStart+(chain.Count-1-i)*delay;
                    float rebound=Mathf.SmoothStep(0,1,Mathf.InverseLerp(back,back+returnSeconds,elapsed));
                    geometry[i].localPosition=starts[i]+offsets[i]*approach*(1-rebound);
                }
                pose.Sample(Mathf.Clamp01(elapsed/seconds));
                yield return null;
            }
        }finally{
            for(int i=0;i<geometry.Length;i++)if(geometry[i])geometry[i].localPosition=starts[i];
            if(pose)pose.End();EndPushInterrupt();Busy=false;
        }
    }
    IEnumerator Recoil(MagnetPiece incoming,Vector2Int dir,Vector2Int from){
        PlayMagnetPushSound();
        var magnetStart=incoming.transform.position;var playerStart=player.position;
        var forward=new Vector3(dir.x,0,dir.y);var axis=new Vector3(dir.y,0,-dir.x);
        var facing=Quaternion.LookRotation(forward);
        var approach=magnetStart+forward*(cellSize*.28f);
        var braced=playerStart+forward*(cellSize*.08f);
        // Contact first, then launch both bodies. The far magnet stays pinned by the stone.
        float playerFront=0,magnetRear=0;
        var absolute=new Vector3(Mathf.Abs(forward.x),0,Mathf.Abs(forward.z));
        foreach(var r in player.GetComponentsInChildren<Renderer>())if(r.enabled){
            playerFront=Mathf.Max(playerFront,Vector3.Dot(r.bounds.center-playerStart,forward)+Vector3.Dot(r.bounds.extents,absolute));
        }
        foreach(var r in incoming.geometry.GetComponentsInChildren<Renderer>())if(r.enabled){
            magnetRear=Mathf.Min(magnetRear,Vector3.Dot(r.bounds.center-magnetStart,forward)-Vector3.Dot(r.bounds.extents,absolute));
        }
        var impact=braced+forward*Mathf.Clamp(playerFront-magnetRear,cellSize*.12f,cellSize*.9f);
        var recoilPose=PlayerPushPose.Begin(player);
        try{
        float approachSeconds=Mathf.Max(.42f,stepSeconds*1.9f);
        for(float t=0;t<approachSeconds;t+=MovementDeltaTime){
            float a=Mathf.SmoothStep(0,1,t/approachSeconds);
            incoming.transform.position=Vector3.Lerp(magnetStart,approach,a);
            player.position=Vector3.Lerp(playerStart,braced,a);
            player.rotation=facing;recoilPose.SampleRecoilPush(Mathf.Clamp01(t/.12f),0);yield return null;
        }
        incoming.transform.position=approach;player.position=braced;
        float returnSeconds=Mathf.Max(.20f,stepSeconds);
        for(float t=0;t<returnSeconds;t+=MovementDeltaTime){
            float a=t/returnSeconds;
            incoming.transform.position=Vector3.Lerp(approach,impact,a*a);
            recoilPose.SampleRecoilPush(1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,a)));
            yield return null;
        }
        incoming.transform.position=impact;
        recoilPose.SampleRecoilFlight(0);
        yield return RecoilAcrossIslands(incoming,from,-dir,braced,impact,facing,axis);
        }finally{if(recoilPose&&!ChapterRecoilTravel.Active)recoilPose.End();}
    }
    IEnumerator Assemble(MagnetPiece incoming,MagnetPiece target,Vector2Int dir,Quaternion arrival,MagnetProduct product,Vector2Int next,Vector2Int bridgeDir){
        return AssembleCore(incoming,target,dir,arrival,product,next,bridgeDir,false);
    }
    IEnumerator AssembleCore(MagnetPiece incoming,MagnetPiece target,Vector2Int dir,Quaternion arrival,MagnetProduct product,Vector2Int next,Vector2Int bridgeDir,bool portal){
        if(!portal)BeginPushInterrupt(dir);
        pushHoverParticipants.Add(incoming);pushHoverParticipants.Add(target);
        Vector3 start=incoming.transform.position;Quaternion old=incoming.Pose;float degrees;MagnetPiece.Rolled(incoming.shape,old,dir,out degrees);
        bool uNorth=target.product==MagnetProduct.BridgeHalf?target.baseNorth:incoming.shape==MagnetShape.Horseshoe?incoming.north:target.north;
        bool tippedReceiver=target.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(target.Pose)&&(product==MagnetProduct.WideBar||product==MagnetProduct.Cross);
        bool tippedIncoming=product==MagnetProduct.Cross&&MagnetPiece.VerticalBar(old);
        Quaternion receiverStart=target.Pose;
        Quaternion receiverEnd=tippedReceiver?ReceiverLandingPose(target,dir):receiverStart;
        Quaternion yaw=Yaw(receiverEnd*Vector3.right);
        if(product==MagnetProduct.Ring&&MagnetPiece.FlatU(receiverStart)){
            var opening=receiverStart*Vector3.forward;
            if(AxialRingDock(old,receiverStart,dir)||SideBySideRingDock(old,receiverStart,dir))opening=-new Vector3(dir.x,0,dir.y);
            yaw=Quaternion.LookRotation(opening,Vector3.up);
        }
        // A vertical bar has no horizontal long axis; retain the flat U's facing.
        if(product==MagnetProduct.Lift)yaw=Quaternion.LookRotation((incoming.shape==MagnetShape.Horseshoe?incoming.Pose:target.Pose)*Vector3.forward,Vector3.up);
        if(product==MagnetProduct.BridgeHalf||product==MagnetProduct.Bridge)yaw=Yaw(new Vector3(bridgeDir.x,0,bridgeDir.y));
        // Wide-bar sockets put the incoming rail on local +Z. Choose the
        // equivalent long-axis direction that keeps it on its approach side.
        if(product==MagnetProduct.WideBar&&Vector3.Dot(start-target.transform.position,yaw*Vector3.forward)<-.01f)
            yaw=Quaternion.AngleAxis(180,Vector3.up)*yaw;
        // For end-to-end flat rails, let the receiver yield to the push's right
        // (screen down when pushing right), regardless of either bar's reversed pose.
        var pushAxis=new Vector3(dir.x,0,dir.y);
        if(product==MagnetProduct.WideBar&&!MagnetPiece.VerticalBar(old)&&!MagnetPiece.VerticalBar(receiverStart)
            &&Mathf.Abs(Vector3.Dot(old*Vector3.right,pushAxis))>.95f
            &&Mathf.Abs(Vector3.Dot(receiverStart*Vector3.right,pushAxis))>.95f)
            yaw=Yaw(pushAxis);
        Vector3 playerStart=player.position,playerEnd=PushPlayerEnd(next,incoming);
        var pushPose=portal?null:PlayerPushPose.Begin(player);
        if(!portal)player.rotation=Quaternion.LookRotation(new Vector3(dir.x,0,dir.y));
        // Every recipe drives the player during docking, not with a later Walk.
        bool throwing=(product==MagnetProduct.BridgeHalf||product==MagnetProduct.Bridge||product==MagnetProduct.Lift)&&incoming.shape==MagnetShape.Bar;
        System.Action<float> follow=progress=>{
            if(portal)return;
            float travel=throwing?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,progress)):progress;
            player.position=Vector3.Lerp(playerStart,playerEnd,travel);
            if(throwing)pushPose.SampleBridgeThrow(progress);else pushPose.Sample(progress);
        };
        yield return AnimateAssembly(incoming,target,dir,product,yaw,bridgeDir,uNorth,tippedReceiver,tippedIncoming,receiverEnd,follow);
        if(!portal)EndPushInterrupt();
        if(pushPose)pushPose.End();
        incoming.transform.SetParent(target.transform,true);incoming.enabled=false;incoming.combined=true;incoming.gameObject.SetActive(false);
        target.bridgeDirection=bridgeDir;MagnetVisuals.Product(target,product,cellSize,yaw,uNorth,tippedReceiver&&product==MagnetProduct.Cross,incoming.north);
        if(product==MagnetProduct.Cross&&!tippedReceiver&&!tippedIncoming)target.geometry.localPosition=Vector3.down*.24f;
        if(!portal){player.position=playerEnd;NotifyLanding(next);Busy=false;}
        else {pushHoverParticipants.Remove(incoming);pushHoverParticipants.Remove(target);}
    }
    public IEnumerator AssemblePortalCargo(MagnetPiece incoming,MagnetPiece receiver,Vector2Int direction,MagnetProduct product){
        var bridge=receiver.bridgeDirection;
        if(product==MagnetProduct.BridgeHalf)bridge=Direction((incoming.shape==MagnetShape.Horseshoe?incoming:receiver).Pose*Vector3.forward);
        yield return AssembleCore(incoming,receiver,direction,incoming.Pose,product,Cell(player),bridge,true);
    }
    Vector3 Support(Vector2Int cell,Vector3 from){
        var m=Deck(cell);var p=Position(cell,Height(cell));
        if(m&&m.product==MagnetProduct.Ring){
            var side=m.transform.forward;float sign=Vector3.Dot(from-m.transform.position,side)>=0?1:-1;p+=side*(m.product==MagnetProduct.Ring?.48f:.48f)*sign;
        }
        return p;
    }
    IEnumerator WalkSupported(Vector2Int cell){
        var target=Support(cell,player.position);var source=Piece(Cell(player));var destination=Piece(cell);
        yield return TurnPlayer(target-player.position);
        // Ring traversal follows its solid rim, never the hollow centre.
        if(destination&&destination.product==MagnetProduct.Ring){
            var center=destination.transform.position+Vector3.up*.24f;Vector3 radial=player.position-center;radial.y=0;if(radial.sqrMagnitude<.01f)radial=Vector3.back;
            var entry=center+radial.normalized*.48f;yield return Slide(player,entry,walkSeconds);
            float a0=Mathf.Atan2(radial.z,radial.x),a1=Mathf.Atan2((target-center).z,(target-center).x);float delta=Mathf.DeltaAngle(a0*Mathf.Rad2Deg,a1*Mathf.Rad2Deg)*Mathf.Deg2Rad;
            for(int i=1;i<=8;i++){float a=a0+delta*i/8;yield return Slide(player,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.48f,walkSeconds/8);}
        }else if(source&&source.product==MagnetProduct.Ring){
            var center=source.transform.position+Vector3.up*.24f;var to=target-center;to.y=0;var radial=player.position-center;
            float a0=Mathf.Atan2(radial.z,radial.x),a1=Mathf.Atan2(to.z,to.x);float delta=Mathf.DeltaAngle(a0*Mathf.Rad2Deg,a1*Mathf.Rad2Deg)*Mathf.Deg2Rad;
            for(int i=1;i<=8;i++){float a=a0+delta*i/8;yield return Slide(player,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.48f,walkSeconds/8);}
        }
        yield return TurnPlayer(target-player.position);
        yield return Slide(player,target,walkSeconds);NotifyLanding(cell);
    }
    void NotifyLanding(Vector2Int p){RefreshPlayerSupport();if(deferPushLanding){deferredPushLanding=p;return;}var t=Tile(p);if(t&&t.goal){ReachedGoal=true;if(goalLight&&goalCompleteMaterial)goalLight.sharedMaterial=goalCompleteMaterial;}if(t){SessionExploration.VisitTile(this,t);TrackIsland(t);Landed?.Invoke(t);TryCollectStar(p);}}
    public bool RotorActive(MagnetPiece cross){
        if(!cross||cross.product!=MagnetProduct.Cross)return false;var c=Cell(cross.transform);
        foreach(var axis in new[]{Vector2Int.right,Vector2Int.up}){
            var a=Piece(c-axis);var b=Piece(c+axis);
            if(!a||!b||a.combined||b.combined||a.shape!=MagnetShape.Horseshoe||b.shape!=MagnetShape.Horseshoe||a.north==b.north)continue;
            Vector3 d=new Vector3(axis.x,0,axis.y);
            if(SameHeight(a.transform.position.y,cross.transform.position.y)&&SameHeight(b.transform.position.y,cross.transform.position.y)&&Vector3.Dot(a.Pose*Vector3.forward,d)>.95f&&Vector3.Dot(b.Pose*Vector3.forward,-d)>.95f&&MagnetPiece.FlatU(a.Pose)&&MagnetPiece.FlatU(b.Pose))return true;
        }return false;
    }
    IEnumerator TurnCross(MagnetPiece cross){
        var moves=new List<MagnetPiece>();var destinations=new List<Vector2Int>();var c=Cell(cross.transform);bool blocked=false;
        foreach(var d in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            var piece=Piece(c+d);if(!piece||!SameHeight(piece.transform.position.y,cross.transform.position.y))continue;
            if(!piece.CanBePushed||piece.shape==MagnetShape.Horseshoe){blocked=true;break;}
            if(!MagnetPiece.VerticalBar(piece.Pose))continue;
            var end=c+d*2;if(!Floor(end)||Piece(end)||end==Cell(player)||!SameHeight(GroundHeight(end),piece.transform.position.y)){blocked=true;break;}
            moves.Add(piece);destinations.Add(end);
        }
        var rotation=cross.geometry.localRotation;var starts=moves.Select(m=>m.transform.position).ToArray();
        yield return TurnPlayer(cross.transform.position-player.position);
        if(!blocked)PlayMagnetPushSound();
        var sweepPose=PlayerPushPose.BeginCrossSweep(player);
        try{
            float seconds=Mathf.Max(.01f,crossTurnSeconds);
            for(float t=0;t<seconds;t+=MovementDeltaTime){
                float progress=Mathf.Clamp01(t/seconds),a=PlayerPushPose.CrossSweepProgress(progress);
                sweepPose.SampleCrossSweep(progress);
                float angle=blocked?Mathf.Sin(a*Mathf.PI)*12:90*a;
                cross.geometry.localRotation=Quaternion.AngleAxis(angle,Vector3.up)*rotation;
                if(!blocked)for(int i=0;i<moves.Count;i++)moves[i].transform.position=Vector3.Lerp(starts[i],Position(destinations[i],GroundHeight(destinations[i])),a);
                yield return null;
            }
        }finally{if(sweepPose)sweepPose.End();}
        cross.geometry.localRotation=blocked?rotation:Quaternion.AngleAxis(90,Vector3.up)*rotation;
        if(!blocked)for(int i=0;i<moves.Count;i++)moves[i].transform.position=Position(destinations[i],GroundHeight(destinations[i]));
        LastRule=blocked?"十字受阻回位，物件保持原位":"十字旋转，竖直轻型长条向外移一格";Busy=false;
    }
    bool BeginLift(MagnetPiece device,int levels,Vector2Int direction){
        float bottom=device.transform.position.y,top=bottom+LayerHeight*levels;
        if(!SameHeight(player.position.y,bottom)&&!SameHeight(player.position.y,top))return Reject("需要从装置入口所在层进入");
        Busy=true;StartMovement(LiftTravel(device,levels,direction,SameHeight(player.position.y,bottom)));LastRule=levels==1?"进入单层磁流":"进入两层磁流";return true;
    }
    IEnumerator LiftTravel(MagnetPiece device,int levels,Vector2Int direction,bool up){
        var c=Cell(device.transform);float bottom=device.transform.position.y,top=bottom+LayerHeight*levels;
        var d=new Vector3(direction.x,0,direction.y);float clearance=levels==2?.64f:.48f;var side=new Vector3(-d.z,0,d.x)*clearance;
        var entry=device.transform.position-d*clearance+side;entry.y=up?bottom:top;
        yield return Slide(player,entry,stepSeconds);
        var exit=entry;exit.y=up?top:bottom;yield return Slide(player,exit,joinSeconds*levels*1.5f);
        // Keep the player beside the column. A following normal step chooses the exit tile.
        CommitLiftSupport(device);
        Busy=false;
    }
    void LateUpdate(){
        UpdateMagnetHover();
        UpdateBridgeJunctionLinks();
        TickKnockedMagnets();
        foreach(var m in magnets){if(!m||!m.enabled||!m.gameObject.activeInHierarchy)continue;
            bool active=m.product==MagnetProduct.Lift||(m.product==MagnetProduct.Cross&&RotorActive(m));
            var flow=m.GetComponent<MagnetFlow>();if(active&&!flow)flow=m.gameObject.AddComponent<MagnetFlow>();
            if(flow){flow.height=LayerHeight*(m.product==MagnetProduct.Cross?2:1);flow.active=active;}
            if(active&&m.product==MagnetProduct.Cross&&!Busy)m.geometry.Rotate(Vector3.up,MovementDeltaTime*60,Space.World);
        }
    }
}
}
