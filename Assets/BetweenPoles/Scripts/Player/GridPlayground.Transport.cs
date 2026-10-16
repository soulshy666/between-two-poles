using UnityEngine;
using System.Collections.Generic;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    bool GapWideBar(MagnetPiece m){
        return m&&!Tile(Cell(m.transform))&&(m.product==MagnetProduct.WideBar
            ||(m.product==MagnetProduct.None&&m.combined&&m.walkable&&m.shape==MagnetShape.Bar));
    }
    bool AlongDeck(MagnetPiece deck,Vector2Int direction){
        var axis=deck.product==MagnetProduct.Bridge
            ?new Vector3(deck.bridgeDirection.x,0,deck.bridgeDirection.y):deck.Pose*Vector3.right;
        return Mathf.Abs(Vector3.Dot(axis.normalized,new Vector3(direction.x,0,direction.y)))>.95f;
    }
    bool WideBarEndConnected(MagnetPiece bridge,Vector2Int direction,HashSet<MagnetPiece> visiting){
        var cell=Cell(bridge.transform);float baseHeight=bridge.transform.position.y,top=DeckHeight(bridge);
        while(bridge.Occupies(cell+direction,cellSize))cell+=direction;
        // Follow spans to a shore or an independently connected crossing span.
        for(int i=0;i<=magnets.Length*2;i++){
            cell+=direction;var tile=Tile(cell);
            // A rock blocks ground walking, but its solid shore still anchors
            // a completed span. Access from its top is checked by TryStepCore.
            if(tile)return SameHeight(tile.surfaceHeight,baseHeight)||SameHeight(tile.surfaceHeight,top);
            // Settled space rings are shores in every direction, even though
            // rings remain pushable objects elsewhere in the movement system.
            foreach(var ring in magnets)if(RingShore(ring)&&ring.Occupies(cell,cellSize)
                &&SameHeight(DeckHeight(ring),top))return true;
            var deck=Deck(cell,bridge,true);
            if(!deck||visiting.Contains(deck)||!SameHeight(DeckHeight(deck),top)
                ||(deck.product!=MagnetProduct.WideBar&&deck.product!=MagnetProduct.Bridge
                    &&!(deck.product==MagnetProduct.None&&deck.combined&&deck.shape==MagnetShape.Bar)))return false;
            // A completed crosswise span can anchor a branch at its side.
            // It must reach its own shores without relying on this branch.
            if(!AlongDeck(deck,direction))return ConnectedSpan(deck,visiting);
        }
        return false;
    }
    bool WideBarConnected(MagnetPiece bridge){
        return ConnectedSpan(bridge,new HashSet<MagnetPiece>());
    }
    bool ConnectedSpan(MagnetPiece bridge,HashSet<MagnetPiece> visiting){
        if(!bridge||!bridge.walkable||(bridge.product!=MagnetProduct.WideBar&&bridge.product!=MagnetProduct.Bridge
            &&!(bridge.product==MagnetProduct.None&&bridge.combined&&bridge.shape==MagnetShape.Bar)))return false;
        if(!visiting.Add(bridge))return false;
        var axis=bridge.product==MagnetProduct.Bridge?bridge.bridgeDirection:Direction(bridge.Pose*Vector3.right);
        bool connected=AlongDeck(bridge,axis)&&WideBarEndConnected(bridge,axis,visiting)&&WideBarEndConnected(bridge,-axis,visiting);
        visiting.Remove(bridge);return connected;
    }
    bool BridgePassage(Vector2Int from,Vector2Int to,MagnetPiece moving=null){
        var direction=to-from;var source=Deck(from,moving,true);var destination=Deck(to,moving,true);
        bool junction=source&&destination&&source!=destination
            &&SameHeight(DeckHeight(source),DeckHeight(destination))
            &&(AlongDeck(source,direction)||AlongDeck(destination,direction))
            &&WideBarConnected(source)&&WideBarConnected(destination);
        return (!GapWideBar(source)||((AlongDeck(source,direction)||junction)&&TraversableSpan(source)))
            &&(!GapWideBar(destination)||((AlongDeck(destination,direction)||junction)&&TraversableSpan(destination)));
    }
    bool TraversableSpan(MagnetPiece deck){
        // Completed wide bars are usable floating platforms even before reaching a shore.
        return deck&&deck.walkable&&(deck.product==MagnetProduct.WideBar||WideBarConnected(deck));
    }
    // A deck and the material travelling on it share a cell, but not a layer.
    MagnetPiece Deck(Vector2Int cell,MagnetPiece ignore=null,bool fixedOnly=false){
        MagnetPiece result=null;
        foreach(var m in magnets)if(m&&m!=ignore&&m.enabled&&m.gameObject.activeInHierarchy&&m.walkable&&(!fixedOnly||!m.CanBePushed)&&m.Occupies(cell,cellSize))
            if(!result||DeckHeight(m)>DeckHeight(result))result=m;
        return result;
    }
    bool SpaceRing(MagnetPiece ring){
        return ring&&ring.product==MagnetProduct.Ring&&!Tile(Cell(ring.transform));
    }
    bool RingShore(MagnetPiece ring){
        return SpaceRing(ring)&&ring.enabled&&ring.gameObject.activeInHierarchy&&ring.geometry
            &&ring.walkable&&MagnetPiece.FlatU(ring.Pose)&&!MagnetStillAnimating(ring);
    }
    bool ShoreLevelBridge(MagnetPiece deck){
        if(!deck||!deck.walkable)return false;
        var cell=Cell(deck.transform);
        return SpaceRing(deck)&&MagnetPiece.FlatU(deck.Pose)
            ||deck.product==MagnetProduct.WideBar&&!Tile(cell)
            ||deck.product==MagnetProduct.Bridge&&(!Tile(cell)||!Tile(cell+deck.bridgeDirection));
    }
    float DeckHeight(MagnetPiece deck){
        if(ShoreLevelBridge(deck))return deck.transform.position.y;
        if(deck.product==MagnetProduct.None)foreach(var bridges in FindObjectsOfType<PrejoinedTestBridges>()){
            float height;if(bridges.board==this&&bridges.TryGetShoreLevel(deck,out height))return height;
        }
        return deck.transform.position.y+.24f;
    }
    bool Supported(MagnetPiece m){
        var cell=Cell(m.transform);var tile=Tile(cell);
        if(tile&&!tile.blocked&&SameHeight(tile.surfaceHeight,m.transform.position.y))return true;
        var deck=Deck(cell,m);
        return deck&&(!GapWideBar(deck)||TraversableSpan(deck))&&SameHeight(DeckHeight(deck),m.transform.position.y);
    }
    public enum MagnetLandingKind { Ground, Deck, Portal, Space, Magnet, Blocked }
    public struct MagnetLanding {
        public MagnetLandingKind kind;
        public Vector3 position;
        public BlackHolePortal portal;
        public MagnetPiece obstacle;
    }
    // One read-only destination query for every source of magnetic movement.
    public MagnetLanding InspectMagnetDestination(Vector2Int from,float height,Vector2Int cell,MagnetPiece moving){
        var result=new MagnetLanding{kind=MagnetLandingKind.Blocked,position=Position(cell,height)};
        if(IsWreckCell(cell)||RingRouteReserved(cell))return result;
        float landing;
        if(!TravelSurfaceCore(from,height,cell,moving,out landing))return result;
        result.position=Position(cell,landing);
        result.portal=BlackHolePortal.At(this,cell);
        if(result.portal){
            if(result.portal.CanArrive&&(!result.portal.Cargo||result.portal.Cargo==moving))result.kind=MagnetLandingKind.Portal;
            return result;
        }
        result.obstacle=TransportObstacle(cell,moving);
        result.kind=result.obstacle?MagnetLandingKind.Magnet:Deck(cell,moving)?MagnetLandingKind.Deck:Tile(cell)?MagnetLandingKind.Ground:MagnetLandingKind.Space;
        return result;
    }
    public bool TryPortalKickDestination(Vector2Int from,Vector2Int direction,float height,out Vector3 landing){
        var result=InspectMagnetDestination(from,height,from+direction,null);landing=result.position;
        return result.kind!=MagnetLandingKind.Blocked&&result.kind!=MagnetLandingKind.Magnet;
    }
    bool TravelSurface(Vector2Int from,float height,Vector2Int to,MagnetPiece moving,out float landing){
        var result=InspectMagnetDestination(from,height,to,moving);landing=result.position.y;
        return result.kind!=MagnetLandingKind.Blocked;
    }
    bool TravelSurfaceCore(Vector2Int from,float height,Vector2Int to,MagnetPiece moving,out float landing){
        landing=height;var tile=Tile(to);var deck=Deck(to,moving);var source=Deck(from,moving);
        if((tile&&tile.blocked)||!BridgePassage(from,to,moving))return false;
        // A two-cell bridge is a route only on its unsupported cells, including
        // when a material is pushed across it and the player follows behind.
        if(tile&&deck&&deck.product==MagnetProduct.Bridge)return false;
        bool onDeck=source&&SameHeight(height,DeckHeight(source));
        if(deck){
            landing=DeckHeight(deck);
            if(SameHeight(height,landing))return true;
            // Match player entry rules: an island's raised wide bar is an obstacle.
            bool entry=deck.product!=MagnetProduct.WideBar||!tile;
            return entry&&(SameHeight(height,deck.transform.position.y)
                ||(onDeck&&SameHeight(source.transform.position.y,deck.transform.position.y)));
        }
        if(!tile)return true; // First unsupported cell still permits edge hovering.
        landing=tile.surfaceHeight;
        return SameHeight(height,landing)||(onDeck&&SameHeight(source.transform.position.y,landing));
    }
    MagnetPiece TransportObstacle(Vector2Int cell,MagnetPiece moving){
        var deck=Deck(cell,moving);MagnetPiece result=null;
        foreach(var m in magnets)if(m&&m!=moving&&m!=deck&&m.enabled&&m.gameObject.activeInHierarchy&&m.Occupies(cell,cellSize))
            if(!result||m.transform.position.y>result.transform.position.y)result=m;
        return result;
    }
    float TransportHeight(MagnetPiece m,Vector2Int to){
        float height;TravelSurface(Cell(m.transform),m.transform.position.y,to,m,out height);return height;
    }
    Vector3 PushPlayerEnd(Vector2Int cell,MagnetPiece moving){
        if(cell==Cell(player))return player.position;
        var deck=Deck(cell,moving);var end=Position(cell,deck?DeckHeight(deck):GroundHeight(cell));
        if(deck&&deck.product==MagnetProduct.Ring){
            var side=deck.transform.forward;float sign=Vector3.Dot(player.position-deck.transform.position,side)>=0?1:-1;
            end+=side*.48f*sign;
        }
        return end;
    }
}
}
