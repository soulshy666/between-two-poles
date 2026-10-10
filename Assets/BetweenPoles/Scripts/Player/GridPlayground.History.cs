using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    sealed class WorldState {
        public Snapshot[] pieces;
        public RecoilArrival[] arrivals;
        public KnockCheckpoint[] knockPoints;
        public KnockFlight[] knockFlights;
        public Vector3 playerPosition,entryPosition;
        public Quaternion playerRotation,entryRotation;
        public Transform island;
        public bool goal;
        public Material goalMaterial;
    }
    public const int UndoLimit=20;
    readonly List<WorldState> history=new List<WorldState>(UndoLimit);
    readonly HashSet<Transform> recordedGeometry=new HashSet<Transform>();
    struct RecoilArrival {
        public int index;
        public Transform island;
        public Snapshot piece;
        public bool active;
    }
    readonly List<RecoilArrival> recoilArrivals=new List<RecoilArrival>();
    // A foreign landing is a local checkpoint, not a transfer of authored ownership.
    void RecordRecoilArrival(MagnetPiece magnet,Transform island){
        if(!island||initialState==null)return;
        for(int i=0;i<magnets.Length;i++){
            if(!magnets[i].transform.IsChildOf(magnet.transform))continue;
            var tile=Tile(CellAt(initialState.pieces[i].position));
            var origin=Owner(tile);
            int existing=-1;
            for(int j=0;j<recoilArrivals.Count;j++)if(recoilArrivals[j].index==i){
                var entry=recoilArrivals[j];entry.active=false;recoilArrivals[j]=entry;
                if(entry.island==island)existing=j;
            }
            if(origin==island)continue;
            if(existing>=0){var entry=recoilArrivals[existing];entry.active=true;recoilArrivals[existing]=entry;}
            else recoilArrivals.Add(new RecoilArrival{index=i,island=island,piece=SavePiece(magnets[i]),active=true});
        }
    }
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
        if(state.arrivals!=null)foreach(var entry in state.arrivals)if(entry.piece.geometry)keep.Add(entry.piece.geometry);
        if(state.knockPoints!=null)foreach(var entry in state.knockPoints)if(entry.piece.geometry)keep.Add(entry.piece.geometry);
    }
    void ReleaseUnusedGeometry(){
        // Only release runtime objects previously referenced by this board's snapshots.
        // Original reset geometry and every retained undo state must remain alive.
        var keep=new HashSet<Transform>();KeepGeometry(initialState,keep);
        foreach(var state in history)KeepGeometry(state,keep);
        foreach(var entry in recoilArrivals)if(entry.piece.geometry)keep.Add(entry.piece.geometry);
        foreach(var entry in knockCheckpoints)if(entry.piece.geometry)keep.Add(entry.piece.geometry);
        foreach(var m in magnets)if(m&&m.geometry)keep.Add(m.geometry);
        var expired=new List<Transform>();
        foreach(var geometry in recordedGeometry)if(!geometry||!keep.Contains(geometry))expired.Add(geometry);
        foreach(var geometry in expired){recordedGeometry.Remove(geometry);if(geometry){geometry.gameObject.SetActive(false);Destroy(geometry.gameObject);}}
    }
    Transform Owner(GridTile tile){var a=tile?tile.GetComponentInParent<IslandSurfaceAnchor>():null;return a?a.center:null;}
    Transform Owner(MagnetPiece m){var tile=Tile(Cell(m.transform));if(tile)return Owner(tile);var a=m.GetComponent<IslandSurfaceAnchor>();return a?a.center:null;}
    Snapshot SavePiece(MagnetPiece m){return new Snapshot{portalEntryPosition=m.portalEntryPosition,storedInPortal=m.storedInPortal,parent=m.transform.parent,position=m.transform.position,rotation=m.transform.rotation,geometry=m.geometry,pose=m.geometry.localRotation,geoPosition=m.geometry.localPosition,scale=m.geometry.localScale,combined=m.combined,walkable=m.walkable,enabled=m.enabled,active=m.gameObject.activeSelf,product=m.product,baseNorth=m.baseNorth,bridgeDirection=m.bridgeDirection,north=m.north,shape=m.shape,owner=Owner(m)};}
    WorldState SaveWorld(){
        var s=new WorldState{pieces=new Snapshot[magnets.Length],arrivals=recoilArrivals.ToArray(),playerPosition=player.position,playerRotation=player.rotation,island=currentIsland,entryPosition=islandEntry,entryRotation=islandEntryRotation,goal=ReachedGoal,goalMaterial=goalLight?goalLight.sharedMaterial:null};
        s.knockPoints=knockCheckpoints.ToArray();s.knockFlights=knockedFlights.ToArray();
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
            m.portalEntryPosition=s.portalEntryPosition;m.storedInPortal=s.storedInPortal;m.product=s.product;m.baseNorth=s.baseNorth;m.bridgeDirection=s.bridgeDirection;m.north=s.north;m.shape=s.shape;
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
        foreach(var bridge in FindObjectsOfType<PrejoinedTestBridges>())if(bridge.board==this&&bridge.isActiveAndEnabled)bridge.Refresh();
    }
    void RestoreWorld(WorldState s){
        ClearPushPresentations();
        movementStack.Clear();movementPlayback=null;
        EndPushInterrupt();
        var pushPose=player.GetComponent<PlayerPushPose>();if(pushPose)pushPose.End();
        ClearWalkInterrupt();
        ClearMovementInput();
        StopAllCoroutines();EndRecoilFlight();ClearKnockFlights();Busy=false;RestorePieces(s.pieces);
        recoilArrivals.Clear();if(s.arrivals!=null)recoilArrivals.AddRange(s.arrivals);
        knockCheckpoints.Clear();if(s.knockPoints!=null)knockCheckpoints.AddRange(s.knockPoints);
        if(s.knockFlights!=null)foreach(var f in s.knockFlights){knockedFlights.Add(f);if(f.magnet)KnockRendering(f.magnet,true,f.lands?Vector3.Lerp(f.offset,SurfaceOffset(f.end),Mathf.Clamp01(f.elapsed/f.duration)):f.offset);}
        player.SetPositionAndRotation(s.playerPosition,s.playerRotation);currentIsland=s.island;islandEntry=s.entryPosition;islandEntryRotation=s.entryRotation;
        ReachedGoal=s.goal;if(goalLight)goalLight.sharedMaterial=s.goalMaterial;RefreshRestoredIsland();
    }
    public bool UndoStep(){
        if(OpeningCinematic)return false;
        if(history.Count==0)return Reject("没有可以撤销的操作");
        var state=history[history.Count-1];history.RemoveAt(history.Count-1);
        RestoreWorld(state);ReleaseUnusedGeometry();LastRule="已撤销上一步";return true;
    }
    public bool ResetCurrentIsland(){
        ClearMovementInput();
        if(Busy)return Reject("请等待当前动作结束后重置小岛");
        if(knockedFlights.Exists(f=>f.lands))return Reject("请等待被撞飞的磁铁落地后重置");
        ClearPushPresentations();
        if(initialState==null)return false;
        RecordHistory(SaveWorld());
        var selected=new bool[magnets.Length];
        // Reset ownership comes from the original ground cell, never the presentation anchor.
        // A magnet authored in water has no island even when rendered beside one.
        for(int i=0;i<magnets.Length;i++){
            var tile=Tile(CellAt(initialState.pieces[i].position));
            selected[i]=tile&&Owner(tile)==currentIsland;
        }
        var restore=(Snapshot[])initialState.pieces.Clone();
        // Resetting the authored island recalls its materials and retires all foreign
        // checkpoints. Undo retains them through the WorldState captured above.
        recoilArrivals.RemoveAll(entry=>selected[entry.index]);
        foreach(var entry in recoilArrivals)if(entry.active&&entry.island==currentIsland){
            selected[entry.index]=true;restore[entry.index]=entry.piece;
        }
        var affected=(bool[])selected.Clone();
        // Split mixed-origin assemblies. Foreign materials regain their own geometry
        // at the assembly location; only this island's materials return to their start.
        for(int i=0;i<magnets.Length;i++){
            if(selected[i])continue;
            var assembly=magnets[i].transform;
            while(assembly.parent&&assembly.parent.GetComponent<MagnetPiece>())assembly=assembly.parent;
            bool connected=false;
            for(int j=0;j<magnets.Length;j++)if(selected[j]&&magnets[j].transform.IsChildOf(assembly)){connected=true;break;}
            if(!connected)continue;
            var state=restore[i];state.position=assembly.position;state.parent=assembly.parent;
            var binding=assembly.GetComponent<IslandSurfaceAnchor>();state.owner=binding?binding.center:Owner(Tile(CellAt(assembly.position)));
            restore[i]=state;affected[i]=true;
        }
        ResolveKnockReset(restore,affected);
        RestorePieces(restore,affected);
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
