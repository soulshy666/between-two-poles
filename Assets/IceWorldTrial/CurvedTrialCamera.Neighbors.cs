using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public partial class CurvedTrialCamera {
    readonly Dictionary<Vector2Int,Transform> framingOwners=new Dictionary<Vector2Int,Transform>();
    readonly Dictionary<Transform,float> neighborFrameSizes=new Dictionary<Transform,float>();
    readonly Dictionary<GridTile,Transform> framingTileOwners=new Dictionary<GridTile,Transform>();
    GridTile[] framingTiles;
    GridPlayground framingBoard;
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
