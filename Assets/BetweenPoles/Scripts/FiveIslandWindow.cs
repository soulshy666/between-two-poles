using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetweenPoles {
// Keep every island visible; the camera frustum determines what appears on screen.
[ExecuteAlways, DefaultExecutionOrder(-100)]
public sealed class FiveIslandWindow : MonoBehaviour {
    [Serializable] public class Room {
        public string id;
        [Min(1), Tooltip("网页编辑器的房间编号；0 表示旧关卡，按原数组顺序切换。")]
        public int roomNumber;
        public Transform center;
        public Renderer[] surfaces;
        public int[] neighbors = new int[0];
    }
    public GridPlayground board;
    public IslandCamera view;
    public Room[] rooms = new Room[0];
    public int initialRoom;
    public PrejoinedTestBridges bridges;
    [Min(1f), Tooltip("主岛外显示的边缘范围（格）；只裁切画面，不改变地块或碰撞。")]
    public float edgePreviewCells=3.25f;
    public int CurrentRoom { get; private set; }
    public Transform[] VisibleCenters { get; private set; }
    readonly HashSet<Transform> visible = new HashSet<Transform>();
    readonly Dictionary<Vector2Int,Transform> tileOwners=new Dictionary<Vector2Int,Transform>();
    readonly Dictionary<Vector2Int,GridTile> floor=new Dictionary<Vector2Int,GridTile>();
    int bridgeSignature=int.MinValue;
    readonly Dictionary<Vector2Int,MagnetPiece> bridgeSpans=new Dictionary<Vector2Int,MagnetPiece>();
    bool BridgeCandidate(MagnetPiece m){return m&&m.enabled&&m.gameObject.activeInHierarchy&&!board.IsKnockedFlying(m)&&!(board.RecoilFlying&&m==board.RecoilMagnet)&&(m.walkable||(!m.combined&&m.shape==MagnetShape.Bar&&!MagnetPiece.VerticalBar(m.Pose)));}
    Vector4 previewBounds;
    ChapterMapReturn effectMap;
    // Presentation only: keep remote puzzle state and travel simulations alive.
    public bool NeedsDynamicEffects(Vector3 position){
        if(!Application.isPlaying||!enabled||!board||!board.enabled||visible.Count==0)return true;
        if(!effectMap)effectMap=GetComponent<ChapterMapReturn>();
        if((effectMap&&effectMap.MapSessionActive)||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit||ChapterRecoilTravel.Active||board.RecoilFlying)return true;
        float padding=board.cellSize*2;
        return position.x>=previewBounds.x-padding&&position.x<=previewBounds.z+padding&&
            position.z>=previewBounds.y-padding&&position.z<=previewBounds.w+padding;
    }
    MaterialPropertyBlock previewBlock;
    sealed class PieceView {
        public Transform geometry;
        public int hierarchyRevision,geometryChildren;
        public IslandSurfaceAnchor binding;
        public Renderer[] renderers;
        public bool previewApplied;
        public Vector4 bounds;
    }
    readonly Dictionary<MagnetPiece,PieceView> pieceViews=new Dictionary<MagnetPiece,PieceView>();
    MagnetPiece[] cachedPieces;
    void OnEnable(){if(Application.isPlaying&&!GetComponent<IslandDiscoveryView>())gameObject.AddComponent<IslandDiscoveryView>();if(board)board.Landed+=OnLanded;}
    void OnDisable(){pieceViews.Clear();cachedPieces=null;if(previewBlock==null)previewBlock=new MaterialPropertyBlock();if(board)board.Landed-=OnLanded;foreach(var room in rooms)foreach(var renderer in room.surfaces)if(renderer){renderer.GetPropertyBlock(previewBlock);previewBlock.SetFloat("_IslandWindowClipEnabled",0);renderer.SetPropertyBlock(previewBlock);}}
    void Start(){if(enabled)Show(initialRoom);}
    void OnLanded(GridTile tile){
        var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();
        if(!owner)return;
        for(int i=0;i<rooms.Length;i++)if(rooms[i].center==owner.center){if(i!=CurrentRoom)Show(i);return;}
    }
    public void RefreshLayout(){pieceViews.Clear();tileOwners.Clear();floor.Clear();bridgeSignature=int.MinValue;Show(CurrentRoom);}
    public void Show(int index){
        if(index<0||index>=rooms.Length)return;
        CurrentRoom=index;visible.Clear();visible.Add(rooms[index].center);
        if(tileOwners.Count==0&&board)foreach(var tile in board.tiles){if(!tile||!tile.gameObject.activeInHierarchy)continue;var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();var key=Cell(tile.transform);floor[key]=tile;if(owner&&owner.center)tileOwners[key]=owner.center;}
        foreach(var room in rooms)if(room.center)visible.Add(room.center);
        Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
        foreach(var pair in tileOwners)if(pair.Value==rooms[index].center){var p=(Vector2)pair.Key*board.cellSize;min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
        float margin=(edgePreviewCells+.5f)*board.cellSize;
        previewBounds=new Vector4(min.x-margin,min.y-margin,max.x+margin,max.y+margin);
        var centers=new List<Transform>{rooms[index].center};
        foreach(var r in rooms){bool show=visible.Contains(r.center);if(show&&r.center!=rooms[index].center)centers.Add(r.center);foreach(var renderer in r.surfaces)if(renderer){renderer.enabled=show;ApplyPreview(renderer,r.center!=rooms[index].center);}}
        VisibleCenters=centers.ToArray();RefreshPieces();
    }
    public void PreviewRecoilTarget(Transform target){
        if(!target)return;visible.Add(target);
        foreach(var room in rooms)if(room.center==target)foreach(var renderer in room.surfaces)if(renderer)renderer.enabled=true;
        RefreshPieces();
    }
    void ApplyPreview(Renderer renderer,bool edge){if(previewBlock==null)previewBlock=new MaterialPropertyBlock();renderer.GetPropertyBlock(previewBlock);previewBlock.SetFloat("_IslandWindowClipEnabled",0);previewBlock.SetVector("_IslandWindowBounds",previewBounds);renderer.SetPropertyBlock(previewBlock);}
    Vector2Int Cell(Transform t){return new Vector2Int(Mathf.RoundToInt(t.position.x/board.cellSize),Mathf.RoundToInt(t.position.z/board.cellSize));}
    void LateUpdate(){
        if(!board||!board.enabled)return;
        // Edit-mode Game view must hide the same remote pieces as the island surfaces.
        // Do not rebuild gameplay bridge links or change puzzle transforms in previews.
        if(!Application.isPlaying){
            if(rooms.Length>0&&(visible.Count==0||CurrentRoom!=Mathf.Clamp(initialRoom,0,rooms.Length-1)))Show(Mathf.Clamp(initialRoom,0,rooms.Length-1));
            else RefreshPieces();
            return;
        }
        RefreshBridgeLinks();RefreshPieces();
    }
    void RefreshBridgeLinks(){
        if(!board||!bridges||floor.Count==0)return;
        int signature=17;
        foreach(var m in board.magnets)if(BridgeCandidate(m)){unchecked{signature=signature*31+m.GetInstanceID();signature=signature*31+Cell(m.transform).GetHashCode();signature=signature*31+m.Pose.GetHashCode();signature=signature*31+(int)m.product;signature=signature*31+m.bridgeDirection.GetHashCode();signature=signature*31+m.transform.position.y.GetHashCode();signature=signature*31+(m.walkable?1:0);signature=signature*31+(m.CanBePushed?1:0);signature=signature*31+(m.geometry?m.geometry.GetInstanceID():0);signature=signature*31+m.VisualHierarchyRevision;}}
        if(signature==bridgeSignature)return;bridgeSignature=signature;
        // Only rebuild the occupied-cell index after an actual input change.
        var spans=bridgeSpans;spans.Clear();
        foreach(var m in board.magnets)if(BridgeCandidate(m)){IndexBridgeSpan(spans,Cell(m.transform),m);if(m.product==MagnetProduct.Bridge)IndexBridgeSpan(spans,Cell(m.transform)+m.bridgeDirection,m);}
        var links=new List<PrejoinedTestBridges.Link>();
        foreach(var pair in spans){var m=pair.Value;if(pair.Key!=Cell(m.transform))continue;var right=m.Pose*Vector3.right;var dir=m.product==MagnetProduct.Bridge?m.bridgeDirection:new Vector2Int(Mathf.RoundToInt(right.x),Mathf.RoundToInt(right.z));GridTile a=null,b=null;
            for(int step=1;step<=3;step++){var p=pair.Key-dir*step;if(floor.TryGetValue(p,out a))break;if(!spans.ContainsKey(p))break;}
            for(int step=1;step<=3;step++){var p=pair.Key+dir*step;if(floor.TryGetValue(p,out b))break;if(!spans.ContainsKey(p))break;}
            if(a&&b&&tileOwners.TryGetValue(Cell(a.transform),out var ownerA)&&tileOwners.TryGetValue(Cell(b.transform),out var ownerB)&&ownerA!=ownerB)links.Add(new PrejoinedTestBridges.Link{shoreA=a,shoreB=b,magnet=m,renderers=m.GetComponentsInChildren<Renderer>(true)});
        }
        // Clear old shader overrides before a reset removes a completed bridge.
        bridges.enabled=false;bridges.links=links.ToArray();bridges.enabled=true;
    }
    static void IndexBridgeSpan(Dictionary<Vector2Int,MagnetPiece> spans,Vector2Int cell,MagnetPiece piece){
        // Cargo shares a cell with its support. Keep the fixed deck as the
        // projection link so depositing cargo cannot remove the bridge below it.
        if(spans.TryGetValue(cell,out var previous)){
            bool fixedDeck=piece.walkable&&!piece.CanBePushed;
            bool previousDeck=previous.walkable&&!previous.CanBePushed;
            if(previousDeck&&!fixedDeck)return;
            if(previousDeck==fixedDeck&&previous.transform.position.y<=piece.transform.position.y)return;
        }
        spans[cell]=piece;
    }
    void RefreshPieces(){
        if(!board||visible.Count==0)return;
        // Level reloads/debug layouts replace this array; do not retain their old objects.
        if(cachedPieces!=board.magnets){pieceViews.Clear();cachedPieces=board.magnets;}
        foreach(var piece in board.magnets){
            if(!piece||!piece.enabled)continue;
            int children=piece.geometry?piece.geometry.childCount:0;
            if(!pieceViews.TryGetValue(piece,out var state)){
                state=new PieceView();pieceViews.Add(piece,state);
            }
            // Edit-time hierarchy/component changes can bypass runtime assembly paths.
            // Runtime swaps, undo, links and reactivation invalidate the cached renderer list.
            if(!Application.isPlaying||state.renderers==null||state.geometry!=piece.geometry||
                state.hierarchyRevision!=piece.VisualHierarchyRevision||state.geometryChildren!=children){
                state.geometry=piece.geometry;state.geometryChildren=children;
                state.hierarchyRevision=piece.VisualHierarchyRevision;
                state.binding=piece.GetComponent<IslandSurfaceAnchor>();
                state.renderers=piece.GetComponentsInChildren<Renderer>(true);
                state.previewApplied=false;
            }
            bool flying=(board.RecoilFlying&&piece==board.RecoilMagnet)||board.IsKnockedFlying(piece);
            bool updatePreview=!flying&&(!state.previewApplied||state.bounds!=previewBounds);
            foreach(var renderer in state.renderers){
                if(!renderer||(flying&&!renderer.gameObject.activeInHierarchy))continue;
                // Preserve recovery from other presentation systems hiding a renderer,
                // without making redundant native enabled/property-block writes.
                if(!renderer.enabled)renderer.enabled=true;
                if(updatePreview)ApplyPreview(renderer,false);
            }
            if(flying){state.previewApplied=false;continue;}
            state.previewApplied=true;state.bounds=previewBounds;
            var binding=state.binding;
            if(!binding)continue;
            // Moving pieces adopt the island they actually occupy; floating pieces retain their authored owner.
            Vector3 position=piece.transform.position;var cell=new Vector2Int(Mathf.RoundToInt(position.x/board.cellSize),Mathf.RoundToInt(position.z/board.cellSize));
            if(tileOwners.TryGetValue(cell,out var owner)&&binding.center!=owner)binding.center=owner;
        }
    }
}
}
