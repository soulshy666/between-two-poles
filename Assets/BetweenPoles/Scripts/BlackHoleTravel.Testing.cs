using System.Linq;
using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    BlackHolePortal[] testPortals;
    BlackHolePortal testDestination,previewPortal;
    string testMessage;
    BlackHolePortal chainedEntry;
    Vector2Int chainedEntryDirection;
    IEnumerator TestTravel(BlackHolePortal source){
        chainedEntry=null;
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
            yield return OpenPortalMap();

            testMessage="点击已探索的黑洞出洞；出口受阻时会弹回入口。左键拖拽查看，滚轮缩放。";
            busy=false;
            while(!testDestination){
                if(Input.GetKeyDown(KeyCode.Z)){undoRequested=true;yield break;}
                UpdateNavigation();yield return null;
            }
            busy=true;var destination=testDestination;
            ExitPlan plan;var direction=entryDirection;
            var cargo=source.Cargo;
            bool split=SeparateCargo(cargo,direction);
            bool valid=PlanJourneyExit(destination,direction,split?null:cargo,out plan);
            if(!valid)yield break;

            yield return PlayAnimation((split?"split/":"")+ExitAnimationKind(plan,cargo),CompleteExitJourney(source,destination,direction,cargo,plan,playerScale));
        }finally{
            EndNavigation();testPortals=null;testDestination=null;previewPortal=null;Selecting=false;busy=false;
            if(board&&board.player)board.player.localScale=playerScale;
            if(board){board.SetPortalBusy(false);board.enabled=wasEnabled;board.EndPortalMagnetPresentation();}
            if(board&&board.player){var audio=board.player.GetComponent<PortalAudio>();if(audio)audio.StopAll();}
            if(undoRequested&&board)board.UndoStep();
        }
        // Begin only after the previous journey releases selection and board
        // ownership. No extra frame or grounded entry animation is needed.
        var nextEntry=chainedEntry;var nextDirection=chainedEntryDirection;chainedEntry=null;
        if(!undoRequested&&nextEntry&&nextEntry.CanArrive)nextEntry.Enter(nextDirection);
    }
    IEnumerator CompleteExitJourney(BlackHolePortal source,BlackHolePortal destination,Vector2Int direction,MagnetPiece cargo,ExitPlan plan,Vector3 playerScale){
        var board=source.board;
            destination.Disarm();
            board.player.SetPositionAndRotation(new Vector3(destination.tile.transform.position.x,destination.tile.surfaceHeight,destination.tile.transform.position.z),entryFacing);
            board.CompletePortalLanding(destination.Cell);
            yield return WaitForExitView(destination);
            bool split=SeparateCargo(cargo,direction);
            bool ringHitPlayer=false,ringPushedObstacle=false;
            if(split){
                bool rolling=cargo.portalRollSpeed>0;
                yield return EjectIndependentCargo(destination,CargoExitDirection(cargo,direction),cargo,playerScale,direction,()=>ringHitPlayer=true,()=>ringPushedObstacle=true);
                if(ringHitPlayer&&cargo.storedInPortal==destination){
                    // The collision sends both bodies back through the journey's
                    // entrance. Keep the ring's newly reversed entry momentum;
                    // changing the destination must not rewrite its stored lane.
                    direction=CargoExitDirection(cargo,-direction);
                    var reboundMouth=destination;
                    destination=source;destination.Disarm();
                    board.player.SetPositionAndRotation(
                        new Vector3(destination.tile.transform.position.x,destination.tile.surfaceHeight,destination.tile.transform.position.z),
                        Quaternion.LookRotation(new Vector3(direction.x,0,direction.y)));
                    board.CompletePortalLanding(destination.Cell);
                    if(destination!=reboundMouth)yield return WaitForExitView(destination);
                    ringHitPlayer=false;ringPushedObstacle=false;
                    yield return EjectIndependentCargo(destination,CargoExitDirection(cargo,direction),cargo,playerScale,direction,
                        ()=>ringHitPlayer=true,()=>ringPushedObstacle=true);
                    // Retry only once: two obstructed sides retain the existing
                    // safe recovery instead of bouncing forever.
                }
                cargo=null;
                PlanJourneyExit(destination,direction,null,out plan);
                // A wheel stopped immediately outside the mouth is an obstacle,
                // not a new request to topple it across its rolling axis.
                if(rolling&&plan.exitKickTarget&&plan.exitKickTarget.product==MagnetProduct.Ring)
                    plan=new ExitPlan{failedExit=true};
                if(ringPushedObstacle&&!plan.kick){
                    GridTile landingTile;Vector3 landing;
                    // Keep a validated follow-up kick, including a flat ring shove.
                    // If no legal kick exists, retain the safe landing fallback.
                    if(destination.TryExitLanding(direction,out landingTile,out landing))plan=new ExitPlan{player=landing};
                }
                if(ringHitPlayer)plan=new ExitPlan{failedExit=true};
            }
            board.player.localScale=playerScale;
            if(ringHitPlayer)board.player.localScale=Vector3.zero;
            else if(plan.failedExit)yield return FailedPortalExit(destination,direction,cargo,playerScale);
            else yield return Eject(destination,direction,cargo,plan);
            if(plan.unstableLanding||plan.failedExit||plan.kickRebound){
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
                var returningCargo=plan.failedExit&&!split?source.Cargo:null;
                if(PlanExit(source,returnDirection,returningCargo,out returnPlan)&&!returnPlan.unstableLanding&&!returnPlan.bounce&&!returnPlan.kickRebound){
                    yield return Eject(source,returnDirection,returningCargo,returnPlan);
                }else{
                    // If the former entry side is no longer safe, recover on the
                    // entrance tile itself instead of repeating a failed landing.
                    board.SetPortalBusy(true);
                    try{yield return EjectPlayer(board.player,returnStart,returnStart,playerScale);}
                    finally{board.SetPortalBusy(false);}
                    board.CompletePortalLanding(source.Cell);
                }
            }
    }
    bool SelectTestPortal(BlackHolePortal portal){
        ExitPlan plan;
        if(portal&&origin.Cargo&&portal.Cargo&&portal.Cargo!=origin.Cargo){testMessage="这个黑洞已存有磁铁，请选择空闲黑洞。";return false;}
        bool valid=portal&&PlanJourneyExit(portal,entryDirection,origin.Cargo,out plan);
        if(!valid){testMessage="这个黑洞已不存在，请选择其他黑洞。";return false;}
        testDestination=portal;return true;
    }
    static bool SeparateCargo(MagnetPiece cargo,Vector2Int direction){
        return cargo&&(cargo.product==MagnetProduct.Ring||cargo.portalRollSpeed>0||CargoExitDirection(cargo,direction)!=direction);
    }
    static Vector2Int CargoExitDirection(MagnetPiece cargo,Vector2Int fallback){
        if(!cargo||!cargo.storedInPortal)return fallback;
        var source=cargo.storedInPortal;
        var entry=new Vector2Int(Mathf.RoundToInt(cargo.portalEntryPosition.x/source.board.cellSize),Mathf.RoundToInt(cargo.portalEntryPosition.z/source.board.cellSize));
        var delta=source.Cell-entry;
        if(delta==Vector2Int.zero)return fallback;
        return Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?new Vector2Int(delta.x>0?1:-1,0):new Vector2Int(0,delta.y>0?1:-1);
    }
    IEnumerator EjectIndependentCargo(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,Vector3 playerScale,Vector2Int playerDirection=default(Vector2Int),System.Action onPlayerHit=null,System.Action onObstaclePushed=null){
        var receiver=target.board.PortalObstacle(target.Cell+direction);
        var exitStone=target.board.TileAt(target.Cell+direction);
        bool solidExit=SolidRingExit(target,exitStone);
        // Walkable decks can be terrain; only intercept an actual blocked ring lane.
        if(cargo.product==MagnetProduct.Ring&&(solidExit||receiver)&&!target.board.CanPortalRingEnter(cargo,target.Cell,target.tile.surfaceHeight,target.Cell+direction)){
            Vector3 pushed;
            if(!solidExit&&TryRingExitPush(target,cargo,receiver,direction,out pushed)){
                yield return PushRingExitObstacle(target,direction,cargo,receiver,pushed);
                if(playerDirection==direction)onObstaclePushed?.Invoke();
                yield break;
            }
            if(playerDirection==direction){
                var impactHole=new Vector3(target.tile.transform.position.x,target.tile.surfaceHeight,target.tile.transform.position.z);
                var impactPose=cargo.Pose;var impactScale=cargo.geometry.localScale;
                var impactPoint=solidExit?StoneContact(target,exitStone,cargo,direction):MagnetContact(target,receiver,cargo,direction);
                cargo.transform.position=impactHole;cargo.gameObject.SetActive(true);
                var binding=cargo.GetComponent<IslandSurfaceAnchor>();var owner=target.tile.GetComponentInParent<IslandSurfaceAnchor>();
                if(binding&&owner){binding.center=owner.center;binding.Apply();}
                target.board.player.localScale=Vector3.zero;
                if(cargo.portalRollSpeed>0)yield return ThrowRollingRebound(cargo,impactHole,impactPoint,impactPose,impactScale,direction,false);
                else yield return ThrowCargo(cargo,impactHole,impactPoint,impactPose,impactScale,.28f,.24f,landingSound:false);
                yield return BounceCargo(cargo,target,target,impactPoint,impactHole,cargo.Pose,impactScale,playerScale,true);
                cargo.portalEntryPosition=impactHole+new Vector3(direction.x,0,direction.y)*target.board.cellSize;
                onPlayerHit?.Invoke();yield break;
            }
        }
        ExitPlan cargoPlan;
        if(PlanExitCore(target,direction,cargo,out cargoPlan,true)&&!cargoPlan.bounce){
            cargoPlan.separateCargo=true;
            yield return Eject(target,direction,cargo,cargoPlan);
            target.board.player.localScale=Vector3.zero;yield break;
        }
        var stone=target.board.TileAt(target.Cell+direction);
        var obstacle=target.board.MagnetAt(target.Cell+direction);
        if(cargo.product==MagnetProduct.Ring&&(SolidRingExit(target,stone)||obstacle)&&target.CanArrive&&(!target.Cargo||target.Cargo==cargo)){
            // The returning ring enters this mouth from the impact side. Its next
            // launch reverses independently of the actor's remembered entry lane.
            for(int attempt=0;attempt<2;attempt++){
                yield return ReboundIndependentRing(target,direction,cargo,stone,obstacle);
                direction=-direction;
                if(PlanExitCore(target,direction,cargo,out cargoPlan,true)&&!cargoPlan.bounce){
                    cargoPlan.separateCargo=true;
                    yield return Eject(target,direction,cargo,cargoPlan);
                    target.board.player.localScale=Vector3.zero;yield break;
                }
                stone=target.board.TileAt(target.Cell+direction);
                obstacle=target.board.MagnetAt(target.Cell+direction);
                if(!SolidRingExit(target,stone)&&!obstacle)break;
            }
            // Two blocked sides must not trap the journey in an endless bounce.
            yield break;
        }
        // Other blocked cargo lanes retain their existing storage behavior.
        var savedPosition=cargo.transform.position;var pose=cargo.Pose;var scale=cargo.geometry.localScale;
        var hole=new Vector3(target.transform.position.x,target.tile.surfaceHeight,target.transform.position.z);
        var hit=hole+new Vector3(direction.x,0,direction.y)*target.board.cellSize*.22f;
        target.board.player.localScale=Vector3.zero;
        try{
            cargo.gameObject.SetActive(true);
            yield return ThrowCargo(cargo,hole,hit,pose,scale,.3f,.25f);
            yield return ThrowCargo(cargo,hit,hole,pose,scale,.35f,.3f);
        }finally{cargo.gameObject.SetActive(false);cargo.transform.position=savedPosition;cargo.geometry.rotation=pose;cargo.geometry.localScale=scale;MagnetVisuals.Ground(cargo);}
    }
    static bool SolidRingExit(BlackHolePortal portal,GridTile tile){
        return tile&&(tile.blocked||tile.surfaceHeight>portal.tile.surfaceHeight+.01f);
    }
    static bool TryRingExitPush(BlackHolePortal portal,MagnetPiece ring,MagnetPiece receiver,Vector2Int direction,out Vector3 destination){
        destination=Vector3.zero;
        if(!receiver||!receiver.CanBePushed||!receiver.enabled||!receiver.gameObject.activeInHierarchy)return false;
        var board=portal.board;var cell=portal.Cell+direction;float floorHeight;
        if(!board.TryPortalFloor(cell,out floorHeight)||Mathf.Abs(floorHeight-portal.tile.surfaceHeight)>.01f
            ||Mathf.Abs(receiver.transform.position.y-floorHeight)>.01f||board.PortalObstacle(cell)!=receiver)return false;
        var arrival=board.InspectMagnetDestination(cell,receiver.transform.position.y,cell+direction,receiver);
        // Never push a second cargo into the occupied shared box, or a chain of magnets.
        if(arrival.kind!=GridPlayground.MagnetLandingKind.Ground&&arrival.kind!=GridPlayground.MagnetLandingKind.Deck
            &&arrival.kind!=GridPlayground.MagnetLandingKind.Space)return false;
        destination=arrival.position;return true;
    }
    IEnumerator PushRingExitObstacle(BlackHolePortal portal,Vector2Int direction,MagnetPiece ring,MagnetPiece receiver,Vector3 destination){
        var board=portal.board;var start=new Vector3(portal.tile.transform.position.x,portal.tile.surfaceHeight,portal.tile.transform.position.z);
        var end=receiver.transform.position;var pose=ring.Pose;var scale=ring.geometry.localScale;
        var axis=new Vector3(direction.y,0,-direction.x);bool rolling=ring.portalRollSpeed>0;
        var receiverPose=receiver.Pose;float receiverDegrees=ImpactPushDegrees(receiver,direction);
        float contact=Mathf.Clamp01(Vector3.Distance(start,MagnetContact(portal,receiver,ring,direction))/board.cellSize);
        float seconds=rolling?board.cellSize/Mathf.Max(.01f,ring.portalRollSpeed):.55f;
        var binding=ring.GetComponent<IslandSurfaceAnchor>();var owner=portal.tile.GetComponentInParent<IslandSurfaceAnchor>();
        if(binding&&owner){binding.center=owner.center;binding.Apply();}
        ring.gameObject.SetActive(true);ring.storedInPortal=null;
        board.AnimatePortalMagnet(ring,true);board.AnimatePortalMagnet(receiver,true);
        try{
            for(float elapsed=0;elapsed<seconds;){
                elapsed=Mathf.Min(seconds,elapsed+AnimationDeltaTime);float p=elapsed/seconds;
                ring.transform.position=Vector3.Lerp(start,end,p);
                ring.geometry.rotation=rolling?Quaternion.AngleAxis(180*p,axis)*pose:pose;
                ring.geometry.localScale=scale*Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.2f));
                MagnetVisuals.Ground(ring);ring.geometry.position+=Vector3.up*(1.52f*p*(1-p));
                ApplyImpactPush(receiver,end,destination,receiverPose,axis,receiverDegrees,Mathf.InverseLerp(contact,1,p));
                if(elapsed<seconds)yield return null;
            }
            ring.transform.position=end;ring.geometry.localScale=scale;MagnetVisuals.Ground(ring);
            ApplyImpactPush(receiver,end,destination,receiverPose,axis,receiverDegrees,1);
            yield return board.SettleMagnetArrival(receiver,end,true);
            ring.portalRollSpeed=0;
        }finally{ring.geometry.localScale=scale;board.AnimatePortalMagnet(ring,false);board.AnimatePortalMagnet(receiver,false);}
    }
    IEnumerator ReboundIndependentRing(BlackHolePortal target,Vector2Int direction,MagnetPiece cargo,GridTile stone,MagnetPiece obstacle){
        var board=target.board;var pose=cargo.Pose;var scale=cargo.geometry.localScale;
        var hole=new Vector3(target.tile.transform.position.x,target.tile.surfaceHeight,target.tile.transform.position.z);
        var axis=new Vector3(direction.x,0,direction.y);
        board.player.localScale=Vector3.zero;
        cargo.transform.position=hole;
        var owner=target.tile.GetComponentInParent<IslandSurfaceAnchor>();var binding=cargo.GetComponent<IslandSurfaceAnchor>();
        if(binding&&owner){binding.center=owner.center;binding.Apply();}
        try{
            cargo.gameObject.SetActive(true);
            var hit=obstacle?MagnetContact(target,obstacle,cargo,direction):StoneContact(target,stone,cargo,direction);
            if(cargo.portalRollSpeed>0){
                yield return ThrowRollingRebound(cargo,hole,hit,pose,scale,direction,false);
                if(obstacle)yield return board.TransferPortalRingMomentum(cargo,obstacle,direction,cargo.portalRollSpeed);
                yield return ThrowRollingRebound(cargo,hit,hole,cargo.Pose,scale,-direction,true);
            }else{
                yield return ThrowCargo(cargo,hole,hit,pose,scale,.28f,.24f,landingSound:false);
                if(obstacle)yield return board.TransferPortalRingMomentum(cargo,obstacle,direction,board.cellSize/.55f);
                yield return ThrowCargo(cargo,hit,hole,pose,scale,.35f,.3f,landingSound:false);
            }
        }finally{
            cargo.gameObject.SetActive(false);cargo.transform.position=hole;
            cargo.geometry.rotation=pose;cargo.geometry.localScale=scale;MagnetVisuals.Ground(cargo);
            cargo.storedInPortal=target;cargo.portalEntryPosition=hole+axis*board.cellSize;
        }
    }
    IEnumerator ThrowRollingRebound(MagnetPiece ring,Vector3 start,Vector3 end,Quaternion pose,Vector3 scale,Vector2Int direction,bool entering){
        float distance=Vector3.Distance(start,end);
        float seconds=Mathf.Max(.18f,distance/Mathf.Max(.01f,ring.portalRollSpeed));
        var axis=new Vector3(direction.y,0,-direction.x);
        foreach(var renderer in ring.GetComponentsInChildren<Renderer>(true)){
            renderer.enabled=true;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
            block.SetFloat("_IslandWindowClipEnabled",0);renderer.SetPropertyBlock(block);
        }
        for(float elapsed=0;elapsed<seconds;){
            elapsed=Mathf.Min(seconds,elapsed+AnimationDeltaTime);float p=elapsed/seconds;
            ring.transform.position=Vector3.Lerp(start,end,p);
            ring.geometry.rotation=Quaternion.AngleAxis(180*distance*p/ring.storedInPortal.board.cellSize,axis)*pose;
            ring.geometry.localScale=scale*(entering?Mathf.SmoothStep(1,0,Mathf.InverseLerp(.6f,1,p)):Mathf.SmoothStep(0,1,Mathf.Clamp01(p/.35f)));
            MagnetVisuals.Ground(ring);ring.geometry.position+=Vector3.up*(.8f*p*(1-p));
            if(elapsed<seconds)yield return null;
        }
    }
}
}
