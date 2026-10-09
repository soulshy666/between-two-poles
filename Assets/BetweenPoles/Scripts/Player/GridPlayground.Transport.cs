using UnityEngine;
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
    bool WideBarEndConnected(MagnetPiece bridge,Vector2Int direction){
        var cell=Cell(bridge.transform);float baseHeight=bridge.transform.position.y,top=DeckHeight(bridge);
        // Follow end-to-end spans to a shore. A sideways bar, unfinished bridge,
        // height mismatch or open end cannot anchor a walkable connection.
        for(int i=0;i<=magnets.Length*2;i++){
            cell+=direction;var tile=Tile(cell);
            if(tile)return !tile.blocked&&(SameHeight(tile.surfaceHeight,baseHeight)||SameHeight(tile.surfaceHeight,top));
            var deck=Deck(cell,bridge,true);
            if(!deck||!AlongDeck(deck,direction)||!SameHeight(DeckHeight(deck),top)
                ||(deck.product!=MagnetProduct.WideBar&&deck.product!=MagnetProduct.Bridge
                    &&!(deck.product==MagnetProduct.None&&deck.combined&&deck.shape==MagnetShape.Bar)))return false;
        }
        return false;
    }
    bool WideBarConnected(MagnetPiece bridge){
        var axis=Direction(bridge.Pose*Vector3.right);
        return AlongDeck(bridge,axis)&&WideBarEndConnected(bridge,axis)&&WideBarEndConnected(bridge,-axis);
    }
    bool BridgePassage(Vector2Int from,Vector2Int to,MagnetPiece moving=null){
        var direction=to-from;var source=Deck(from,moving,true);var destination=Deck(to,moving,true);
        return (!GapWideBar(source)||(AlongDeck(source,direction)&&WideBarConnected(source)))
            &&(!GapWideBar(destination)||(AlongDeck(destination,direction)&&WideBarConnected(destination)));
    }
    // A deck and the material travelling on it share a cell, but not a layer.
    MagnetPiece Deck(Vector2Int cell,MagnetPiece ignore=null,bool fixedOnly=false){
        MagnetPiece result=null;
        foreach(var m in magnets)if(m&&m!=ignore&&m.enabled&&m.gameObject.activeInHierarchy&&m.walkable&&(!fixedOnly||!m.CanBePushed)&&m.Occupies(cell,cellSize))
            if(!result||DeckHeight(m)>DeckHeight(result))result=m;
        return result;
    }
    bool ShoreLevelBridge(MagnetPiece deck){
        if(!deck||!deck.walkable)return false;
        var cell=Cell(deck.transform);
        return deck.product==MagnetProduct.WideBar&&!Tile(cell)
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
        var cell=Cell(m.transform);var tile=Tile(cell);var deck=Deck(cell,m);
        return (tile&&!tile.blocked&&SameHeight(tile.surfaceHeight,m.transform.position.y))
            ||(deck&&(!GapWideBar(deck)||WideBarConnected(deck))&&SameHeight(DeckHeight(deck),m.transform.position.y));
    }
    bool TravelSurface(Vector2Int from,float height,Vector2Int to,MagnetPiece moving,out float landing){
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
