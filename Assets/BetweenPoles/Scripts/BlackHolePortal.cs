using UnityEngine;
namespace BetweenPoles {
// Place under a GridTile. Only the player activates this one-cell portal.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class BlackHolePortal : MonoBehaviour {
    public GridPlayground board;
    public GridTile tile;
    bool armed;
    Vector2Int lastOutside;
    public Vector2Int EntryDirection {get;private set;}
    public string Room { get { var a=tile.GetComponentInParent<IslandSurfaceAnchor>(true);return a&&a.center?a.center.name:""; } }
    void OnEnable(){foreach(var r in GetComponentsInChildren<Renderer>())r.localBounds=new Bounds(Vector3.zero,Vector3.one*300);}
    public Vector2Int Cell {get{return new Vector2Int(Mathf.RoundToInt(tile.transform.position.x/board.cellSize),Mathf.RoundToInt(tile.transform.position.z/board.cellSize));}}
    public bool CanArrive {get{if(!tile||tile.blocked)return false;foreach(var m in board.magnets)if(m&&m.enabled&&m.gameObject.activeSelf&&m.Occupies(Cell,board.cellSize))return false;return true;}}
    public bool TryExit(Vector2Int direction,out GridTile exit){Vector3 landing;return TryExitLanding(direction,out exit,out landing);}
    public bool TryExitLanding(Vector2Int direction,out GridTile exit,out Vector3 landing){
        exit=null;landing=Vector3.zero;if(Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1||!CanArrive)return false;
        return TrySurface(board,Cell+direction,out exit,out landing);
    }
    public static bool TrySurface(GridPlayground board,Vector2Int cell,out GridTile exit,out Vector3 landing){
        exit=board.TileAt(cell);
        landing=new Vector3(cell.x*board.cellSize,exit?exit.surfaceHeight:float.NegativeInfinity,cell.y*board.cellSize);
        if(board.IsWreckCell(cell))return false;
        bool support=exit&&!exit.blocked;
        if(exit&&exit.blocked){
            var anchor=exit.GetComponentInParent<IslandSurfaceAnchor>(true);
            if(anchor)support|=FindTop(board,anchor.transform,cell,ref landing);
        }
        // Portals can place a player on any magnetic shape, including upright pieces.
        foreach(var m in board.magnets)if(m&&m.enabled&&m.gameObject.activeSelf&&m.Occupies(cell,board.cellSize))support|=FindTop(board,m.geometry,cell,ref landing);
        return support;
    }
    static bool FindTop(GridPlayground board,Transform root,Vector2Int cell,ref Vector3 landing){
        bool found=false;float nearest=float.PositiveInfinity;Vector3 center=new Vector3(cell.x*board.cellSize,0,cell.y*board.cellSize);
        // Renderer bounds are deliberately enlarged for spherical rendering; use real mesh triangles.
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
            if(!filter.gameObject.activeSelf||filter.transform.IsChildOf(board.player)||filter.GetComponentInParent<BlackHolePortal>(true))continue;
            var mesh=filter.sharedMesh;if(!mesh||!mesh.isReadable)continue;var vertices=mesh.vertices;var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3){
                var a=filter.transform.TransformPoint(vertices[indices[i]]);var b=filter.transform.TransformPoint(vertices[indices[i+1]]);var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.y<.5f)continue;
                var point=(a+b+c)/3;var delta=point-center;
                if(Mathf.Abs(delta.x)>board.cellSize*.5f||Mathf.Abs(delta.z)>board.cellSize*.5f)continue;
                float distance=delta.x*delta.x+delta.z*delta.z;
                if(point.y>landing.y+.001f||Mathf.Abs(point.y-landing.y)<.001f&&distance<nearest){landing=point;nearest=distance;found=true;}
            }
        }
        return found;
    }
    void Update(){
        if(!Application.isPlaying||!board||!tile||!board.player)return;
        // A new arrival must leave the cell before it can activate again.
        if(board.Busy||!board.enabled)return;
        if(board.PlayerCell!=Cell){lastOutside=board.PlayerCell;armed=true;return;}
        if(!armed||board.Busy||!board.enabled||BlackHoleTravel.Selecting)return;
        EntryDirection=Cell-lastOutside;
        armed=false;BlackHoleTravel.Begin(this);
    }
    public void Disarm(){armed=false;}
}
}
