using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BetweenPoles {
public sealed class GridPlayground:MonoBehaviour {
    public float cellSize=1.5f;
    public float stepSeconds=.22f;
    [Range(.3f,1.2f)] public float joinSeconds=.65f;
    public Transform player;
    public Transform playerVisual;
    public GridTile[] tiles;
    public MagnetPiece[] magnets;
    public Renderer goalLight;
    public Material goalCompleteMaterial;
    public bool ReachedGoal { get; private set; }
    public bool Busy { get; private set; }
    public event System.Action<GridTile> Landed;
    struct Snapshot { public Transform parent; public Vector3 position,scale; public Quaternion rotation; public bool combined,walkable,enabled,active; }
    Snapshot[] initial; bool[] poles; MagnetShape[] shapes;
    Vector3 playerStart; Quaternion playerRotation;
    Material initialGoal;
    Vector2Int Cell(Transform t){return new Vector2Int(Mathf.RoundToInt(t.position.x/cellSize),Mathf.RoundToInt(t.position.z/cellSize));}
    GridTile Tile(Vector2Int p){foreach(var t in tiles)if(t&&Cell(t.transform)==p)return t;return null;}
    MagnetPiece Piece(Vector2Int p,MagnetPiece ignore=null){foreach(var m in magnets)if(m&&m.enabled&&m!=ignore&&m.gameObject.activeSelf&&Cell(m.transform)==p)return m;return null;}
    Vector3 Position(Vector2Int p,float y=0){return new Vector3(p.x*cellSize,y,p.y*cellSize);}
    bool Floor(Vector2Int p){var t=Tile(p);return t&&!t.blocked;}
    void Awake(){
        playerStart=player.position;playerRotation=player.rotation;
        if(goalLight)initialGoal=goalLight.sharedMaterial;
        initial=new Snapshot[magnets.Length];poles=new bool[magnets.Length];shapes=new MagnetShape[magnets.Length];
        for(int i=0;i<magnets.Length;i++) {var m=magnets[i];initial[i]=new Snapshot{parent=m.transform.parent,position=m.transform.position,rotation=m.transform.rotation,scale=m.geometry.localScale,combined=m.combined,walkable=m.walkable,enabled=m.enabled,active=m.gameObject.activeSelf};poles[i]=m.north;shapes[i]=m.shape;}
    }
    void Update(){
        if(Input.GetKeyDown(KeyCode.R)){ResetPuzzle();return;}
        if(Busy)return;
        if(Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.RightArrow))TryStep(Vector2Int.right);
        else if(Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.LeftArrow))TryStep(Vector2Int.left);
        else if(Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow))TryStep(Vector2Int.up);
        else if(Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow))TryStep(Vector2Int.down);
    }
    public bool TryStep(Vector2Int dir){
        if(Busy||Mathf.Abs(dir.x)+Mathf.Abs(dir.y)!=1)return false;
        Vector2Int from=Cell(player),next=from+dir;
        var t=Tile(next); if(t&&t.blocked)return false;
        var m=Piece(next);
        if(m&&!m.walkable) {
            if(m.combined)return false;
            Vector2Int target=next+dir;var far=Piece(target,m);var land=Tile(target);
            if(land&&land.blocked)return false;
            if(far){
                if(far.combined)return false;
                if(far.north==m.north){
                    Vector2Int beyond=target+dir;
                    if(Floor(beyond)&&!Piece(beyond)){StartCoroutine(PushPair(m,far,dir,next,target,beyond));return true;}
                    Vector2Int back=from-dir;
                    if(Floor(back)&&!Piece(back)){StartCoroutine(MovePlayer(back));return true;}
                    return false;
                }
                // This first playable level contains parallel bars only; mixed recipes are not guessed.
                if(m.shape!=MagnetShape.Bar||far.shape!=MagnetShape.Bar)return false;
                if(Mathf.Abs(Vector3.Dot(m.transform.right,far.transform.right))<.95f)return false;
                StartCoroutine(Join(m,far,target,next));return true;
            }
            if(!Floor(target))return false;
            StartCoroutine(Push(m,target,next,dir));return true;
        }
        if(!Floor(next)&&!(m&&m.walkable))return false;
        StartCoroutine(MovePlayer(next));return true;
    }
    IEnumerator Slide(Transform target,Vector3 end,float seconds){
        Vector3 start=target.position;float time=0;
        while(time<seconds){time+=Time.deltaTime;float a=Mathf.SmoothStep(0,1,time/seconds);target.position=Vector3.Lerp(start,end,a);yield return null;}
        target.position=end;
    }
    float Height(Vector2Int p){var m=Piece(p);if(m&&m.walkable)return .22f;var t=Tile(p);return t?t.surfaceHeight:0;}
    IEnumerator Walk(Vector2Int p){
        Vector3 end=Position(p,Height(p));Vector3 direction=end-player.position;direction.y=0;
        if(direction.sqrMagnitude>.01f)player.rotation=Quaternion.LookRotation(direction);
        yield return Slide(player,end,stepSeconds);
        var t=Tile(p);if(t&&t.goal){ReachedGoal=true;if(goalLight&&goalCompleteMaterial)goalLight.sharedMaterial=goalCompleteMaterial;}
        if(t)Landed?.Invoke(t);
    }
    IEnumerator MovePlayer(Vector2Int p){Busy=true;yield return Walk(p);Busy=false;}
    IEnumerator Push(MagnetPiece m,Vector2Int p,Vector2Int playerCell,Vector2Int dir){
        Busy=true;Vector3 start=m.transform.position;Quaternion rotation=m.geometry.localRotation;
        float time=0;
        while(time<stepSeconds){time+=Time.deltaTime;float a=Mathf.SmoothStep(0,1,time/stepSeconds);m.transform.position=Vector3.Lerp(start,Position(p),a);m.geometry.localRotation=Quaternion.AngleAxis(180*a,new Vector3(dir.y,0,-dir.x))*rotation;yield return null;}
        m.transform.position=Position(p);m.geometry.localRotation=rotation;
        yield return Walk(playerCell);Busy=false;
    }
    IEnumerator PushPair(MagnetPiece a,MagnetPiece b,Vector2Int dir,Vector2Int next,Vector2Int target,Vector2Int beyond){
        Busy=true;yield return Slide(b.transform,Position(beyond),stepSeconds);yield return Slide(a.transform,Position(target),stepSeconds);yield return Walk(next);Busy=false;
    }
    IEnumerator Join(MagnetPiece incoming,MagnetPiece target,Vector2Int targetCell,Vector2Int playerCell){
        Busy=true;Vector3 center=Position(targetCell);
        Vector3 start=incoming.transform.position;
        Vector3 side=target.transform.forward;
        if(Vector3.Dot(start-center,side)<0)side=-side;
        var incomingBox=incoming.geometry.GetComponentInChildren<BoxCollider>();
        var targetBox=target.geometry.GetComponentInChildren<BoxCollider>();
        float incomingWidth=incomingBox.size.z*Mathf.Abs(incomingBox.transform.lossyScale.z);
        float targetWidth=targetBox.size.z*Mathf.Abs(targetBox.transform.lossyScale.z);
        Vector3 incomingEnd=center+side*(targetWidth*.5f);
        Vector3 targetEnd=center-side*(incomingWidth*.5f);
        Vector3 targetStart=target.geometry.position;
        float elapsed=0;
        // Both halves separate sideways before advancing. Their solid volumes never cross.
        // Offsets come from the physical widths, so prefab edits cannot introduce overlap.
        while(elapsed<joinSeconds){
            elapsed+=Time.deltaTime;
            Vector3 a,b;
            SampleJoin(start,targetStart,incomingEnd,targetEnd,side,elapsed/joinSeconds,out a,out b);
            incoming.transform.position=a;target.geometry.position=b;
            yield return null;
        }
        incoming.transform.position=incomingEnd;target.geometry.position=targetEnd;
        incoming.transform.rotation=target.transform.rotation;
        incoming.transform.SetParent(target.transform,true);incoming.enabled=false;
        incoming.combined=true;target.combined=true;target.walkable=true;
        incoming.gameObject.SetActive(true);
        yield return Walk(playerCell);Busy=false;
    }
    public static void SampleJoin(Vector3 incomingStart,Vector3 targetStart,Vector3 incomingEnd,Vector3 targetEnd,Vector3 side,float progress,out Vector3 incoming,out Vector3 target){
        float separate=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.28f));
        float advance=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.28f)/.72f));
        Vector3 laneStart=incomingStart+side*Vector3.Dot(incomingEnd-targetStart,side);
        incoming=Vector3.Lerp(Vector3.Lerp(incomingStart,laneStart,separate),incomingEnd,advance);
        target=Vector3.Lerp(targetStart,targetEnd,separate);
    }
    public void ResetPuzzle(){
        StopAllCoroutines();Busy=false;ReachedGoal=false;
        for(int i=0;i<magnets.Length;i++)magnets[i].transform.SetParent(initial[i].parent,true);
        for(int i=0;i<magnets.Length;i++){
            var m=magnets[i];m.transform.position=initial[i].position;m.transform.rotation=initial[i].rotation;
            m.geometry.localPosition=Vector3.zero;m.geometry.localRotation=Quaternion.identity;m.geometry.localScale=initial[i].scale;
            m.north=poles[i];m.shape=shapes[i];m.combined=initial[i].combined;m.walkable=initial[i].walkable;m.enabled=initial[i].enabled;m.gameObject.SetActive(initial[i].active);
        }
        player.position=playerStart;player.rotation=playerRotation;if(goalLight)goalLight.sharedMaterial=initialGoal;
        var tile=Tile(Cell(player));if(tile)Landed?.Invoke(tile);
    }
}
}
