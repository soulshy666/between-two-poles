using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetweenPoles {
// Only presentation is culled. Puzzle objects stay alive so snapshots and pushed pieces survive travel.
[ExecuteAlways, DefaultExecutionOrder(-100)]
public sealed class FiveIslandWindow : MonoBehaviour {
    [Serializable] public class Room {
        public string id;
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
    Vector4 previewBounds;
    MaterialPropertyBlock previewBlock;
    void OnEnable(){if(board)board.Landed+=OnLanded;}
    void OnDisable(){if(previewBlock==null)previewBlock=new MaterialPropertyBlock();if(board)board.Landed-=OnLanded;foreach(var room in rooms)foreach(var renderer in room.surfaces)if(renderer){renderer.GetPropertyBlock(previewBlock);previewBlock.SetFloat("_IslandWindowClipEnabled",0);renderer.SetPropertyBlock(previewBlock);}}
    void Start(){if(enabled)Show(initialRoom);}
    void OnLanded(GridTile tile){
        var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();
        if(!owner)return;
        for(int i=0;i<rooms.Length;i++)if(rooms[i].center==owner.center){if(i!=CurrentRoom)Show(i);return;}
    }
    public void RefreshLayout(){tileOwners.Clear();floor.Clear();bridgeSignature=int.MinValue;Show(CurrentRoom);}
    public void Show(int index){
        if(index<0||index>=rooms.Length)return;
        CurrentRoom=index;visible.Clear();visible.Add(rooms[index].center);
        if(tileOwners.Count==0&&board)foreach(var tile in board.tiles){if(!tile||!tile.gameObject.activeInHierarchy)continue;var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();var key=Cell(tile.transform);floor[key]=tile;if(owner&&owner.center)tileOwners[key]=owner.center;}
        // Adjacency is a graph, not one nearest island per compass direction. Never include neighbors-of-neighbors.
        foreach(var neighbor in IslandVisibility.Neighbors(tileOwners,rooms[index].center,IslandVisibility.GapForScene(gameObject.scene.name)))visible.Add(neighbor);
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
        int signature=17;var spans=new Dictionary<Vector2Int,MagnetPiece>();
        foreach(var m in board.magnets)if(m&&m.enabled&&m.gameObject.activeInHierarchy&&!board.IsKnockedFlying(m)&&!(board.RecoilFlying&&m==board.RecoilMagnet)&&(m.walkable||(!m.combined&&m.shape==MagnetShape.Bar&&!MagnetPiece.VerticalBar(m.Pose)))){unchecked{signature=signature*31+m.GetInstanceID();signature=signature*31+Cell(m.transform).GetHashCode();signature=signature*31+m.transform.rotation.GetHashCode();signature=signature*31+(int)m.product;signature=signature*31+m.bridgeDirection.GetHashCode();}spans[Cell(m.transform)]=m;if(m.product==MagnetProduct.Bridge)spans[Cell(m.transform)+m.bridgeDirection]=m;}
        if(signature==bridgeSignature)return;bridgeSignature=signature;
        var links=new List<PrejoinedTestBridges.Link>();
        foreach(var pair in spans){var m=pair.Value;if(pair.Key!=Cell(m.transform))continue;var right=m.transform.right;var dir=m.product==MagnetProduct.Bridge?m.bridgeDirection:new Vector2Int(Mathf.RoundToInt(right.x),Mathf.RoundToInt(right.z));GridTile a=null,b=null;
            for(int step=1;step<=3;step++){var p=pair.Key-dir*step;if(floor.TryGetValue(p,out a))break;if(!spans.ContainsKey(p))break;}
            for(int step=1;step<=3;step++){var p=pair.Key+dir*step;if(floor.TryGetValue(p,out b))break;if(!spans.ContainsKey(p))break;}
            if(a&&b&&tileOwners.TryGetValue(Cell(a.transform),out var ownerA)&&tileOwners.TryGetValue(Cell(b.transform),out var ownerB)&&ownerA!=ownerB)links.Add(new PrejoinedTestBridges.Link{shoreA=a,shoreB=b,magnet=m,renderers=m.GetComponentsInChildren<Renderer>(true)});
        }
        // Clear old shader overrides before a reset removes a completed bridge.
        bridges.enabled=false;bridges.links=links.ToArray();bridges.enabled=true;
    }
    void RefreshPieces(){
        if(!board||visible.Count==0)return;
        foreach(var piece in board.magnets){
            if(!piece||!piece.enabled)continue;
            if((board.RecoilFlying&&piece==board.RecoilMagnet)||board.IsKnockedFlying(piece)){foreach(var r in piece.GetComponentsInChildren<Renderer>())r.enabled=true;continue;}
            var binding=piece.GetComponent<IslandSurfaceAnchor>();
            if(!binding)continue;
            // Moving pieces adopt the island they actually occupy; floating pieces retain their authored owner.
            Vector3 position=piece.transform.position;var cell=new Vector2Int(Mathf.RoundToInt(position.x/board.cellSize),Mathf.RoundToInt(position.z/board.cellSize));
            if(tileOwners.TryGetValue(cell,out var owner))binding.center=owner;
            bool show=visible.Contains(binding.center);
            if(bridges&&bridges.links!=null)foreach(var link in bridges.links)if(link.magnet==piece){
                var a=link.shoreA.GetComponentInParent<IslandSurfaceAnchor>();var b=link.shoreB.GetComponentInParent<IslandSurfaceAnchor>();
                show=a&&b&&visible.Contains(a.center)&&visible.Contains(b.center);break;
            }
            foreach(var renderer in piece.GetComponentsInChildren<Renderer>(true)){renderer.enabled=show;ApplyPreview(renderer,binding.center!=rooms[CurrentRoom].center);}
        }
    }
}
}
