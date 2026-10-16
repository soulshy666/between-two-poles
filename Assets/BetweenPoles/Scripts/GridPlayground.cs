using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BetweenPoles {
public sealed partial class GridPlayground:MonoBehaviour {
    public float cellSize=1.5f;
    public float LayerHeight {get{return cellSize*.5f;}}
    public float stepSeconds=.22f;
    [Range(.12f,.3f)] public float walkSeconds=.25f;
    [Tooltip("Target catch-up time for fresh walking taps. Speed eases toward the target; held movement returns to normal pace.")]
    [Range(.04f,.15f)] public float walkTapCatchupSeconds=.07f;
    [Tooltip("Maximum walking speed relative to normal walking, including rapid taps and turns.")]
    [Range(1f,1.5f)] public float maxWalkSpeedMultiplier=1.5f;
    [Tooltip("Maximum push acceleration relative to each magnet's normal timing.")]
    [Range(1f,1.5f)] public float maxPushSpeedMultiplier=1.5f;
    float WalkSpeedLimit => cellSize/Mathf.Max(.01f,walkSeconds)*Mathf.Clamp(maxWalkSpeedMultiplier,1f,1.5f);
    [Tooltip("Turning duration. Ordinary walking turns while moving, without a startup pause.")]
    [Range(.04f,.16f)] public float turnSeconds=.05f;
    [Tooltip("Short landing pause between automatically repeated walking steps; fresh key presses bypass it.")]
    [Range(0f,.1f)] public float heldStepPauseSeconds=0f;
    [Range(.3f,.8f)] public float barPushSeconds=.46f;
    [Tooltip("Tap catch-up target. Ordinary rolls ease toward a bounded speed; special interactions compress their remaining playback.")]
    [Range(.06f,.18f)] public float pushTapCatchupSeconds=.10f;
    [Range(.2f,2f)] public float uRollSeconds=.7f;
    [Range(.6f,2.5f)] public float joinSeconds=1.35f;
    [Tooltip("Duration of one cross-magnet hand sweep and clockwise quarter-turn.")]
    [Range(.3f,1.5f)] public float crossTurnSeconds=.75f;
    [Tooltip("Opposite-pole assembly playback speed; also drives the player's matching push pose.")]
    [Range(1f,2f)] public float attractionAnimationSpeed=1.25f;
    public Transform player;
    public Transform playerVisual;
    public GridTile[] tiles;
    public MagnetPiece[] magnets;
    public Renderer goalLight;
    public Material goalCompleteMaterial;
    public bool ReachedGoal { get; private set; }
    bool movementBusy,collectingStar;
    public bool Busy { get{return movementBusy||collectingStar||GhostMode;} private set{movementBusy=value;} }
    public event System.Action<GridTile> Landed;
    struct BufferedStep { public Vector2Int direction; public float time; }
    readonly Queue<BufferedStep> bufferedSteps=new Queue<BufferedStep>();
    const float InputBufferSeconds=.6f;
    Vector2Int heldDirection;
    MobileDirectionPad mobilePad;
    float nextHeldStep;
    bool reversibleWalk;
    bool completingWalkForTurn;
    bool curvedWalkRequested;
    Vector2Int walkDirection;
    float walkTapSpeed;
    float walkCurrentSpeed,walkTapTime;
    Vector3 walkOrigin,walkDestination;
    bool interruptiblePush,pushFeedbackOnly;
    bool rollingRingShove;
    Vector2Int pushDirection;
    WorldState pushUndo;
    bool flowingPush,continuePushOnComplete;
    float pushFlowProgress,pushFlowSpeed,pushFlowBoost;
    float pushFlowTapTime;
    float pushFlowCarryTime=-1;
    readonly Stack<IEnumerator> movementStack=new Stack<IEnumerator>();
    Coroutine movementPlayback;
    bool finishingMovement;
    bool deferPushLanding;
    bool exitingPush;
    Vector2Int pushExitDirection;
    Vector2Int? deferredPushLanding;
    float MovementDeltaTime {get{return recordingPush?PushSampleSeconds:finishingMovement?10f:Time.deltaTime;}}
    void StartMovement(IEnumerator action){
        movementStack.Push(action);movementPlayback=StartCoroutine(PlayMovement());
    }
    bool AdvanceMovement(){
        while(movementStack.Count>0){
            var action=movementStack.Peek();
            if(!action.MoveNext()){movementStack.Pop();(action as System.IDisposable)?.Dispose();continue;}
            var child=action.Current as IEnumerator;
            if(child!=null){movementStack.Push(child);continue;}
            return true;
        }
        return false;
    }
    IEnumerator PlayMovement(){
        while(AdvanceMovement()){
            if(pendingRingRelease!=null){
                var flight=pendingRingRelease;pendingRingRelease=null;
                var remaining=new Stack<IEnumerator>(movementStack.ToArray());
                // Stack.ToArray is top-first; preserve the original coroutine nesting.
                remaining=new Stack<IEnumerator>(remaining);
                movementStack.Clear();movementPlayback=null;
                flight.playback=StartCoroutine(ContinueLiveRing(flight,remaining));yield break;
            }
            yield return null;
        }
        pendingRingRelease=null;movementPlayback=null;
        // Finish the transaction before starting the next one, but do not spend
        // a rendered frame idle between two ordinary pushes.
        if(continuePushOnComplete){
            continuePushOnComplete=false;
            while(bufferedSteps.Count>0&&Time.unscaledTime-bufferedSteps.Peek().time>InputBufferSeconds)bufferedSteps.Dequeue();
            var direction=bufferedSteps.Count>0?bufferedSteps.Dequeue().direction:
                DirectionHeld(heldDirection)?heldDirection:Vector2Int.zero;
            if(direction!=Vector2Int.zero){
                if(TryStep(direction)){if(flowingPush)yield break;}
                else bufferedSteps.Clear();
            }
            pushFlowSpeed=pushFlowBoost=0;pushFlowCarryTime=-1;
        }
    }
    void FinishMovement(){
        if(movementPlayback!=null)StopCoroutine(movementPlayback);
        movementPlayback=null;
        // Execute the existing finalization paths, including recipe changes and
        // landing events, rather than restoring the pre-push snapshot.
        finishingMovement=true;
        try{while(AdvanceMovement()){}}
        finally{finishingMovement=false;}
    }
    struct Snapshot { public float portalRollSpeed; public BlackHolePortal storedInPortal; public Transform parent,owner; public Vector3 position,scale,portalEntryPosition; public Quaternion rotation; public bool combined,walkable,enabled,active,north; public Transform geometry; public Quaternion pose; public Vector3 geoPosition; public MagnetShape shape; public MagnetProduct product; public bool baseNorth; public Vector2Int bridgeDirection; }
    Vector2Int Cell(Transform t){return new Vector2Int(Mathf.RoundToInt(t.position.x/cellSize),Mathf.RoundToInt(t.position.z/cellSize));}
    readonly System.Collections.Generic.Dictionary<Vector2Int,GridTile> frameTiles=new System.Collections.Generic.Dictionary<Vector2Int,GridTile>();
    int tileFrame=-1;GridTile[] indexedTiles;Vector3 indexedBoardPosition;float indexedCellSize;
    public void InvalidateTileLookup(){tileFrame=-1;}
    GridTile Tile(Vector2Int p){
        // Rebuild at most once per frame, preserving the authored first tile at each cell.
        // Array replacement and chapter translation invalidate even in the same frame.
        if(!Application.isPlaying||tileFrame!=Time.frameCount||indexedTiles!=tiles||indexedBoardPosition!=transform.position||indexedCellSize!=cellSize){
            frameTiles.Clear();foreach(var t in tiles)if(t){var key=Cell(t.transform);if(!frameTiles.ContainsKey(key))frameTiles.Add(key,t);}
            tileFrame=Time.frameCount;indexedTiles=tiles;indexedBoardPosition=transform.position;indexedCellSize=cellSize;
        }
        return frameTiles.TryGetValue(p,out var found)&&found?found:null;
    }
    MagnetPiece Piece(Vector2Int p,MagnetPiece ignore=null){
        MagnetPiece result=null;
        foreach(var m in magnets)if(m&&m.enabled&&m!=ignore&&!IsKnockedFlying(m)&&m.gameObject.activeInHierarchy&&m.Occupies(p,cellSize))
            if(!result||m.transform.position.y>result.transform.position.y+.01f
                ||(SameHeight(m.transform.position.y,result.transform.position.y)&&result.walkable&&!m.walkable))result=m;
        return result;
    }
    Vector3 Position(Vector2Int p,float y=0){return new Vector3(p.x*cellSize,y,p.y*cellSize);}
    bool Floor(Vector2Int p){var t=Tile(p);return t&&!t.blocked;}
    void Awake(){
        if(ChapterAtlas.IsInspectionScene(gameObject.scene)){enabled=false;return;}
        SharedPlayer.Prepare(this);
        mobilePad=GetComponent<MobileDirectionPad>();
        if(!mobilePad&&(Application.isMobilePlatform||Application.isEditor))mobilePad=gameObject.AddComponent<MobileDirectionPad>();
        if(player)PlayerPushPose.EnsureIdle(player);
        CaptureInitialState();InitializeDiskReferences();
    }
    public void CaptureInitialState(){
        InvalidateTileLookup();
        IslandEdgeDressing.Refresh(this);
        IceRockVisuals.Apply(this);
        recoilArrivals.Clear();
        ClearKnockFlights();knockCheckpoints.Clear();
        ClearMovementInput();
        foreach(var m in magnets)if(m&&m.geometry&&!m.combined&&m.product==MagnetProduct.None&&m.shape==MagnetShape.Horseshoe&&!MagnetPiece.FlatU(m.Pose)){
            m.geometry.rotation=MagnetPiece.FlatUPose(m.Pose);MagnetVisuals.Ground(m);
        }
        currentIsland=Owner(Tile(Cell(player)));CapturePlayerIslandEntries();
        history.Clear();initialState=SaveWorld();ReleaseUnusedGeometry();
    }
    public bool JumpTestRoom(int direction){
        if(OpeningCinematic)return false;
        if(Busy||ChapterRecoilTravel.Active||BlackHoleTravel.InTransit||BlackHoleTravel.Selecting)return false;
        if(direction!=1&&direction!=-1)return false;
        IslandCamera view=null;
        foreach(var candidate in FindObjectsOfType<IslandCamera>())if(candidate.enabled&&candidate.board==this){view=candidate;break;}
        if(!view||view.islandCenters==null)return Reject("当前场景没有可切换的房间");
        var centers=new List<Transform>();
        foreach(var center in view.islandCenters)if(center&&center.gameObject.activeInHierarchy&&!centers.Contains(center))centers.Add(center);
        var roomNumbers=new Dictionary<Transform,int>();
        foreach(var window in FindObjectsOfType<FiveIslandWindow>())if(window.board==this&&window.rooms!=null){
            var usedNumbers=new HashSet<int>();bool validNumbers=true;
            foreach(var room in window.rooms)if(room.center&&centers.Contains(room.center)){
                if(room.roomNumber<1||!usedNumbers.Add(room.roomNumber)){validNumbers=false;break;}
                roomNumbers[room.center]=room.roomNumber;
            }
            // Legacy or partially numbered scenes retain their authored order.
            if(!validNumbers||roomNumbers.Count!=centers.Count)roomNumbers.Clear();
            break;
        }
        if(roomNumbers.Count==centers.Count&&centers.Count>0)centers.Sort((a,b)=>roomNumbers[a].CompareTo(roomNumbers[b]));
        int current=centers.IndexOf(currentIsland);if(current<0)current=centers.IndexOf(Owner(Tile(Cell(player))));
        int next=current+direction;
        if(current<0||next<0||next>=centers.Count)return Reject(direction>0?"已经是最后一个房间":"已经是第一个房间");
        GridTile landing=null;float closest=float.PositiveInfinity;
        foreach(var tile in tiles){
            if(!tile||!tile.gameObject.activeInHierarchy||tile.blocked||tile.goal||Owner(tile)!=centers[next]||Piece(Cell(tile.transform))||tile.GetComponentInChildren<BlackHolePortal>(true))continue;
            float distance=(tile.transform.position-centers[next].position).sqrMagnitude;
            if(distance<closest){closest=distance;landing=tile;}
        }
        if(!landing)return Reject("目标房间没有安全的空地");
        ClearMovementInput();history.Clear();ReleaseUnusedGeometry();
        player.position=Position(Cell(landing.transform),landing.surfaceHeight);
        NotifyLanding(Cell(landing.transform));
        var anchor=player.GetComponent<IslandSurfaceAnchor>();if(anchor){anchor.center=centers[next];anchor.Apply();}
        int targetNumber=roomNumbers.TryGetValue(centers[next],out var number)?number:next+1;
        LastRule="测试跳转：房间编号 "+targetNumber+"（"+(next+1)+" / "+centers.Count+"）";return true;
    }
    void Update(){
        if(GhostMode){TickGhostMode();return;}
        if(Input.GetKeyDown(KeyCode.Alpha4)||Input.GetKeyDown(KeyCode.Keypad4)){ToggleGhostMode();return;}
        if(ChapterRecoilTravel.Active||ChapterRecoilTravel.UndoInputFrame==Time.frameCount){ClearMovementInput();return;}
        if(BlackHoleTravel.SkipTransitionActive){ClearMovementInput();return;}
        if(player&&(Input.anyKey||Input.mouseScrollDelta.sqrMagnitude>0
            ||Mathf.Abs(Input.GetAxisRaw("Mouse X"))+Mathf.Abs(Input.GetAxisRaw("Mouse Y"))>.001f)){
            var pose=player.GetComponent<PlayerPushPose>();if(pose)pose.CancelIdle();
        }
        if(GameSaveSlots.Restoring||OpeningCinematic||collectingStar){ClearMovementInput();return;}
        var panel=GetComponent<MagnetDebugPanel>();
        if((panel&&panel.enabled&&panel.IsOpen)||Time.timeScale<=0){ClearMovementInput();return;}
        if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1)){JumpTestRoom(1);return;}
        if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Keypad2)){JumpTestRoom(-1);return;}
        if(Input.GetKeyDown(KeyCode.Z)){ClearMovementInput();UndoStep();return;}
        if(Input.GetKeyDown(KeyCode.R)){ClearMovementInput();ResetCurrentIsland();return;}
        Vector2Int direction=Vector2Int.zero;
        if(Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.RightArrow))direction=Vector2Int.right;
        else if(Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.LeftArrow))direction=Vector2Int.left;
        else if(Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow))direction=Vector2Int.up;
        else if(Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow))direction=Vector2Int.down;
        if(mobilePad&&mobilePad.Pressed!=Vector2Int.zero)direction=mobilePad.Pressed;
        if(direction!=Vector2Int.zero){heldDirection=direction;nextHeldStep=Time.unscaledTime+walkSeconds;}
        else if(heldDirection!=Vector2Int.zero&&!DirectionHeld(heldDirection)){
            // The most recently pressed direction wins; releasing it resumes a
            // direction that is still held, including an immediate reversal.
            heldDirection=Vector2Int.zero;
            foreach(var candidate in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
                if(DirectionHeld(candidate)){heldDirection=candidate;break;}
            direction=heldDirection;nextHeldStep=Time.unscaledTime+walkSeconds;
        }
        bool hadBufferedInput=bufferedSteps.Count>0;
        PumpMovementInput(direction,DirectionPressed(direction));
        // Holding does not enqueue extra steps: release stops repeats immediately,
        // while the current step and explicitly buffered taps may finish normally.
        if(direction==Vector2Int.zero&&!hadBufferedInput&&!Busy&&heldDirection!=Vector2Int.zero&&Time.unscaledTime>=nextHeldStep){
            TryStep(heldDirection);nextHeldStep=Mathf.Max(nextHeldStep,Time.unscaledTime+.1f);
        }
    }
    bool DirectionHeld(Vector2Int direction){
        if(direction!=Vector2Int.zero&&mobilePad&&mobilePad.Held==direction)return true;
        if(direction==Vector2Int.right)return Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow);
        if(direction==Vector2Int.left)return Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow);
        if(direction==Vector2Int.up)return Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow);
        if(direction==Vector2Int.down)return Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow);
        return false;
    }
    bool DirectionPressed(Vector2Int direction){
        if(direction!=Vector2Int.zero&&mobilePad&&mobilePad.Pressed==direction)return true;
        if(direction==Vector2Int.right)return Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.RightArrow);
        if(direction==Vector2Int.left)return Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.LeftArrow);
        if(direction==Vector2Int.up)return Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow);
        if(direction==Vector2Int.down)return Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow);
        return false;
    }
    void ClearMovementInput(){
        exitingPush=false;
        curvedWalkRequested=false;
        walkTapSpeed=0;
        pushFlowBoost=0;
        bufferedSteps.Clear();heldDirection=Vector2Int.zero;nextHeldStep=0;
        completingWalkForTurn=false;
        var pose=player?player.GetComponent<PlayerPushPose>():null;if(pose){pose.CancelIdle();pose.EndEdgeBalance();pose.EndStonePush();}
    }
    void PauseHeldWalkRepeat(){
        // Gate only automatic repeats, never Busy or the magnet animation clock.
        // New key-downs still go straight through PumpMovementInput / TryStep.
        if(heldDirection!=Vector2Int.zero&&!finishingMovement&&!recordingPush)
            nextHeldStep=Mathf.Max(nextHeldStep,Time.unscaledTime+Mathf.Clamp(heldStepPauseSeconds,0,.1f));
    }
    void PumpMovementInput(Vector2Int direction,bool freshPress=false){
        if(responsiveJump&&freshPress&&direction!=Vector2Int.zero)jumpTapBoost=true;
        if(exitingPush&&direction!=Vector2Int.zero){
            pushExitDirection=direction;
            bufferedSteps.Clear();
            bufferedSteps.Enqueue(new BufferedStep{direction=direction,time=Time.unscaledTime});
        }
        // Ordinary rolls keep their live trajectory. A tap changes its speed,
        // not its pose or coroutine, and the buffered step joins at the boundary.
        if(flowingPush&&freshPress&&direction==pushDirection&&bufferedSteps.Count<2){
            pushFlowBoost=Mathf.Max(pushFlowBoost,(1-pushFlowProgress)/Mathf.Clamp(pushTapCatchupSeconds,.06f,.18f));
            pushFlowTapTime=Time.unscaledTime;
        }
        if(CanInterruptPush(direction,freshPress)){InterruptPush(direction);return;}
        // A fresh tap advances the current stride faster and queues exactly one next step.
        // Keep traversing the validated segment; never teleport across a cell boundary.
        if(freshPress&&reversibleWalk&&!completingWalkForTurn&&direction==walkDirection&&bufferedSteps.Count<2){
            walkTapSpeed=Mathf.Max(walkTapSpeed,Vector3.Distance(player.position,walkDestination)/Mathf.Clamp(walkTapCatchupSeconds,.04f,.15f));
            walkTapTime=Time.unscaledTime;
        }
        if(TryReverseWalk(direction)){curvedWalkRequested=freshPress&&completingWalkForTurn;return;}
        while(bufferedSteps.Count>0&&Time.unscaledTime-bufferedSteps.Peek().time>InputBufferSeconds)bufferedSteps.Dequeue();
        // Direction changes replace stale intent instead of waiting behind it.
        // Repeated taps in the same direction still retain the existing two-step buffer.
        if(direction!=Vector2Int.zero&&bufferedSteps.Count>0&&bufferedSteps.Peek().direction!=direction)
            bufferedSteps.Clear();
        if(direction!=Vector2Int.zero&&bufferedSteps.Count<2)
            bufferedSteps.Enqueue(new BufferedStep{direction=direction,time=Time.unscaledTime});
        if(!Busy&&bufferedSteps.Count>0&&!TryStep(bufferedSteps.Dequeue().direction))bufferedSteps.Clear();
    }
    void OnDisable(){CancelGhostMode();ClearBridgeJunctionLinks();if(!portalMagnetPresentation)ClearMagnetHover();ClearMovementInput();ClearPushPresentations();}
    void OnApplicationFocus(bool focused){if(!focused)ClearMovementInput();}
    // Non-zero initial speed makes the first frame responsive; the end settles softly.
    static float MovementProgress(float progress){float p=Mathf.Clamp01(progress);return p+p*p-p*p*p;}
    IEnumerator TurnPlayer(Vector3 direction){
        direction.y=0;if(direction.sqrMagnitude<.0001f)yield break;
        var start=player.rotation;var end=Quaternion.LookRotation(direction);
        float angle=Quaternion.Angle(start,end);
        if(angle<.1f){player.rotation=end;yield break;}
        float seconds=Mathf.Max(.01f,turnSeconds)*Mathf.Lerp(.65f,1.35f,angle/180f);
        // Keep the feet planted until facing the next step. Use the movement
        // clock so undo and immediate movement finalization still work.
        for(float elapsed=0;elapsed<seconds;elapsed+=MovementDeltaTime){
            player.rotation=Quaternion.Slerp(start,end,Mathf.SmoothStep(0,1,elapsed/seconds));
            yield return null;
        }
        player.rotation=end;
    }
    IEnumerator Slide(Transform target,Vector3 end,float seconds){
        Vector3 start=target.position;float time=0;
        while(time<seconds){Vector3 previous=target.position;time+=MovementDeltaTime;float a=target==player?MovementProgress(time/seconds):Mathf.SmoothStep(0,1,time/seconds);target.position=Vector3.Lerp(start,end,a);if(target==player){var pose=player.GetComponent<PlayerPushPose>();if(pose)pose.AdvanceWalk(Vector3.Distance(previous,target.position));}yield return null;}
        target.position=end;
    }
    float Height(Vector2Int p){
        var m=Deck(p);if(m)return DeckHeight(m);
        var t=Tile(p);return t?t.surfaceHeight:0;
    }
    IEnumerator Walk(Vector2Int p){
        var pose=PlayerPushPose.BeginWalk(player,cellSize);
        try{yield return WalkSupported(p);}finally{pose.End();}
    }
    bool TryReverseWalk(Vector2Int direction){
        if(!reversibleWalk||Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1
            ||(direction==walkDirection&&!completingWalkForTurn))return false;
        if(direction==-walkDirection){
            walkTapSpeed=0;
            walkCurrentSpeed=cellSize/Mathf.Max(.01f,walkSeconds);
            // Retrace only the distance already travelled, even before the midpoint.
            // Swapping endpoints also allows another reversal during the return.
            var previousOrigin=walkOrigin;walkOrigin=walkDestination;walkDestination=previousOrigin;
            walkDirection=direction;completingWalkForTurn=false;
            bufferedSteps.Clear();
            FaceWalkDirection(direction);
            return true;
        }
        if(direction==walkDirection){completingWalkForTurn=false;bufferedSteps.Clear();return true;}
        // A fresh keyboard turn may round this corner on clear floor. Otherwise
        // finish the validated axis before starting the queued perpendicular step.
        completingWalkForTurn=true;
        walkTapSpeed=0;
        bufferedSteps.Clear();
        bufferedSteps.Enqueue(new BufferedStep{direction=direction,time=Time.unscaledTime});
        FaceWalkDirection(direction);
        return true;
    }
    void FaceWalkDirection(Vector2Int direction){
        var facing=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        player.rotation=Quaternion.RotateTowards(player.rotation,facing,180f/Mathf.Max(.085f,turnSeconds)*MovementDeltaTime);
    }
    bool PlainWalkCell(Vector2Int cell,float height,Transform owner){
        var tile=Tile(cell);
        return tile&&!tile.blocked&&!tile.goal&&SameHeight(tile.surfaceHeight,height)&&Owner(tile)==owner
            &&!Piece(cell)&&!IsWreckCell(cell)&&!BlackHolePortal.At(this,cell)&&!FindCollectibleStar(cell);
    }
    bool ContinuePlainWalk(){
        while(bufferedSteps.Count>0&&Time.unscaledTime-bufferedSteps.Peek().time>InputBufferSeconds)bufferedSteps.Dequeue();
        bool buffered=bufferedSteps.Count>0;
        // Held movement resumes through Update after the landing pause.
        // Explicit taps may still continue immediately at the cell boundary.
        if(!buffered&&heldStepPauseSeconds>0)return false;
        var direction=buffered?bufferedSteps.Peek().direction:heldDirection;
        if(direction==Vector2Int.zero||(!buffered&&!DirectionHeld(direction))||finishingMovement)return false;
        var from=Cell(player);var next=from+direction;
        if(!PlainWalkCell(from,player.position.y,Owner(Tile(from)))
            ||!PlainWalkCell(next,player.position.y,Owner(Tile(from))))return false;
        if(buffered)bufferedSteps.Dequeue();
        RecordHistory(SaveWorld());
        if(direction!=walkDirection||Time.unscaledTime-walkTapTime>.24f)walkTapSpeed=0;
        walkOrigin=player.position;walkDestination=Position(next,Height(next));walkDirection=direction;
        completingWalkForTurn=false;return true;
    }
    void ClearWalkInterrupt(){reversibleWalk=false;completingWalkForTurn=false;curvedWalkRequested=false;walkTapSpeed=0;}
    void BeginPushInterrupt(Vector2Int direction,bool feedbackOnly=false,bool playPushSound=true){pushHoverParticipants.Clear();if(!feedbackOnly&&playPushSound)PlayMagnetPushSound();interruptiblePush=true;pushFeedbackOnly=feedbackOnly;pushDirection=direction;pushUndo=null;}
    void EndPushInterrupt(){pushHoverParticipants.Clear();interruptiblePush=false;pushFeedbackOnly=false;pushUndo=null;flowingPush=false;rollingRingShove=false;}
    bool CanInterruptPush(Vector2Int direction,bool freshPress=false){
        // A rolling ring releases the player through its live handoff. Recording it
        // on a rapid tap would instead wait for the entire remaining roll playback.
        return !rollingRingShove&&Busy&&interruptiblePush&&(pushUndo!=null||pushFeedbackOnly)
            &&(direction!=pushDirection||(freshPress&&!pushFeedbackOnly&&!flowingPush))&&Mathf.Abs(direction.x)+Mathf.Abs(direction.y)==1;
    }
    bool InterruptPush(Vector2Int direction){
        bufferedSteps.Clear();
        var start=player.position;var facing=player.rotation;
        bool continuePushing=direction==pushDirection&&!pushFeedbackOnly;
        var currentPose=player.GetComponent<PlayerPushPose>();
        var release=!continuePushing&&currentPose?currentPose.CapturePushRelease():null;
        // Recording must not bake a tap boost into an already accelerated replay.
        if(flowingPush){pushFlowBoost=0;pushFlowSpeed=0;}
        deferredPushLanding=null;deferPushLanding=true;
        try{FinishPushWithPresentation(Mathf.Clamp(pushTapCatchupSeconds,.06f,.18f));}finally{deferPushLanding=false;}
        pushFlowSpeed=pushFlowBoost=0;pushFlowCarryTime=-1;
        var end=player.position;var landing=deferredPushLanding;deferredPushLanding=null;
        // The push is now committed. Start a legal walking step immediately,
        // while its visible path blends from the pre-commit player position.
        if(!continuePushing&&CanBlendPushExit(start,end,direction)){
            if(landing.HasValue)NotifyLanding(landing.Value);
            var beforeWalk=SaveWorld();
            var destination=CellAt(end)+direction;
            RecordHistory(beforeWalk);
            player.SetPositionAndRotation(start,facing);
            exitingPush=true;pushExitDirection=direction;
            Busy=true;StartMovement(BlendPushExit(end,Position(destination,end.y),destination,release));
            return true;
        }
        player.SetPositionAndRotation(start,facing);
        bufferedSteps.Enqueue(new BufferedStep{direction=direction,time=Time.unscaledTime});
        exitingPush=!continuePushing;pushExitDirection=direction;
        Busy=true;StartMovement(CompleteInterruptedPush(end,landing,continuePushing,release));
        return true;
    }
    bool CanBlendPushExit(Vector3 visible,Vector3 committed,Vector2Int direction){
        var incoming=committed-visible;
        if(incoming.sqrMagnitude<.000001f||Mathf.Abs(Vector3.Dot(incoming.normalized,new Vector3(direction.x,0,direction.y)))>.01f)return false;
        var from=CellAt(committed);var owner=Owner(Tile(from));
        if(!SameHeight(visible.y,committed.y)||!PlainWalkCell(from,committed.y,owner)
            ||!PlainWalkCell(from+direction,committed.y,owner))return false;
        var destination=Position(from+direction,committed.y);
        // Conservatively require the entire swept rectangle, including the
        // character's width, to be plain floor. Near hazards use the grid corner.
        float margin=cellSize*.18f;
        int minX=Mathf.FloorToInt((Mathf.Min(visible.x,destination.x)-margin)/cellSize+.5f);
        int maxX=Mathf.FloorToInt((Mathf.Max(visible.x,destination.x)+margin)/cellSize+.5f);
        int minZ=Mathf.FloorToInt((Mathf.Min(visible.z,destination.z)-margin)/cellSize+.5f);
        int maxZ=Mathf.FloorToInt((Mathf.Max(visible.z,destination.z)+margin)/cellSize+.5f);
        for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
            if(!PlainWalkCell(new Vector2Int(x,z),committed.y,owner))return false;
        return true;
    }
    static Vector3 RoundedTurnPoint(Vector3 start,Vector3 corner,Vector3 end,float t){
        // Quadratic curve: enters along the old axis and leaves along the new.
        return Vector3.Lerp(Vector3.Lerp(start,corner,t),Vector3.Lerp(corner,end,t),t);
    }
    IEnumerator BlendPushExit(Vector3 corner,Vector3 destination,Vector2Int landing,System.Action<float> release,PlayerPushPose walkingPose=null){
        var pose=walkingPose?walkingPose:PlayerPushPose.BeginWalk(player,cellSize);
        var start=player.position;
        const int samples=64;
        var path=new Vector3[samples+1];path[0]=start;
        for(int i=1;i<=samples;i++)path[i]=RoundedTurnPoint(start,corner,destination,(float)i/samples);
        int segment=1;
        float elapsed=0;
        try{
            while(segment<=samples){
                float dt=MovementDeltaTime;elapsed+=dt;
                var previous=player.position;
                float budget=WalkSpeedLimit*dt,travelled=0;
                while(segment<=samples&&budget>0){
                    float remaining=Vector3.Distance(player.position,path[segment]);
                    float step=Mathf.Min(remaining,budget);
                    player.position=Vector3.MoveTowards(player.position,path[segment],step);
                    budget-=step;travelled+=step;
                    if(remaining<=step+.000001f)segment++;else break;
                }
                var tangent=player.position-previous;tangent.y=0;
                if(tangent.sqrMagnitude>.000001f)player.rotation=Quaternion.RotateTowards(player.rotation,Quaternion.LookRotation(tangent),180f/Mathf.Max(.085f,turnSeconds)*dt);
                pose.AdvanceWalk(travelled);
                release?.Invoke(Mathf.Clamp01(elapsed/.08f));
                yield return null;
            }
            player.position=destination;
        }finally{if(pose)pose.End();}
        exitingPush=false;NotifyLanding(landing);Busy=false;
        continuePushOnComplete=!finishingMovement&&!recordingPush;
    }
    IEnumerator CompleteInterruptedPush(Vector3 end,Vector2Int? landing,bool continuePushing,System.Action<float> release){
        var pose=continuePushing?PlayerPushPose.Begin(player):PlayerPushPose.BeginWalk(player,cellSize);
        var start=player.position;
        float seconds=Mathf.Clamp(pushTapCatchupSeconds,.06f,.18f);
        // MovementProgress has a peak derivative of 4/3. Bound peak, not average speed.
        float limit=continuePushing?cellSize/Mathf.Max(.01f,barPushSeconds)*Mathf.Clamp(maxPushSpeedMultiplier,1f,1.5f):WalkSpeedLimit;
        seconds=Mathf.Max(seconds,Vector3.Distance(start,end)*4f/(3f*limit));
        try{
            float elapsed=0;
            while(elapsed<seconds){
                elapsed+=MovementDeltaTime;
                float p=Mathf.Clamp01(elapsed/seconds);
                var previous=player.position;player.position=Vector3.Lerp(start,end,MovementProgress(p));
                if(!continuePushing)FaceWalkDirection(pushExitDirection);
                if(continuePushing)pose.Sample(.25f+p*.5f);else pose.AdvanceWalk(Vector3.Distance(previous,player.position));
                if(!continuePushing)release?.Invoke(Mathf.Clamp01(elapsed/.08f));
                yield return null;
            }
            player.position=end;
            // Release input only after the render tracks reach the same committed state.
            // The next push can then operate on the same piece without a visual jump.
            if(continuePushing)while(pushPresentations.Count>0)yield return null;
        }finally{if(pose)pose.End();}
        exitingPush=false;
        if(landing.HasValue)NotifyLanding(landing.Value);Busy=false;
        if(!continuePushing&&!finishingMovement&&!recordingPush){
            // Let the movement driver start the queued direction this frame.
            continuePushOnComplete=true;
        }
    }
    IEnumerator MovePlayer(Vector2Int p){
        Busy=true;
        var star=FindCollectibleStar(p);
        if(star){
            collectingStar=true;bufferedSteps.Clear();
            yield return TurnPlayer(Position(p,Height(p))-player.position);
            yield return CollectStar(star,Position(p,Height(p)));
            NotifyLanding(p);Busy=false;yield break;
        }
        var from=Cell(player);
        // Ordinary ground walking can reverse anywhere along its validated segment.
        // Supported paths and magnet transactions keep their existing landing rules.
        if(Floor(from)&&Floor(p)&&!Piece(from)&&!Piece(p)){
            var walkPose=PlayerPushPose.BeginWalk(player,cellSize);
            reversibleWalk=true;
            walkTapSpeed=0;
            walkCurrentSpeed=cellSize/Mathf.Max(.01f,walkSeconds);
            walkOrigin=player.position;walkDestination=Position(p,Height(p));walkDirection=p-from;
            while(true){
                if(curvedWalkRequested&&completingWalkForTurn&&bufferedSteps.Count>0){
                    var direction=bufferedSteps.Peek().direction;
                    if(CanBlendPushExit(player.position,walkDestination,direction)){
                        var visible=player.position;var corner=walkDestination;
                        player.position=corner;NotifyLanding(CellAt(corner));RecordHistory(SaveWorld());
                        player.position=visible;bufferedSteps.Dequeue();ClearWalkInterrupt();
                        exitingPush=true;pushExitDirection=direction;
                        var target=CellAt(corner)+direction;
                        yield return BlendPushExit(corner,Position(target,corner.y),target,null,walkPose);
                        yield break;
                    }
                    curvedWalkRequested=false;
                }
                Vector3 end=walkDestination;
                // Carry speed across cells; fresh taps accelerate without a velocity jump.
                var previous=player.position;
                FaceWalkDirection(completingWalkForTurn&&bufferedSteps.Count>0?bufferedSteps.Peek().direction:walkDirection);
                float speed=cellSize/Mathf.Max(.01f,walkSeconds)*(completingWalkForTurn?1.6f:1f);
                if(Time.unscaledTime-walkTapTime>.24f)walkTapSpeed=0;
                speed=Mathf.Min(WalkSpeedLimit,Mathf.Max(speed,walkTapSpeed));
                walkCurrentSpeed=Mathf.Min(WalkSpeedLimit,Mathf.MoveTowards(walkCurrentSpeed,speed,cellSize/Mathf.Max(.01f,walkSeconds)*3*MovementDeltaTime/.07f));
                player.position=Vector3.MoveTowards(player.position,end,walkCurrentSpeed*MovementDeltaTime);
                walkPose.AdvanceWalk(Vector3.Distance(previous,player.position));
                if((player.position-end).sqrMagnitude<.000001f){
                    player.position=end;NotifyLanding(CellAt(end));
                    if(collectingStar||!ContinuePlainWalk())break;
                }
                yield return null;
            }
            ClearWalkInterrupt();
            walkPose.End();
            // A short reversed step must not wait for the original full-step deadline.
            nextHeldStep=Time.unscaledTime;
            PauseHeldWalkRepeat();
        }else {yield return Walk(p);PauseHeldWalkRepeat();}
        Busy=false;
    }
    public static void SampleJoin(Vector3 incomingStart,Vector3 targetStart,Vector3 incomingEnd,Vector3 targetEnd,Vector3 side,float progress,out Vector3 incoming,out Vector3 target){
        float separate=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.28f));
        float advance=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.28f)/.72f));
        Vector3 laneStart=incomingStart+side*Vector3.Dot(incomingEnd-targetStart,side);
        incoming=Vector3.Lerp(Vector3.Lerp(incomingStart,laneStart,separate),incomingEnd,advance);
        target=Vector3.Lerp(targetStart,targetEnd,separate);
    }
    // Full reset is retained for the independent test-panel reset button.
    public void ResetPuzzle(){
        if(initialState==null)return;
        RestoreWorld(initialState);history.Clear();ReleaseUnusedGeometry();LastRule="已恢复测试初始布局";
    }
}
}
