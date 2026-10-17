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
    // All mouths on this board share one storage slot. Keep storedInPortal as
    // entry metadata, not ownership: direction, momentum and undo need it.
    public MagnetPiece Cargo {get{if(board)foreach(var m in board.magnets)if(m&&m.storedInPortal&&m.storedInPortal.board==board)return m;return null;}}
    public static BlackHolePortal At(GridPlayground board,Vector2Int cell){var tile=board.TileAt(cell);return tile?tile.GetComponentInChildren<BlackHolePortal>():null;}
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
        return board.TryLandingSurface(cell,out exit,out landing);
    }
    internal static bool TryGeometrySurface(GridPlayground board,Vector2Int cell,out GridTile exit,out Vector3 landing){
        exit=board.TileAt(cell);
        landing=new Vector3(cell.x*board.cellSize,exit?exit.surfaceHeight:float.NegativeInfinity,cell.y*board.cellSize);
        if(board.IsWreckCell(cell))return false;
        bool support=exit&&!exit.blocked;
        if(exit&&exit.blocked){
            var anchor=exit.GetComponentInParent<IslandSurfaceAnchor>(true);
            support|=FindTop(board,exit.transform,cell,ref landing);
            if(anchor)support|=FindTop(board,anchor.transform,cell,ref landing);
        }
        // Portals can place a player on any magnetic shape, including upright pieces.
        foreach(var m in board.magnets)if(m&&m.enabled&&m.gameObject.activeSelf&&m.Occupies(cell,board.cellSize))support|=FindTop(board,m.geometry,cell,ref landing);
        return support;
    }
    static bool FindTop(GridPlayground board,Transform root,Vector2Int cell,ref Vector3 landing){
        bool found=false,centered=false;float nearest=float.PositiveInfinity;Vector3 center=new Vector3(cell.x*board.cellSize,0,cell.y*board.cellSize);
        // Renderer bounds are deliberately enlarged for spherical rendering; use real mesh triangles.
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
            if(!filter.gameObject.activeSelf||filter.transform.IsChildOf(board.player)||filter.GetComponentInParent<BlackHolePortal>(true))continue;
            var mesh=filter.sharedMesh;if(!mesh||!mesh.isReadable)continue;var vertices=mesh.vertices;var indices=mesh.triangles;
            for(int i=0;i<indices.Length;i+=3){
                var a=filter.transform.TransformPoint(vertices[indices[i]]);var b=filter.transform.TransformPoint(vertices[indices[i+1]]);var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.y<.5f)continue;
                var point=ClosestTopPoint(center,a,b,c);var delta=point-center;
                if(Mathf.Abs(delta.x)>board.cellSize*.5f||Mathf.Abs(delta.z)>board.cellSize*.5f)continue;
                float distance=delta.x*delta.x+delta.z*delta.z;
                bool atCenter=distance<.000001f;
                // On a sloped rock, the foothold is directly below the cell centre,
                // not a higher outer edge of another triangle. Rings still use an edge.
                if(centered&&!atCenter)continue;
                if((atCenter&&found&&!centered)||point.y>landing.y+.001f
                    ||Mathf.Abs(point.y-landing.y)<.001f&&distance<nearest){
                    landing=point;nearest=distance;found=true;centered=atCenter;
                }
            }
        }
        return found;
    }
    // Project the cell centre onto the triangle, rather than using its centroid.
    // Outside a triangle, retain the nearest solid edge (important for rings).
    static Vector3 ClosestTopPoint(Vector3 center,Vector3 a,Vector3 b,Vector3 c){
        var ab=new Vector2(b.x-a.x,b.z-a.z);var ac=new Vector2(c.x-a.x,c.z-a.z);
        var p=new Vector2(center.x-a.x,center.z-a.z);
        float determinant=ab.x*ac.y-ab.y*ac.x;
        if(Mathf.Abs(determinant)>.000001f){
            float u=(p.x*ac.y-p.y*ac.x)/determinant;
            float v=(ab.x*p.y-ab.y*p.x)/determinant;
            if(u>=0&&v>=0&&u+v<=1)return a+(b-a)*u+(c-a)*v;
        }
        var best=ClosestTopEdge(center,a,b);
        var next=ClosestTopEdge(center,b,c);
        if(TopDistance(center,next)<TopDistance(center,best))best=next;
        next=ClosestTopEdge(center,c,a);
        return TopDistance(center,next)<TopDistance(center,best)?next:best;
    }
    static float TopDistance(Vector3 a,Vector3 b){float x=a.x-b.x,z=a.z-b.z;return x*x+z*z;}
    static Vector3 ClosestTopEdge(Vector3 center,Vector3 a,Vector3 b){
        var edge=b-a;float length=edge.x*edge.x+edge.z*edge.z;
        float t=length>.000001f?Mathf.Clamp01(((center.x-a.x)*edge.x+(center.z-a.z)*edge.z)/length):0;
        return a+edge*t;
    }
    void Update(){
        if(!Application.isPlaying||!board||!tile||!board.player)return;
        var panel=board.GetComponent<MagnetDebugPanel>();if(panel&&panel.IsOpen)return;
        // A new arrival must leave the cell before it can activate again.
        if(board.Busy||!board.enabled)return;
        if(board.PlayerCell!=Cell){lastOutside=board.PlayerCell;armed=true;return;}
        if(!armed||board.Busy||!board.enabled||BlackHoleTravel.Selecting)return;
        EntryDirection=Cell-lastOutside;
        armed=false;BlackHoleTravel.Begin(this);
    }
    public void Enter(Vector2Int direction){EntryDirection=direction;armed=false;BlackHoleTravel.Begin(this);}
    public void Disarm(){armed=false;}
}
}
