using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetweenPoles {
// Only presentation is culled. Puzzle objects stay alive so snapshots and pushed pieces survive travel.
[DefaultExecutionOrder(-100)]
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
    public int CurrentRoom { get; private set; }
    public Transform[] VisibleCenters { get; private set; }
    readonly HashSet<Transform> visible = new HashSet<Transform>();
    readonly Dictionary<Vector2Int,Transform> tileOwners=new Dictionary<Vector2Int,Transform>();
    readonly Dictionary<Vector2Int,GridTile> floor=new Dictionary<Vector2Int,GridTile>();
    int bridgeSignature=int.MinValue;
    void OnEnable(){if(board)board.Landed+=OnLanded;}
    void OnDisable(){if(board)board.Landed-=OnLanded;}
    void Start(){Show(initialRoom);}
    void OnLanded(GridTile tile){
        var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();
        if(!owner)return;
        for(int i=0;i<rooms.Length;i++)if(rooms[i].center==owner.center){if(i!=CurrentRoom)Show(i);return;}
    }
    public void Show(int index){
        if(index<0||index>=rooms.Length)return;
        CurrentRoom=index;visible.Clear();visible.Add(rooms[index].center);
        if(tileOwners.Count==0&&board)foreach(var tile in board.tiles){var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();var key=Cell(tile.transform);floor[key]=tile;if(owner)tileOwners[key]=owner.center;}
        foreach(int n in rooms[index].neighbors)if(n>=0&&n<rooms.Length&&visible.Count<5)visible.Add(rooms[n].center);
        var centers=new List<Transform>{rooms[index].center};
        foreach(var r in rooms){bool show=visible.Contains(r.center);if(show&&r.center!=rooms[index].center)centers.Add(r.center);foreach(var renderer in r.surfaces)if(renderer)renderer.enabled=show;}
        VisibleCenters=centers.ToArray();RefreshPieces();
    }
    Vector2Int Cell(Transform t){return new Vector2Int(Mathf.RoundToInt(t.position.x/board.cellSize),Mathf.RoundToInt(t.position.z/board.cellSize));}
    void LateUpdate(){RefreshBridgeLinks();RefreshPieces();}
    void RefreshBridgeLinks(){
        if(!board||!bridges||floor.Count==0)return;
        int signature=17;var spans=new Dictionary<Vector2Int,MagnetPiece>();
        foreach(var m in board.magnets)if(m&&m.enabled&&m.walkable&&m.gameObject.activeInHierarchy){unchecked{signature=signature*31+m.GetInstanceID();}spans[Cell(m.transform)]=m;}
        if(signature==bridgeSignature)return;bridgeSignature=signature;
        var links=new List<PrejoinedTestBridges.Link>();
        foreach(var pair in spans){var m=pair.Value;var right=m.transform.right;var dir=new Vector2Int(Mathf.RoundToInt(right.x),Mathf.RoundToInt(right.z));GridTile a=null,b=null;
            for(int step=1;step<=3;step++){var p=pair.Key-dir*step;if(floor.TryGetValue(p,out a))break;if(!spans.ContainsKey(p))break;}
            for(int step=1;step<=3;step++){var p=pair.Key+dir*step;if(floor.TryGetValue(p,out b))break;if(!spans.ContainsKey(p))break;}
            if(a&&b)links.Add(new PrejoinedTestBridges.Link{shoreA=a,shoreB=b,magnet=m,renderers=m.GetComponentsInChildren<Renderer>(true)});
        }
        // Clear old shader overrides before a reset removes a completed bridge.
        bridges.enabled=false;bridges.links=links.ToArray();bridges.enabled=true;
    }
    void RefreshPieces(){
        if(!board||visible.Count==0)return;
        foreach(var piece in board.magnets){
            if(!piece||!piece.enabled)continue;
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
            foreach(var renderer in piece.GetComponentsInChildren<Renderer>(true))renderer.enabled=show;
        }
    }
}
}
