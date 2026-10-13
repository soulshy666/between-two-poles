using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    public enum HeightMove { Walk, Up, Down, Blocked }
    public struct StandingSurface {
        public Vector2Int cell;
        public Vector3 point;
        public Object owner;
        public bool valid,canJump;
    }
    // Authored normal-ground datum, independent of the displayed curved world.
    public float normalGroundHeight;
    StandingSurface standingSurface;
    public StandingSurface PlayerSupport { get { if(!Busy)RefreshPlayerSupport();return standingSurface; } }

    // The single height policy. Drop equality is deliberately forbidden.
    public static HeightMove EvaluateHeight(float from,float to,bool canJump,float layer){
        float delta=to-from;
        if(float.IsNaN(delta)||float.IsInfinity(delta)||layer<=0)return HeightMove.Blocked;
        if(Mathf.Abs(delta)<.0001f)return HeightMove.Walk;
        if(delta>0)return canJump&&delta<=layer+.00001f?HeightMove.Up:HeightMove.Blocked;
        return -delta<layer-.00001f?HeightMove.Down:HeightMove.Blocked;
    }
    StandingSurface MakeSurface(Vector2Int cell,Vector3 point,Object owner){
        return new StandingSurface{cell=cell,point=point,owner=owner,valid=true,
            canJump=point.y>normalGroundHeight+.01f};
    }
    // Geometry is adapted here; movement never branches on rock vs magnet heights.
    public bool TryLandingSurface(Vector2Int cell,out GridTile tile,out Vector3 point){
        bool found=BlackHolePortal.TryGeometrySurface(this,cell,out tile,out point);
        var deck=Deck(cell);var occupant=Piece(cell);
        if(deck&&occupant==deck){
            float height=DeckHeight(deck);
            if(!tile||(!tile.blocked&&height>=tile.surfaceHeight-.0001f)){
                point=Support(cell,point);found=true;
            }
        }
        return found;
    }
    bool TryStandingSurface(Vector2Int cell,out StandingSurface surface){
        surface=default;
        GridTile tile;Vector3 point;
        if(!TryLandingSurface(cell,out tile,out point))return false;
        var magnet=Piece(cell);
        surface=MakeSurface(cell,point,magnet?(Object)magnet:tile);
        return true;
    }
    void RefreshPlayerSupport(){
        if(!player)return;
        var cell=Cell(player);StandingSurface surface;
        if(TryStandingSurface(cell,out surface)&&SameHeight(surface.point.y,player.position.y)){
            standingSurface=surface;return;
        }
        // Ground underneath a magnet is a distinct support, not its top.
        var tile=Tile(cell);
        if(tile&&!tile.blocked&&SameHeight(tile.surfaceHeight,player.position.y)){
            standingSurface=MakeSurface(cell,Position(cell,tile.surfaceHeight),tile);return;
        }
        // A lift provides a virtual support at its endpoint, between grid centres.
        var device=standingSurface.owner as MagnetPiece;
        if(device&&standingSurface.valid&&standingSurface.cell==cell
            &&SameHeight(standingSurface.point.y,player.position.y)
            &&(device.product==MagnetProduct.Lift||device.product==MagnetProduct.Cross))return;
        standingSurface=default;
    }
    void CommitLiftSupport(MagnetPiece device){
        standingSurface=MakeSurface(Cell(player),player.position,device);
        NotifyLanding(Cell(player));
    }
}
}
