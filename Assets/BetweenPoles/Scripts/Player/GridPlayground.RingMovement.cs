using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    bool RingCanEnter(MagnetPiece ring,Vector2Int cell){
        var tile=Tile(cell);
        return tile&&!tile.blocked&&SameHeight(tile.surfaceHeight,ring.transform.position.y)
            &&!Piece(cell,ring)&&cell!=PlayerCell;
    }
    bool BeginPushRing(MagnetPiece ring,Vector2Int dir,Vector2Int origin){
        bool flat=MagnetPiece.FlatU(ring.Pose);
        Vector3 forward=new Vector3(dir.x,0,dir.y);
        // A push along the wheel axle tips the upright ring onto its flat face.
        // A push along the circumference keeps it upright and rolling.
        bool tip=flat||Mathf.Abs(Vector3.Dot(ring.Pose*Vector3.up,forward))>.95f;
        if(!RingCanEnter(ring,origin+dir))return Reject("圆环前方没有同高空地，无法推动");
        int cells=1;
        if(!tip)while(cells<tiles.Length&&RingCanEnter(ring,origin+dir*(cells+1)))cells++;
        Busy=true;StartCoroutine(MoveRing(ring,dir,origin,cells,tip));
        LastRule=flat?"圆环向前一格并推立；从圆周侧再推可连续滚动":tip?"圆环向推动方向倒下一格，恢复平躺":"圆环沿推动方向连续滚动，到障碍或地面边缘前停下";
        return true;
    }
    IEnumerator MoveRing(MagnetPiece ring,Vector2Int dir,Vector2Int origin,int cells,bool tip){
        Vector3 start=ring.transform.position,end=Position(origin+dir*cells,start.y);
        Vector3 playerStart=player.position,playerEnd=Position(origin,start.y);
        Vector3 forward=new Vector3(dir.x,0,dir.y),axis=new Vector3(dir.y,0,-dir.x);
        Quaternion pose=ring.Pose;
        var bounds=AssemblyBounds(AssemblyBodies(ring.geometry));
        float radius=Mathf.Max(.1f,bounds.size.y*.5f);
        float degrees=tip?90:cellSize*cells/radius*Mathf.Rad2Deg;
        float seconds=Mathf.Max(.7f,uRollSeconds)*(tip?1:cells);
        ring.walkable=false;player.rotation=Quaternion.LookRotation(forward);
        // One continuous interpolation prevents a stop/start at every grid line.
        for(float elapsed=0;elapsed<seconds;elapsed+=Time.deltaTime){
            float progress=Mathf.Clamp01(elapsed/seconds);
            if(tip)progress=Mathf.SmoothStep(0,1,progress);
            ring.transform.position=Vector3.Lerp(start,end,progress);
            ring.geometry.rotation=Quaternion.AngleAxis(degrees*progress,axis)*pose;
            MagnetVisuals.Ground(ring);
            player.position=Vector3.Lerp(playerStart,playerEnd,Mathf.SmoothStep(0,1,Mathf.Clamp01(progress*cells)));
            yield return null;
        }
        ring.transform.position=end;ring.geometry.rotation=Quaternion.AngleAxis(degrees,axis)*pose;
        MagnetVisuals.Ground(ring);ring.walkable=MagnetPiece.FlatU(ring.Pose);
        player.position=playerEnd;NotifyLanding(origin);Busy=false;
    }
}
}
