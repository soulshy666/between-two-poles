using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    const float MagnetHoverAmplitude=.09f,MagnetHoverPeriod=2.8f;
    readonly Dictionary<MagnetPiece,float> hoveringMagnets=new Dictionary<MagnetPiece,float>();
    readonly HashSet<MagnetPiece> pushHoverParticipants=new HashSet<MagnetPiece>();
    readonly Dictionary<Transform,Vector3> hoverRenderPositions=new Dictionary<Transform,Vector3>();
    readonly List<MagnetPiece> expiredHovers=new List<MagnetPiece>();
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
        expiredHovers.Clear();
        foreach(var entry in hoveringMagnets)
            if(!ShouldHover(entry.Key))expiredHovers.Add(entry.Key);
        foreach(var m in expiredHovers)hoveringMagnets.Remove(m);
        foreach(var m in magnets){
            if(!ShouldHover(m))continue;
            if(!hoveringMagnets.ContainsKey(m))hoveringMagnets.Add(m,0);
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
            float offset=MagnetHoverAmplitude*Mathf.Sin(t*Mathf.PI*2/MagnetHoverPeriod)
                *Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.4f));
            var geometry=m.geometry;
            hoverRenderPositions[geometry]=geometry.position;
            geometry.position+=Vector3.up*offset;
        }
    }
    // Render-only offsets never enter movement, assembly bounds or undo snapshots.
    void RestoreMagnetHover(Camera camera){
        foreach(var entry in hoverRenderPositions)if(entry.Key)entry.Key.position=entry.Value;
        hoverRenderPositions.Clear();
    }
    void ClearMagnetHover(){
        Camera.onPreCull-=RenderMagnetHover;Camera.onPostRender-=RestoreMagnetHover;
        RestoreMagnetHover(null);hoveringMagnets.Clear();
    }
}
}
