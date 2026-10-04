using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BetweenPoles {
public sealed partial class GridPlayground:MonoBehaviour {
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
    struct Snapshot { public Transform parent; public Vector3 position,scale; public Quaternion rotation; public bool combined,walkable,enabled,active; public Transform geometry; public Quaternion pose; public Vector3 geoPosition; public MagnetProduct product; public bool baseNorth; public Vector2Int bridgeDirection; }
    Snapshot[] initial; bool[] poles; MagnetShape[] shapes;
    Vector3 playerStart; Quaternion playerRotation;
    Material initialGoal;
    Vector2Int Cell(Transform t){return new Vector2Int(Mathf.RoundToInt(t.position.x/cellSize),Mathf.RoundToInt(t.position.z/cellSize));}
    GridTile Tile(Vector2Int p){foreach(var t in tiles)if(t&&Cell(t.transform)==p)return t;return null;}
    MagnetPiece Piece(Vector2Int p,MagnetPiece ignore=null){foreach(var m in magnets)if(m&&m.enabled&&m!=ignore&&m.gameObject.activeInHierarchy&&m.Occupies(p,cellSize))return m;return null;}
    Vector3 Position(Vector2Int p,float y=0){return new Vector3(p.x*cellSize,y,p.y*cellSize);}
    bool Floor(Vector2Int p){var t=Tile(p);return t&&!t.blocked;}
    void Awake(){
        CaptureInitialState();
    }
    public void CaptureInitialState(){
        playerStart=player.position;playerRotation=player.rotation;
        if(goalLight)initialGoal=goalLight.sharedMaterial;
        initial=new Snapshot[magnets.Length];poles=new bool[magnets.Length];shapes=new MagnetShape[magnets.Length];
        for(int i=0;i<magnets.Length;i++) {var m=magnets[i];initial[i]=new Snapshot{geometry=m.geometry,pose=m.geometry.localRotation,geoPosition=m.geometry.localPosition,product=m.product,baseNorth=m.baseNorth,bridgeDirection=m.bridgeDirection,parent=m.transform.parent,position=m.transform.position,rotation=m.transform.rotation,scale=m.geometry.localScale,combined=m.combined,walkable=m.walkable,enabled=m.enabled,active=m.gameObject.activeSelf};poles[i]=m.north;shapes[i]=m.shape;}
    }
    void Update(){
        var panel=GetComponent<MagnetDebugPanel>();
        if(panel&&panel.enabled&&panel.IsOpen)return;
        if(Input.GetKeyDown(KeyCode.R)){ResetPuzzle();return;}
        if(Busy)return;
        if(Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.RightArrow))TryStep(Vector2Int.right);
        else if(Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.LeftArrow))TryStep(Vector2Int.left);
        else if(Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow))TryStep(Vector2Int.up);
        else if(Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow))TryStep(Vector2Int.down);
    }
    IEnumerator Slide(Transform target,Vector3 end,float seconds){
        Vector3 start=target.position;float time=0;
        while(time<seconds){time+=Time.deltaTime;float a=Mathf.SmoothStep(0,1,time/seconds);target.position=Vector3.Lerp(start,end,a);yield return null;}
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
    IEnumerator MovePlayer(Vector2Int p){Busy=true;yield return Walk(p);Busy=false;}
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
            if(m.geometry!=initial[i].geometry){m.geometry.gameObject.SetActive(false);Destroy(m.geometry.gameObject);m.geometry=initial[i].geometry;}
            m.geometry.gameObject.SetActive(true);m.geometry.localPosition=initial[i].geoPosition;m.geometry.localRotation=initial[i].pose;m.geometry.localScale=initial[i].scale;
            m.product=initial[i].product;m.baseNorth=initial[i].baseNorth;m.bridgeDirection=initial[i].bridgeDirection;
            m.north=poles[i];m.shape=shapes[i];m.combined=initial[i].combined;m.walkable=initial[i].walkable;m.enabled=initial[i].enabled;m.gameObject.SetActive(initial[i].active);
        }
        player.position=playerStart;player.rotation=playerRotation;if(goalLight)goalLight.sharedMaterial=initialGoal;
        var tile=Tile(Cell(player));if(tile)Landed?.Invoke(tile);
    }
}
}
