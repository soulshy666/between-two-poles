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
    float GroundHeight(Vector2Int p){var t=Tile(p);return t?t.surfaceHeight:0;}
    // Only absorb floating-point drift; this is not an automatic step-up allowance.
    bool SameHeight(float a,float b){return Mathf.Abs(a-b)<.01f;}
    Quaternion Yaw(Vector3 axis){axis.y=0;if(axis.sqrMagnitude<.01f)axis=Vector3.right;return Quaternion.FromToRotation(Vector3.right,axis.normalized);}
    Vector2Int Direction(Vector3 v){return Mathf.Abs(v.x)>Mathf.Abs(v.z)?new Vector2Int(v.x>=0?1:-1,0):new Vector2Int(0,v.z>=0?1:-1);}
    static Quaternion ReceiverLandingPose(MagnetPiece receiver,Vector2Int push) {
        if(receiver.shape!=MagnetShape.Bar||!MagnetPiece.VerticalBar(receiver.Pose))return receiver.Pose;
        // Tip the upright receiver away from the player, in the push direction.
        var axis=new Vector3(push.y,0,-push.x);
        float angle=(receiver.Pose*Vector3.right).y>=0?90:-90;
        return Quaternion.AngleAxis(angle,axis)*receiver.Pose;
    }
    public static MagnetProduct Recipe(MagnetPiece incoming,Quaternion arrival,MagnetPiece target,bool receiverSupported,Vector2Int push) {
        if(target.product==MagnetProduct.BridgeHalf)return incoming.shape==MagnetShape.Bar&&incoming.north!=target.baseNorth&&!MagnetPiece.VerticalBar(arrival)?MagnetProduct.Bridge:MagnetProduct.None;
        if(target.combined||incoming.north==target.north)return MagnetProduct.None;
        if(incoming.shape==target.shape){
            if(incoming.shape==MagnetShape.Horseshoe)return MagnetProduct.Ring;
            bool receiverStanding=MagnetPiece.VerticalBar(target.Pose);
            var a=arrival*Vector3.right;var b=ReceiverLandingPose(target,push)*Vector3.right;
            // Either originally upright bar can fall onto the other, even on ice.
            bool perpendicular=Mathf.Abs(a.y)<.05f&&Mathf.Abs(b.y)<.05f&&Mathf.Abs(Vector3.Dot(a,b))<.05f;
            return perpendicular&&(receiverStanding||MagnetPiece.VerticalBar(incoming.Pose)||!receiverSupported)?MagnetProduct.Cross:MagnetProduct.WideBar;
        }
        Quaternion u=incoming.shape==MagnetShape.Horseshoe?arrival:target.Pose;
        Quaternion bar=incoming.shape==MagnetShape.Bar?arrival:target.Pose;
        if(!MagnetPiece.FlatU(u))return MagnetProduct.None;
        if(MagnetPiece.VerticalBar(bar))return MagnetProduct.Lift;
        return Mathf.Abs((bar*Vector3.right).y)<.05f?MagnetProduct.BridgeHalf:MagnetProduct.None;
    }
    public bool TryStep(Vector2Int dir) {
        if(Busy||Mathf.Abs(dir.x)+Mathf.Abs(dir.y)!=1)return false;
        var before=SaveWorld();
        bool accepted=TryStepCore(dir);
        if(accepted)RecordHistory(before);
        return accepted;
    }
    bool TryStepCore(Vector2Int dir) {
        if(Busy||Mathf.Abs(dir.x)+Mathf.Abs(dir.y)!=1)return false;
        var from=Cell(player);var support=Piece(from);
        if(support&&support.product==MagnetProduct.Bridge){var side=support.transform.forward;float across=Vector3.Dot(new Vector3(dir.x,0,dir.y),side);float lane=Vector3.Dot(player.position-support.transform.position,side);if(Mathf.Abs(across)>.5f&&across*lane<0)return Reject("桥中央镂空，请沿当前侧梁行走");}
        var next=from+dir;var tile=Tile(next);var m=Piece(next);
        if(tile&&tile.blocked)return Reject("石头挡住了这个格子");
        if(m&&m.product==MagnetProduct.Cross){
            if(RotorActive(m))return BeginLift(m,2,dir);
            if(!SameHeight(player.position.y,m.transform.position.y))return Reject("高度不一致");
            Busy=true;StartCoroutine(TurnCross(m));LastRule="推动十字：原地顺时针旋转";return true;
        }
        if(m&&m.product==MagnetProduct.Lift)return BeginLift(m,1,dir);
        if(m&&!m.walkable) {
            if(!m.CanBePushed)return Reject("只有单个磁铁可以推动；合成后固定在原格");
            if(!SameHeight(player.position.y,m.transform.position.y))return Reject("需要站在同一层推动");
            var target=next+dir;var far=Piece(target,m);var land=Tile(target);
            if(land&&land.blocked)return Reject("目标格被石头占据");
            if(land&&!SameHeight(land.surfaceHeight,m.transform.position.y))return Reject("不能将磁铁推上高台");
            float degrees;var arrival=MagnetPiece.Rolled(m.shape,m.Pose,dir,out degrees);
            if(far){
                if(!SameHeight(far.transform.position.y,m.transform.position.y))return Reject("磁铁高度不一致");
                // A bridge's second socket belongs to its U base, not the first rail.
                if(far.product!=MagnetProduct.BridgeHalf&&!far.CanBePushed)return Reject("组合磁铁固定在原格，不能被推动或排斥移位");
                if(far.product!=MagnetProduct.BridgeHalf&&far.north==m.north){
                    var beyond=target+dir;var beyondTile=Tile(beyond);
                    if(Floor(beyond)&&!Piece(beyond)&&SameHeight(GroundHeight(beyond),far.transform.position.y)){
                        Busy=true;StartCoroutine(Repel(m,far,dir,next,target,beyond));LastRule="同极排斥：两块前移一格";return true;
                    }
                    var back=from-dir;
                    if(beyondTile&&beyondTile.blocked&&Floor(back)&&!Piece(back)&&SameHeight(GroundHeight(back),player.position.y)){
                        Busy=true;StartCoroutine(Recoil(back));LastRule="远端受石头阻挡：主角反冲";return true;
                    }
                    return Reject("远端没有落脚位，无法推动；反冲需要石头阻挡与安全后退格");
                }
                bool receiverSupported=land&&!land.blocked&&SameHeight(land.surfaceHeight,far.transform.position.y);
                var result=Recipe(m,arrival,far,receiverSupported,dir);
                if(result==MagnetProduct.None)return Reject("抵达姿态不符合配方，保留原状态");
                var bridgeDir=far.bridgeDirection;
                if(result==MagnetProduct.BridgeHalf){var uPose=m.shape==MagnetShape.Horseshoe?arrival:far.Pose;bridgeDir=Direction(uPose*Vector3.forward);}
                if(result==MagnetProduct.BridgeHalf||result==MagnetProduct.Bridge){
                    var second=Cell(far.transform)+bridgeDir;
                    var occupant=Piece(second,far);
                    var ground=Tile(second);
                    if((ground&&(ground.blocked||!SameHeight(ground.surfaceHeight,far.transform.position.y)))||occupant||second==from||second==next)return Reject("两格桥可位于普通同高地面，但不能覆盖石头、高差、其他材料或主角");
                }
                Busy=true;StartCoroutine(Assemble(m,far,dir,arrival,result,next,bridgeDir));LastRule="合成："+ProductName(result);return true;
            }
            if(!Floor(target))return Reject("单块磁铁不能推入无支撑的太空格");
            Busy=true;StartCoroutine(RollThenWalk(m,dir,target,next));LastRule="翻滚推动一格";return true;
        }
        if(!Floor(next)&&!(m&&m.walkable))return Reject("没有可行走的支撑面");
        if(!SameHeight(player.position.y,Height(next)))return Reject("高差需要磁流升降装置");
        Busy=true;StartCoroutine(MovePlayer(next));LastRule="行走";return true;
    }
    public static string ProductName(MagnetProduct p){switch(p){case MagnetProduct.WideBar:return "一格宽条";case MagnetProduct.Ring:return "闭合圆环";case MagnetProduct.Cross:return "叠放十字";case MagnetProduct.Lift:return "单层磁流升降台";case MagnetProduct.BridgeHalf:return "两格桥半成品";case MagnetProduct.Bridge:return "两格桥";default:return "基础磁铁";}}
    IEnumerator RollPiece(MagnetPiece m,Vector2Int dir,Vector2Int to){
        Vector3 start=m.transform.position,end=Position(to,GroundHeight(to));Quaternion pose=m.Pose;float degrees;var final=MagnetPiece.Rolled(m.shape,pose,dir,out degrees);var axis=new Vector3(dir.y,0,-dir.x);
        for(float t=0;t<stepSeconds;t+=Time.deltaTime){float a=Mathf.SmoothStep(0,1,t/stepSeconds);m.transform.position=Vector3.Lerp(start,end,a);m.geometry.rotation=Quaternion.AngleAxis(degrees*a,axis)*pose;MagnetVisuals.Ground(m);yield return null;}
        m.transform.position=end;m.geometry.rotation=final;MagnetVisuals.Ground(m);
    }
    IEnumerator PushSingleBar(MagnetPiece m,Vector2Int dir,Vector2Int target,Vector2Int next){
        Vector3 start=m.transform.position,end=Position(target,GroundHeight(target));
        Vector3 playerStart=player.position,playerEnd=Position(next,GroundHeight(next));
        Vector3 forward=new Vector3(dir.x,0,dir.y),axis=new Vector3(dir.y,0,-dir.x);
        Quaternion facing=Quaternion.LookRotation(forward),pose=m.Pose;
        float degrees;Quaternion final=MagnetPiece.Rolled(m.shape,pose,dir,out degrees);
        // A short plant/lean precedes the heavy movement; the player follows
        // during the push instead of waiting for the magnet to finish moving.
        float duration=Mathf.Max(.85f,stepSeconds*3);
        for(float elapsed=0;elapsed<duration;elapsed+=Time.deltaTime){
            float p=Mathf.Clamp01(elapsed/duration);
            float brace=AssemblyPhase(p,0,.20f),drive=AssemblyPhase(p,.20f,.86f),settle=AssemblyPhase(p,.86f,1);
            float effort=brace*(1-settle);
            player.rotation=Quaternion.AngleAxis(10*effort,axis)*facing;
            player.position=Vector3.Lerp(playerStart,playerEnd,drive)+forward*(cellSize*.12f*brace*(1-drive));
            m.transform.position=Vector3.Lerp(start,end,drive);
            m.geometry.rotation=Quaternion.AngleAxis(degrees*drive,axis)*pose;
            MagnetVisuals.Ground(m);
            yield return null;
        }
        m.transform.position=end;m.geometry.rotation=final;MagnetVisuals.Ground(m);
        player.SetPositionAndRotation(playerEnd,facing);NotifyLanding(next);
    }
    IEnumerator RollThenWalk(MagnetPiece m,Vector2Int dir,Vector2Int target,Vector2Int next){
        if(m.shape==MagnetShape.Bar)yield return PushSingleBar(m,dir,target,next);
        else {yield return RollPiece(m,dir,target);yield return Walk(next);}
        Busy=false;
    }
    IEnumerator Repel(MagnetPiece near,MagnetPiece far,Vector2Int dir,Vector2Int next,Vector2Int target,Vector2Int beyond){yield return RollPiece(far,dir,beyond);yield return RollPiece(near,dir,target);yield return Walk(next);Busy=false;}
    IEnumerator Recoil(Vector2Int back){var start=player.position;var end=Position(back,GroundHeight(back));float duration=stepSeconds*2;for(float t=0;t<duration;t+=Time.deltaTime){float a=t/duration;player.position=Vector3.Lerp(start,end,a)+Vector3.up*Mathf.Sin(a*Mathf.PI)*.6f;yield return null;}player.position=end;NotifyLanding(back);Busy=false;}
    IEnumerator Assemble(MagnetPiece incoming,MagnetPiece target,Vector2Int dir,Quaternion arrival,MagnetProduct product,Vector2Int next,Vector2Int bridgeDir){
        Vector3 start=incoming.transform.position;Quaternion old=incoming.Pose;float degrees;MagnetPiece.Rolled(incoming.shape,old,dir,out degrees);
        bool uNorth=target.product==MagnetProduct.BridgeHalf?target.baseNorth:incoming.shape==MagnetShape.Horseshoe?incoming.north:target.north;
        bool tippedReceiver=target.shape==MagnetShape.Bar&&MagnetPiece.VerticalBar(target.Pose)&&(product==MagnetProduct.WideBar||product==MagnetProduct.Cross);
        bool tippedIncoming=product==MagnetProduct.Cross&&MagnetPiece.VerticalBar(old);
        Quaternion receiverStart=target.Pose;
        Quaternion receiverEnd=tippedReceiver?ReceiverLandingPose(target,dir):receiverStart;
        Quaternion yaw=Yaw(receiverEnd*Vector3.right);
        if(product==MagnetProduct.BridgeHalf||product==MagnetProduct.Bridge)yaw=Yaw(new Vector3(bridgeDir.x,0,bridgeDir.y));
        bool pushTogether=product==MagnetProduct.BridgeHalf&&incoming.shape==MagnetShape.Bar
            &&Mathf.Abs((old*Vector3.right).y)<.05f;
        Vector3 playerStart=player.position,playerEnd=Position(next,GroundHeight(next));
        System.Action<float> follow=null;
        if(pushTogether){
            player.rotation=Quaternion.LookRotation(new Vector3(dir.x,0,dir.y));
            // Use the bar's exact animation progress, so both bodies start and settle together.
            follow=progress=>player.position=Vector3.Lerp(playerStart,playerEnd,progress);
        }
        yield return AnimateAssembly(incoming,target,dir,product,yaw,bridgeDir,uNorth,tippedReceiver,tippedIncoming,receiverEnd,follow);
        incoming.transform.SetParent(target.transform,true);incoming.enabled=false;incoming.combined=true;incoming.gameObject.SetActive(false);
        target.bridgeDirection=bridgeDir;MagnetVisuals.Product(target,product,cellSize,yaw,uNorth,tippedReceiver&&product==MagnetProduct.Cross);
        if(product==MagnetProduct.Cross&&!tippedReceiver&&!tippedIncoming)target.geometry.localPosition=Vector3.down*.24f;
        if(pushTogether)NotifyLanding(next);
        else yield return Walk(next);
        Busy=false;
    }
    Vector3 Support(Vector2Int cell,Vector3 from){
        var m=Piece(cell);var p=Position(cell,Height(cell));
        if(m&&(m.product==MagnetProduct.Bridge||m.product==MagnetProduct.Ring)){
            var side=m.transform.forward;float sign=Vector3.Dot(from-m.transform.position,side)>=0?1:-1;p+=side*(m.product==MagnetProduct.Ring?.48f:.48f)*sign;
        }
        return p;
    }
    IEnumerator WalkSupported(Vector2Int cell){
        var target=Support(cell,player.position);var source=Piece(Cell(player));var destination=Piece(cell);
        // Ring traversal follows its solid rim, never the hollow centre.
        if(destination&&destination.product==MagnetProduct.Ring){
            var center=destination.transform.position+Vector3.up*.24f;Vector3 radial=player.position-center;radial.y=0;if(radial.sqrMagnitude<.01f)radial=Vector3.back;
            var entry=center+radial.normalized*.48f;yield return Slide(player,entry,stepSeconds);
            float a0=Mathf.Atan2(radial.z,radial.x),a1=Mathf.Atan2((target-center).z,(target-center).x);float delta=Mathf.DeltaAngle(a0*Mathf.Rad2Deg,a1*Mathf.Rad2Deg)*Mathf.Deg2Rad;
            for(int i=1;i<=8;i++){float a=a0+delta*i/8;yield return Slide(player,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.48f,stepSeconds/8);}
        }else if(source&&source.product==MagnetProduct.Ring){
            var center=source.transform.position+Vector3.up*.24f;var to=target-center;to.y=0;var radial=player.position-center;
            float a0=Mathf.Atan2(radial.z,radial.x),a1=Mathf.Atan2(to.z,to.x);float delta=Mathf.DeltaAngle(a0*Mathf.Rad2Deg,a1*Mathf.Rad2Deg)*Mathf.Deg2Rad;
            for(int i=1;i<=8;i++){float a=a0+delta*i/8;yield return Slide(player,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.48f,stepSeconds/8);}
        }
        Vector3 direction=target-player.position;direction.y=0;if(direction.sqrMagnitude>.01f)player.rotation=Quaternion.LookRotation(direction);
        yield return Slide(player,target,stepSeconds);NotifyLanding(cell);
    }
    void NotifyLanding(Vector2Int p){var t=Tile(p);if(t&&t.goal){ReachedGoal=true;if(goalLight&&goalCompleteMaterial)goalLight.sharedMaterial=goalCompleteMaterial;}if(t){TrackIsland(t);Landed?.Invoke(t);}}
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
        for(float t=0;t<joinSeconds;t+=Time.deltaTime){float a=Mathf.SmoothStep(0,1,t/joinSeconds);float angle=blocked?Mathf.Sin(a*Mathf.PI)*12:90*a;cross.geometry.localRotation=Quaternion.AngleAxis(angle,Vector3.up)*rotation;if(!blocked)for(int i=0;i<moves.Count;i++)moves[i].transform.position=Vector3.Lerp(starts[i],Position(destinations[i],GroundHeight(destinations[i])),a);yield return null;}
        cross.geometry.localRotation=blocked?rotation:Quaternion.AngleAxis(90,Vector3.up)*rotation;
        if(!blocked)for(int i=0;i<moves.Count;i++)moves[i].transform.position=Position(destinations[i],GroundHeight(destinations[i]));
        LastRule=blocked?"十字受阻回位，物件保持原位":"十字旋转，竖直轻型长条向外移一格";Busy=false;
    }
    bool BeginLift(MagnetPiece device,int levels,Vector2Int direction){
        float bottom=device.transform.position.y,top=bottom+cellSize*levels;
        if(!SameHeight(player.position.y,bottom)&&!SameHeight(player.position.y,top))return Reject("需要从装置入口所在层进入");
        Busy=true;StartCoroutine(LiftTravel(device,levels,direction,player.position.y<bottom+.5f));LastRule=levels==1?"进入单层磁流":"进入两层磁流";return true;
    }
    IEnumerator LiftTravel(MagnetPiece device,int levels,Vector2Int direction,bool up){
        var c=Cell(device.transform);float bottom=device.transform.position.y,top=bottom+cellSize*levels;
        var d=new Vector3(direction.x,0,direction.y);float clearance=levels==2?.64f:.48f;var side=new Vector3(-d.z,0,d.x)*clearance;
        var entry=device.transform.position-d*clearance+side;entry.y=up?bottom:top;
        yield return Slide(player,entry,stepSeconds);
        var exit=entry;exit.y=up?top:bottom;yield return Slide(player,exit,joinSeconds*levels*1.5f);
        // Keep the player beside the column. A following normal step chooses the exit tile.
        Busy=false;
    }
    void LateUpdate(){
        foreach(var m in magnets){if(!m||!m.enabled||!m.gameObject.activeInHierarchy)continue;
            bool active=m.product==MagnetProduct.Lift||(m.product==MagnetProduct.Cross&&RotorActive(m));
            var flow=m.GetComponent<MagnetFlow>();if(active&&!flow)flow=m.gameObject.AddComponent<MagnetFlow>();
            if(flow){flow.height=cellSize*(m.product==MagnetProduct.Cross?2:1);flow.active=active;}
            if(active&&m.product==MagnetProduct.Cross&&!Busy)m.geometry.Rotate(Vector3.up,Time.deltaTime*60,Space.World);
        }
    }
}
}
