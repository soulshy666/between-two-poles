using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public partial class CurvedTrialCamera {
    readonly Dictionary<Vector2Int,Transform> framingOwners=new Dictionary<Vector2Int,Transform>();
    readonly Dictionary<Transform,float> neighborFrameSizes=new Dictionary<Transform,float>();
    readonly Dictionary<GridTile,Transform> framingTileOwners=new Dictionary<GridTile,Transform>();
    GridTile[] framingTiles;
    GridPlayground framingBoard;
    struct TileFrame {
        public GridTile tile;
        public Transform owner;
        public Vector3 position;
        public float height;
        public bool active;
    }
    TileFrame[] frameTiles;
    Transform[] frameCenters;
    Vector3[] frameCenterPositions;
    GridPlayground frameBoard;
    FiveIslandWindow frameWindow;
    Quaternion frameRotation;
    Vector4 frameSettings;
    float frameCell;
    bool framePlanar;
    // Check mutable authored inputs at the existing 4 Hz cadence. Stable layouts
    // reuse the exact previous framing result, including neighboring-shore BFS.
    // Sampling values (rather than only an array identity) covers debug height edits,
    // active tiles, ownership changes, undo and additive chapter rebasing.
    bool FramingInputsChanged(Transform[] centers,int count,float scale,float aspect,float size,float margin,FiveIslandWindow window){
        var board=island.board;var settings=new Vector4(scale,aspect,size,margin);
        bool changed=frameBoard!=board||frameWindow!=window||!frameRotation.Equals(transform.rotation)||
            !frameSettings.Equals(settings)||frameCell!=board.cellSize||framePlanar!=PlanarChapter;
        frameBoard=board;frameWindow=window;frameRotation=transform.rotation;frameSettings=settings;frameCell=board.cellSize;framePlanar=PlanarChapter;
        if(frameCenters==null||frameCenters.Length!=count){frameCenters=new Transform[count];frameCenterPositions=new Vector3[count];changed=true;}
        for(int i=0;i<count;i++){
            var position=centers[i]?centers[i].position:Vector3.zero;
            if(frameCenters[i]!=centers[i]||!frameCenterPositions[i].Equals(position))changed=true;
            frameCenters[i]=centers[i];frameCenterPositions[i]=position;
        }
        if(frameTiles==null||frameTiles.Length!=board.tiles.Length){frameTiles=new TileFrame[board.tiles.Length];changed=true;}
        for(int i=0;i<board.tiles.Length;i++){
            var tile=board.tiles[i];var next=new TileFrame{tile=tile};
            if(tile){
                next.active=tile.gameObject.activeInHierarchy;next.position=tile.transform.position;next.height=tile.surfaceHeight;
                var binding=tile.GetComponentInParent<IslandSurfaceAnchor>();next.owner=binding?binding.center:null;
            }
            var old=frameTiles[i];
            if(old.tile!=next.tile||old.owner!=next.owner||old.active!=next.active||old.height!=next.height||!old.position.Equals(next.position))changed=true;
            frameTiles[i]=next;
        }
        if(changed){framingTiles=null;framingTileOwners.Clear();}
        return changed;
    }
    // Only fit a small patch of each nearby shore, not the entirety of a distant island.
    float FitNearbyIslandEdges(Transform current,Vector3 center,float scale,float aspect,float size){
        if(!island||!island.board||!current)return size;
        var board=island.board;float cell=board.cellSize;
        if(framingTiles!=board.tiles||framingBoard!=board){
            framingTileOwners.Clear();framingTiles=board.tiles;framingBoard=board;
            foreach(var tile in board.tiles)if(tile){var anchor=tile.GetComponentInParent<IslandSurfaceAnchor>();if(anchor&&anchor.center)framingTileOwners[tile]=anchor.center;}
        }
        framingOwners.Clear();neighborFrameSizes.Clear();
        foreach(var pair in framingTileOwners){var tile=pair.Key;if(!tile||!tile.gameObject.activeInHierarchy||!pair.Value)continue;var p=tile.transform.position;framingOwners[new Vector2Int(Mathf.RoundToInt(p.x/cell),Mathf.RoundToInt(p.z/cell))]=pair.Value;}
        // Always two empty cells in every chapter; exploration shading has its own rules.
        var neighbors=IslandVisibility.Neighbors(framingOwners,current,2);
        float inset=cell*scale*.22f;
        float halfWidth=cell*scale*.5f*(Mathf.Abs(transform.right.x)+Mathf.Abs(transform.right.z));
        float halfHeight=cell*scale*.5f*(Mathf.Abs(transform.up.x)+Mathf.Abs(transform.up.z));
        foreach(var pair in framingTileOwners){
            var tile=pair.Key;if(!tile||!tile.gameObject.activeInHierarchy||!neighbors.Contains(pair.Value))continue;
            var p=tile.transform.position;p.y=tile.surfaceHeight;
            var delta=(p-center)*scale;
            float x=Mathf.Max(0,Mathf.Abs(Vector3.Dot(transform.right,delta))-halfWidth+inset);
            float y=Mathf.Max(0,Mathf.Abs(Vector3.Dot(transform.up,delta))-halfHeight+inset);
            // Keep the glimpse away from the outer HUD area as well as the viewport edge.
            float needed=Mathf.Max(y/.84f,x/(Mathf.Max(.1f,aspect)*.92f));
            if(!neighborFrameSizes.TryGetValue(pair.Value,out var previous)||needed<previous)neighborFrameSizes[pair.Value]=needed;
        }
        foreach(var required in neighborFrameSizes.Values)size=Mathf.Max(size,required);
        return size;
    }
}
}
