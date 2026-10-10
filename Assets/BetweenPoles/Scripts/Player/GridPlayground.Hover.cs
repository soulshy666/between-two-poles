using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    const float MagnetHoverAmplitude=.09f,MagnetHoverPeriod=2.8f;
    readonly Dictionary<MagnetPiece,float> hoveringMagnets=new Dictionary<MagnetPiece,float>();
    readonly HashSet<MagnetPiece> pushHoverParticipants=new HashSet<MagnetPiece>();
    readonly Dictionary<Transform,Vector3> hoverRenderPositions=new Dictionary<Transform,Vector3>();
    readonly List<MagnetPiece> expiredHovers=new List<MagnetPiece>();
    readonly Dictionary<MagnetPiece,float> hoverLevels=new Dictionary<MagnetPiece,float>();
    readonly List<Vector3> hoverVertices=new List<Vector3>();
    readonly Dictionary<Vector2Int,MagnetPiece> hoverNeighbors=new Dictionary<Vector2Int,MagnetPiece>();
    void IndexHoverNeighbors(){
        hoverNeighbors.Clear();
        foreach(var m in magnets){
            if(!m||!m.enabled||!m.gameObject.activeInHierarchy||IsKnockedFlying(m))continue;
            var cell=Cell(m.transform);IndexHoverCell(cell,m);
            if(m.product==MagnetProduct.Bridge||m.product==MagnetProduct.BridgeHalf)IndexHoverCell(cell+m.bridgeDirection,m);
        }
    }
    void IndexHoverCell(Vector2Int cell,MagnetPiece m){
        hoverNeighbors.TryGetValue(cell,out var previous);
        if(!previous||m.transform.position.y>previous.transform.position.y+.01f
            ||(SameHeight(m.transform.position.y,previous.transform.position.y)&&previous.walkable&&!m.walkable))hoverNeighbors[cell]=m;
    }
    float EdgeHoverLevel(MagnetPiece m){
        if(ShoreLevelBridge(m))return DeckHeight(m)-MagnetGeometryTop(m);
        if(m.combined||m.product!=MagnetProduct.None)return 0;
        var cell=Cell(m.transform);
        foreach(var dir in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down}){
            hoverNeighbors.TryGetValue(cell+dir,out var neighbor);
            if(neighbor&&!neighbor.combined&&neighbor.product==MagnetProduct.None&&neighbor.north==m.north
                &&SameHeight(neighbor.transform.position.y,m.transform.position.y)&&Supported(neighbor))return 0;
        }
        // Chapter meshes deliberately enlarge their culling bounds for the planet
        // shader. Measure actual vertices, never Renderer.bounds or Mesh.bounds.
        // Logical Y remains the shore level; only the displayed body sits lower.
        return m.transform.position.y-MagnetGeometryTop(m);
    }
    float MagnetGeometryTop(MagnetPiece m){
        float top=float.NegativeInfinity;
        foreach(var filter in m.geometry.GetComponentsInChildren<MeshFilter>()){
            if(!filter.sharedMesh)continue;
            var renderer=filter.GetComponent<Renderer>();if(renderer&&!renderer.enabled)continue;
            filter.sharedMesh.GetVertices(hoverVertices);
            // Read the transform once per mesh, not once for every vertex.
            var matrix=filter.transform.localToWorldMatrix;
            foreach(var vertex in hoverVertices)top=Mathf.Max(top,matrix.m10*vertex.x+matrix.m11*vertex.y+matrix.m12*vertex.z+matrix.m13);
        }
        return float.IsNegativeInfinity(top)?m.transform.position.y:top;
    }
    void OnEnable(){Camera.onPreCull+=RenderMagnetHover;Camera.onPostRender+=RestoreMagnetHover;}
    bool HoverPassage(MagnetPiece m){
        if(!m.walkable)return false;
        if(m.product==MagnetProduct.WideBar||m.product==MagnetProduct.None)return WideBarConnected(m);
        if(m.product==MagnetProduct.Bridge)
            return HoverEndConnected(m,m.bridgeDirection)&&HoverEndConnected(m,-m.bridgeDirection);
        if(m.product==MagnetProduct.Ring)
            return (HoverEndConnected(m,Vector2Int.right)&&HoverEndConnected(m,Vector2Int.left))
                ||(HoverEndConnected(m,Vector2Int.up)&&HoverEndConnected(m,Vector2Int.down));
        return false;
    }
    bool HoverEndConnected(MagnetPiece m,Vector2Int direction){
        var cell=Cell(m.transform);
        // Start beyond the outer socket of a two-cell bridge.
        while(m.Occupies(cell+direction,cellSize))cell+=direction;
        for(int i=0;i<=magnets.Length*2;i++){
            cell+=direction;var tile=Tile(cell);
            if(tile)return !tile.blocked&&(SameHeight(tile.surfaceHeight,m.transform.position.y)
                ||SameHeight(tile.surfaceHeight,DeckHeight(m)));
            var deck=Deck(cell,m,true);
            if(!deck||!AlongDeck(deck,direction)||!SameHeight(DeckHeight(deck),DeckHeight(m))
                ||(deck.product!=MagnetProduct.WideBar&&deck.product!=MagnetProduct.Bridge
                    &&!(deck.product==MagnetProduct.None&&deck.combined&&deck.shape==MagnetShape.Bar)))return false;
        }
        return false;
    }
    bool ShouldHover(MagnetPiece m){
        return m&&m.enabled&&m.gameObject.activeInHierarchy&&m.geometry&&m.geometry.gameObject.activeInHierarchy
            &&!Supported(m)&&!HoverPassage(m)&&!MagnetStillAnimating(m);
    }
    void UpdateMagnetHover(){
        RestoreMagnetHover(null);
        IndexHoverNeighbors();
        expiredHovers.Clear();
        foreach(var entry in hoveringMagnets)
            if(!ShouldHover(entry.Key))expiredHovers.Add(entry.Key);
        foreach(var m in expiredHovers){hoveringMagnets.Remove(m);hoverLevels.Remove(m);}
        foreach(var m in magnets){
            if(!ShouldHover(m))continue;
            if(!hoveringMagnets.ContainsKey(m))hoveringMagnets.Add(m,0);
            float target=EdgeHoverLevel(m);
            if(!hoverLevels.ContainsKey(m))hoverLevels[m]=0;
            hoverLevels[m]=Mathf.MoveTowards(hoverLevels[m],target,Time.deltaTime*.8f);
            // Docking/rolling has priority; restart gently once the push settles.
            hoveringMagnets[m]=Busy&&interruptiblePush&&pushHoverParticipants.Contains(m)
                ?0:hoveringMagnets[m]+Time.deltaTime;
        }
    }
    void RenderMagnetHover(Camera camera){
        RestoreMagnetHover(null);
        if(!Application.isPlaying||!isActiveAndEnabled)return;
        foreach(var entry in hoveringMagnets){
            var m=entry.Key;if(!m||!m.geometry||!m.gameObject.activeInHierarchy)continue;
            float t=entry.Value;
            float offset=(hoverLevels.TryGetValue(m,out float level)?level:0)+MagnetHoverAmplitude*Mathf.Sin(t*Mathf.PI*2/MagnetHoverPeriod)
                *Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.4f));
            var geometry=m.geometry;
            hoverRenderPositions[geometry]=geometry.position;
            geometry.position+=Vector3.up*offset;
        }
        // Connected bridges do not bob, but their deck must still sit flush with
        // the shore. Keep this display offset out of assembly and undo data.
        foreach(var m in magnets){
            if(!m||!m.enabled||!m.gameObject.activeInHierarchy||!m.geometry
                ||hoveringMagnets.ContainsKey(m)||!ShoreLevelBridge(m)||MagnetStillAnimating(m))continue;
            float offset=DeckHeight(m)-MagnetGeometryTop(m);
            hoverRenderPositions[m.geometry]=m.geometry.position;
            m.geometry.position+=Vector3.up*offset;
        }
    }
    // Render-only offsets never enter movement, assembly bounds or undo snapshots.
    void RestoreMagnetHover(Camera camera){
        foreach(var entry in hoverRenderPositions)if(entry.Key)entry.Key.position=entry.Value;
        hoverRenderPositions.Clear();
    }
    void ClearMagnetHover(){
        Camera.onPreCull-=RenderMagnetHover;Camera.onPostRender-=RestoreMagnetHover;
        RestoreMagnetHover(null);hoveringMagnets.Clear();hoverLevels.Clear();
    }
}
}
