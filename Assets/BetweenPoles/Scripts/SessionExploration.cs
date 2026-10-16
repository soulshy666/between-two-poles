using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public static class SessionExploration {
    static readonly Dictionary<FiveIslandWindow,Dictionary<int,HashSet<int>>> connections=new Dictionary<FiveIslandWindow,Dictionary<int,HashSet<int>>>();
    static readonly HashSet<string> visited=new HashSet<string>();
    public static int Revision {get;private set;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){visited.Clear();connections.Clear();Revision++;}
    public static string[] Export(){var result=new string[visited.Count];visited.CopyTo(result);return result;}
    public static void Import(string[] values){visited.Clear();connections.Clear();if(values!=null)foreach(var value in values)visited.Add(value);Revision++;}
    public static int GetInt(string key,int fallback=0){return visited.Contains(key)?1:fallback;}
    public static void Visit(string scene,string room){if(visited.Add(ChapterAtlas.Key(scene,room)))Revision++;visited.Add(ChapterAtlas.WorldVisitKey(scene));}
    public static void VisitTile(GridPlayground board,GridTile tile){
        if(!board||!tile||ChapterAtlas.IsInspectionScene(board.gameObject.scene))return;
        var anchor=tile.GetComponentInParent<IslandSurfaceAnchor>();
        if(!anchor||!anchor.center)return;
        string scene=board.gameObject.scene.path;
        Visit(scene,anchor.center.name);
        var window=board.GetComponent<FiveIslandWindow>();
        if(window)foreach(var room in window.rooms)if(room.center==anchor.center){Visit(scene,room.id);break;}
    }
    public static void RecordStandingIsland(GridPlayground board){
        if(!board||!board.player||board.OpeningCinematic||board.RecoilFlying||ChapterRecoilTravel.Active||BlackHoleTravel.InTransit)return;
        var tile=board.TileAt(board.PlayerCell);
        if(tile&&Mathf.Abs(board.player.position.y-tile.surfaceHeight)<.15f)VisitTile(board,tile);
    }
    public static float Level(FiveIslandWindow window,int index){
        if(Known(window,index))return 0;
        for(int i=0;i<window.rooms.Length;i++)if(Known(window,i))foreach(int next in Neighbors(window,i))if(next==index)return .5f;
        return 1;
    }
    static HashSet<int> Neighbors(FiveIslandWindow w,int index){
        if(!connections.TryGetValue(w,out var graph)){
            graph=new Dictionary<int,HashSet<int>>();var owners=new Dictionary<Vector2Int,int>();
            if(w.board)foreach(var tile in w.board.tiles){if(!tile)continue;var anchor=tile.GetComponentInParent<IslandSurfaceAnchor>();if(!anchor)continue;for(int i=0;i<w.rooms.Length;i++)if(w.rooms[i].center==anchor.center){var p=tile.transform.position;owners[new Vector2Int(Mathf.RoundToInt(p.x/w.board.cellSize),Mathf.RoundToInt(p.z/w.board.cellSize))]=i;break;}}
            for(int i=0;i<w.rooms.Length;i++)graph[i]=w.rooms[i].neighbors!=null&&w.rooms[i].neighbors.Length>0?new HashSet<int>(w.rooms[i].neighbors):IslandVisibility.Neighbors(owners,i,IslandVisibility.GapForScene(w.gameObject.scene.name));
            connections[w]=graph;
        }
        return graph[index];
    }
    static bool Known(FiveIslandWindow w,int i){var r=w.rooms[i];return GetInt(ChapterAtlas.Key(w.gameObject.scene.path,r.id))>0||r.center&&GetInt(ChapterAtlas.Key(w.gameObject.scene.path,r.center.name))>0;}
}
}
