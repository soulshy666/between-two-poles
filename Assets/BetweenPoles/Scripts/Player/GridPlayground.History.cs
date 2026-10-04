using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    sealed class WorldState {
        public Snapshot[] pieces;
        public Vector3 playerPosition,entryPosition;
        public Quaternion playerRotation,entryRotation;
        public Transform island;
        public bool goal;
        public Material goalMaterial;
    }
    public const int UndoLimit=20;
    readonly List<WorldState> history=new List<WorldState>(UndoLimit);
    readonly HashSet<Transform> recordedGeometry=new HashSet<Transform>();
    WorldState initialState;
    Transform currentIsland;
    Vector3 islandEntry;
    Quaternion islandEntryRotation;
    public int UndoCount {get{return history.Count;}}
    void RecordHistory(WorldState state){
        if(history.Count==UndoLimit)history.RemoveAt(0);
        history.Add(state);ReleaseUnusedGeometry();
    }
    void KeepGeometry(WorldState state,HashSet<Transform> keep){
        if(state==null)return;
        foreach(var piece in state.pieces)if(piece.geometry)keep.Add(piece.geometry);
    }
    void ReleaseUnusedGeometry(){
        // Only release runtime objects previously referenced by this board's snapshots.
        // Original reset geometry and every retained undo state must remain alive.
        var keep=new HashSet<Transform>();KeepGeometry(initialState,keep);
        foreach(var state in history)KeepGeometry(state,keep);
        foreach(var m in magnets)if(m&&m.geometry)keep.Add(m.geometry);
        var expired=new List<Transform>();
        foreach(var geometry in recordedGeometry)if(!geometry||!keep.Contains(geometry))expired.Add(geometry);
        foreach(var geometry in expired){recordedGeometry.Remove(geometry);if(geometry){geometry.gameObject.SetActive(false);Destroy(geometry.gameObject);}}
    }
    Transform Owner(GridTile tile){var a=tile?tile.GetComponentInParent<IslandSurfaceAnchor>():null;return a?a.center:null;}
    Transform Owner(MagnetPiece m){var tile=Tile(Cell(m.transform));if(tile)return Owner(tile);var a=m.GetComponent<IslandSurfaceAnchor>();return a?a.center:null;}
    Snapshot SavePiece(MagnetPiece m){return new Snapshot{parent=m.transform.parent,position=m.transform.position,rotation=m.transform.rotation,geometry=m.geometry,pose=m.geometry.localRotation,geoPosition=m.geometry.localPosition,scale=m.geometry.localScale,combined=m.combined,walkable=m.walkable,enabled=m.enabled,active=m.gameObject.activeSelf,product=m.product,baseNorth=m.baseNorth,bridgeDirection=m.bridgeDirection,north=m.north,shape=m.shape,owner=Owner(m)};}
    WorldState SaveWorld(){
        var s=new WorldState{pieces=new Snapshot[magnets.Length],playerPosition=player.position,playerRotation=player.rotation,island=currentIsland,entryPosition=islandEntry,entryRotation=islandEntryRotation,goal=ReachedGoal,goalMaterial=goalLight?goalLight.sharedMaterial:null};
        for(int i=0;i<magnets.Length;i++){s.pieces[i]=SavePiece(magnets[i]);recordedGeometry.Add(s.pieces[i].geometry);}return s;
    }
    void TrackIsland(GridTile tile){
        var owner=Owner(tile);if(owner==currentIsland)return;
        currentIsland=owner;islandEntry=player.position;islandEntryRotation=player.rotation;
    }
    void RestorePieces(Snapshot[] saved,bool[] selected=null){
        // Detach first: undoing a merge may reverse a parent/child relationship.
        for(int i=0;i<magnets.Length;i++)if(selected==null||selected[i])magnets[i].transform.SetParent(null,true);
        for(int i=0;i<magnets.Length;i++){
            if(selected!=null&&!selected[i])continue;
            var m=magnets[i];var s=saved[i];m.transform.SetParent(s.parent,true);m.transform.SetPositionAndRotation(s.position,s.rotation);
            if(m.geometry!=s.geometry){recordedGeometry.Add(m.geometry);m.geometry.gameObject.SetActive(false);m.geometry=s.geometry;}
            m.geometry.gameObject.SetActive(true);m.geometry.localPosition=s.geoPosition;m.geometry.localRotation=s.pose;m.geometry.localScale=s.scale;
            m.product=s.product;m.baseNorth=s.baseNorth;m.bridgeDirection=s.bridgeDirection;m.north=s.north;m.shape=s.shape;
            m.combined=s.combined;m.walkable=s.walkable;m.enabled=s.enabled;m.gameObject.SetActive(s.active);
            var binding=m.GetComponent<IslandSurfaceAnchor>();if(binding){binding.center=s.owner;binding.Apply();}
        }
    }
    void RefreshRestoredIsland(){
        var binding=player.GetComponent<IslandSurfaceAnchor>();if(binding){binding.center=currentIsland;binding.Apply();}
        // On a bridge there is no landing tile. Keep the last landed island as the view target.
        GridTile landing=Tile(Cell(player));
        if(!landing||Owner(landing)!=currentIsland)foreach(var t in tiles)if(t&&Owner(t)==currentIsland){landing=t;break;}
        if(landing)Landed?.Invoke(landing);
        var window=GetComponent<FiveIslandWindow>();if(window)window.RefreshLayout();
        foreach(var bridge in FindObjectsOfType<PrejoinedTestBridges>())if(bridge.board==this)bridge.Refresh();
    }
    void RestoreWorld(WorldState s){
        StopAllCoroutines();Busy=false;RestorePieces(s.pieces);
        player.SetPositionAndRotation(s.playerPosition,s.playerRotation);currentIsland=s.island;islandEntry=s.entryPosition;islandEntryRotation=s.entryRotation;
        ReachedGoal=s.goal;if(goalLight)goalLight.sharedMaterial=s.goalMaterial;RefreshRestoredIsland();
    }
    public bool UndoStep(){
        if(history.Count==0)return Reject("没有可以撤销的操作");
        var state=history[history.Count-1];history.RemoveAt(history.Count-1);
        RestoreWorld(state);ReleaseUnusedGeometry();LastRule="已撤销上一步";return true;
    }
    public bool ResetCurrentIsland(){
        if(Busy)return Reject("请等待当前动作结束后重置小岛");
        if(initialState==null)return false;
        RecordHistory(SaveWorld());
        var selected=new bool[magnets.Length];
        for(int i=0;i<magnets.Length;i++)selected[i]=initialState.pieces[i].owner==currentIsland;
        // A composite is restored with all of its materials, including an imported material.
        bool changed=true;while(changed){changed=false;for(int i=0;i<magnets.Length;i++)for(int j=0;j<magnets.Length;j++)if(selected[i]&&!selected[j]&&(magnets[j].transform.IsChildOf(magnets[i].transform)||magnets[i].transform.IsChildOf(magnets[j].transform))){selected[j]=true;changed=true;}}
        RestorePieces(initialState.pieces,selected);
        Vector3 spawn=islandEntry;var at=Tile(CellAt(spawn));
        if(!at||at.blocked||Piece(CellAt(spawn))){
            float best=float.PositiveInfinity;
            foreach(var t in tiles){if(!t||t.blocked||Owner(t)!=currentIsland||Piece(Cell(t.transform)))continue;float d=(t.transform.position-islandEntry).sqrMagnitude;if(d<best){best=d;spawn=new Vector3(t.transform.position.x,t.surfaceHeight,t.transform.position.z);}}
        }
        player.SetPositionAndRotation(spawn,islandEntryRotation);
        foreach(var t in tiles)if(t&&t.goal&&Owner(t)==currentIsland){ReachedGoal=initialState.goal;if(goalLight)goalLight.sharedMaterial=initialState.goalMaterial;break;}
        RefreshRestoredIsland();ReleaseUnusedGeometry();LastRule="已重置当前小岛；Z 可撤销重置";return true;
    }
    Vector2Int CellAt(Vector3 p){return new Vector2Int(Mathf.RoundToInt(p.x/cellSize),Mathf.RoundToInt(p.z/cellSize));}
}
}
