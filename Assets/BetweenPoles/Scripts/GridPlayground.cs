using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BetweenPoles {
public sealed partial class GridPlayground:MonoBehaviour {
    public float cellSize=1.5f;
    public float stepSeconds=.22f;
    [Range(.12f,.3f)] public float walkSeconds=.18f;
    [Range(.3f,.8f)] public float barPushSeconds=.46f;
    [Range(.2f,2f)] public float uRollSeconds=.7f;
    [Range(.6f,2.5f)] public float joinSeconds=1.35f;
    public Transform player;
    public Transform playerVisual;
    public GridTile[] tiles;
    public MagnetPiece[] magnets;
    public Renderer goalLight;
    public Material goalCompleteMaterial;
    public bool ReachedGoal { get; private set; }
    public bool Busy { get; private set; }
    public event System.Action<GridTile> Landed;
    struct BufferedStep { public Vector2Int direction; public float time; }
    readonly Queue<BufferedStep> bufferedSteps=new Queue<BufferedStep>();
    const float InputBufferSeconds=.6f;
    Vector2Int heldDirection;
    float nextHeldStep;
    bool reversibleWalk,walkReturning;
    Vector2Int walkDirection;
    Vector3 walkOrigin,walkDestination;
    int walkRevision;
    WorldState walkUndo;
    bool interruptiblePush;
    Vector2Int pushDirection;
    WorldState pushUndo;
    Vector3? interruptedWalkStart;
    struct Snapshot { public Transform parent,owner; public Vector3 position,scale; public Quaternion rotation; public bool combined,walkable,enabled,active,north; public Transform geometry; public Quaternion pose; public Vector3 geoPosition; public MagnetShape shape; public MagnetProduct product; public bool baseNorth; public Vector2Int bridgeDirection; }
    Vector2Int Cell(Transform t){return new Vector2Int(Mathf.RoundToInt(t.position.x/cellSize),Mathf.RoundToInt(t.position.z/cellSize));}
    GridTile Tile(Vector2Int p){foreach(var t in tiles)if(t&&Cell(t.transform)==p)return t;return null;}
    MagnetPiece Piece(Vector2Int p,MagnetPiece ignore=null){foreach(var m in magnets)if(m&&m.enabled&&m!=ignore&&m.gameObject.activeInHierarchy&&m.Occupies(p,cellSize))return m;return null;}
    Vector3 Position(Vector2Int p,float y=0){return new Vector3(p.x*cellSize,y,p.y*cellSize);}
    bool Floor(Vector2Int p){var t=Tile(p);return t&&!t.blocked;}
    void Awake(){
        CaptureInitialState();
    }
    public void CaptureInitialState(){
        ClearMovementInput();
        foreach(var m in magnets)if(m&&m.geometry&&!m.combined&&m.product==MagnetProduct.None&&m.shape==MagnetShape.Horseshoe&&!MagnetPiece.FlatU(m.Pose)){
            m.geometry.rotation=MagnetPiece.FlatUPose(m.Pose);MagnetVisuals.Ground(m);
        }
        currentIsland=Owner(Tile(Cell(player)));islandEntry=player.position;islandEntryRotation=player.rotation;
        history.Clear();initialState=SaveWorld();ReleaseUnusedGeometry();
    }
    public bool JumpTestRoom(int direction){
        if(Busy||ChapterRecoilTravel.Active||BlackHoleTravel.InTransit||BlackHoleTravel.Selecting)return false;
        if(direction!=1&&direction!=-1)return false;
        IslandCamera view=null;
        foreach(var candidate in FindObjectsOfType<IslandCamera>())if(candidate.enabled&&candidate.board==this){view=candidate;break;}
        if(!view||view.islandCenters==null)return Reject("当前场景没有可切换的房间");
        var centers=new List<Transform>();
        foreach(var center in view.islandCenters)if(center&&center.gameObject.activeInHierarchy&&!centers.Contains(center))centers.Add(center);
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
        LastRule="测试跳转：房间 "+(next+1)+" / "+centers.Count;return true;
    }
    void Update(){
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
        if(direction!=Vector2Int.zero){heldDirection=direction;nextHeldStep=Time.unscaledTime+.22f;}
        else if(heldDirection!=Vector2Int.zero&&!DirectionHeld(heldDirection)){
            // The most recently pressed direction wins; releasing it resumes a
            // direction that is still held, including an immediate reversal.
            heldDirection=Vector2Int.zero;
            foreach(var candidate in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
                if(DirectionHeld(candidate)){heldDirection=candidate;break;}
            direction=heldDirection;nextHeldStep=Time.unscaledTime+.22f;
        }
        bool hadBufferedInput=bufferedSteps.Count>0;
        PumpMovementInput(direction);
        // Holding does not enqueue extra steps: release stops repeats immediately,
        // while the current step and explicitly buffered taps may finish normally.
        if(direction==Vector2Int.zero&&!hadBufferedInput&&!Busy&&heldDirection!=Vector2Int.zero&&Time.unscaledTime>=nextHeldStep){
            TryStep(heldDirection);nextHeldStep=Time.unscaledTime+.1f;
        }
    }
    static bool DirectionHeld(Vector2Int direction){
        if(direction==Vector2Int.right)return Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow);
        if(direction==Vector2Int.left)return Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow);
        if(direction==Vector2Int.up)return Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow);
        if(direction==Vector2Int.down)return Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow);
        return false;
    }
    void ClearMovementInput(){bufferedSteps.Clear();heldDirection=Vector2Int.zero;nextHeldStep=0;}
    void PumpMovementInput(Vector2Int direction){
        if(CanInterruptPush(direction)){TryStep(direction);return;}
        if(TryReverseWalk(direction))return;
        while(bufferedSteps.Count>0&&Time.unscaledTime-bufferedSteps.Peek().time>InputBufferSeconds)bufferedSteps.Dequeue();
        // Remember brief repeated taps, without building a long automatic walking queue.
        if(direction!=Vector2Int.zero&&bufferedSteps.Count<2)
            bufferedSteps.Enqueue(new BufferedStep{direction=direction,time=Time.unscaledTime});
        if(!Busy&&bufferedSteps.Count>0&&!TryStep(bufferedSteps.Dequeue().direction))bufferedSteps.Clear();
    }
    void OnDisable(){ClearMovementInput();}
    void OnApplicationFocus(bool focused){if(!focused)ClearMovementInput();}
    // Non-zero initial speed makes the first frame responsive; the end settles softly.
    static float MovementProgress(float progress){float p=Mathf.Clamp01(progress);return p+p*p-p*p*p;}
    IEnumerator Slide(Transform target,Vector3 end,float seconds){
        Vector3 start=target.position;float time=0;
        while(time<seconds){time+=Time.deltaTime;float a=target==player?MovementProgress(time/seconds):Mathf.SmoothStep(0,1,time/seconds);target.position=Vector3.Lerp(start,end,a);yield return null;}
        target.position=end;
    }
    float Height(Vector2Int p){
        var m=Piece(p);
        if(m&&m.walkable){
            if(m.product==MagnetProduct.None)foreach(var bridges in FindObjectsOfType<PrejoinedTestBridges>()){
                float deck;if(bridges.board==this&&bridges.TryGetShoreLevel(m,out deck))return deck;
            }
            return m.transform.position.y+.24f;
        }
        var t=Tile(p);return t?t.surfaceHeight:0;
    }
    IEnumerator Walk(Vector2Int p){yield return WalkSupported(p);}
    bool TryReverseWalk(Vector2Int direction){
        if(!reversibleWalk||direction==Vector2Int.zero||direction!=(walkReturning?walkDirection:-walkDirection))return false;
        walkReturning=!walkReturning;walkRevision++;bufferedSteps.Clear();
        player.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
        return true;
    }
    void ClearWalkInterrupt(){reversibleWalk=false;walkUndo=null;}
    void BeginPushInterrupt(Vector2Int direction){interruptiblePush=true;pushDirection=direction;pushUndo=null;}
    void EndPushInterrupt(){interruptiblePush=false;pushUndo=null;}
    bool CanInterruptPush(Vector2Int direction){
        return Busy&&interruptiblePush&&pushUndo!=null&&direction!=pushDirection&&Mathf.Abs(direction.x)+Mathf.Abs(direction.y)==1;
    }
    bool InterruptPush(Vector2Int direction){
        var before=pushUndo;var visiblePosition=player.position;
        var held=heldDirection;float repeat=nextHeldStep;
        if(history.Count>0&&ReferenceEquals(history[history.Count-1],before))history.RemoveAt(history.Count-1);
        RestoreWorld(before);ReleaseUnusedGeometry();
        heldDirection=held;nextHeldStep=repeat;
        // Validate the new step from the last committed cell, not a rounded
        // mid-animation position. Ground walking keeps its visible starting point.
        interruptedWalkStart=visiblePosition;
        bool moved=TryStep(direction);
        interruptedWalkStart=null;
        if(!moved){
            player.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.y));
            Busy=true;StartCoroutine(ReturnFromCancelledPush(visiblePosition,before.playerPosition));
        }
        return true;
    }
    IEnumerator ReturnFromCancelledPush(Vector3 start,Vector3 end){
        player.position=start;yield return Slide(player,end,.12f);Busy=false;
    }
    IEnumerator MovePlayer(Vector2Int p){
        Busy=true;
        var from=Cell(player);
        // Only ordinary ground walking can reverse. Supported paths and magnet
        // transactions keep their existing atomic movement and landing rules.
        if(Floor(from)&&Floor(p)&&!Piece(from)&&!Piece(p)){
            reversibleWalk=true;walkReturning=false;walkRevision=0;walkUndo=null;
            walkOrigin=player.position;walkDestination=Position(p,Height(p));walkDirection=p-from;
            player.rotation=Quaternion.LookRotation(walkDestination-walkOrigin);
            Vector3 segmentStart=interruptedWalkStart??player.position;
            player.position=segmentStart;
            float elapsed=0;int revision=walkRevision;
            while(true){
                if(revision!=walkRevision){revision=walkRevision;segmentStart=player.position;elapsed=0;}
                Vector3 end=walkReturning?walkOrigin:walkDestination;
                float seconds=Mathf.Max(.001f,walkSeconds*Vector3.Distance(segmentStart,end)/cellSize);
                elapsed+=Time.deltaTime;
                player.position=Vector3.Lerp(segmentStart,end,MovementProgress(elapsed/seconds));
                if(elapsed>=seconds)break;
                yield return null;
            }
            bool cancelled=walkReturning;var undo=walkUndo;
            ClearWalkInterrupt();
            if(cancelled){
                // Returning to the starting cell cancels this step, not the one before it.
                if(undo!=null&&history.Count>0&&ReferenceEquals(history[history.Count-1],undo)){
                    history.RemoveAt(history.Count-1);ReleaseUnusedGeometry();
                }
                LastRule="已反向收回这一步";
            }else NotifyLanding(p);
        }else yield return Walk(p);
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
