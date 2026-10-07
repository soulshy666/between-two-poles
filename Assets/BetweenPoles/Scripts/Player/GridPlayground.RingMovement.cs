using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    bool RingCanEnter(MagnetPiece ring,Vector2Int cell){
        var tile=Tile(cell);
        return (!tile||(!tile.blocked&&SameHeight(tile.surfaceHeight,ring.transform.position.y)))
            &&!Piece(cell,ring)&&cell!=PlayerCell;
    }
    bool BeginPushRing(MagnetPiece ring,Vector2Int dir,Vector2Int origin){
        var support=Tile(origin);
        if(!support||support.blocked||!SameHeight(support.surfaceHeight,ring.transform.position.y))return Reject("边缘外的磁铁无法直接推动");
        bool flat=MagnetPiece.FlatU(ring.Pose);
        Vector3 forward=new Vector3(dir.x,0,dir.y);
        // A push along the wheel axle tips the upright ring onto its flat face.
        // A push along the circumference keeps it upright and rolling.
        bool tip=flat||Mathf.Abs(Vector3.Dot(ring.Pose*Vector3.up,forward))>.95f;
        if(!RingCanEnter(ring,origin+dir))return Reject("圆环前方有障碍、磁铁或不同高度的地面，无法推动");
        int cells=1;
        // Roll off the last supported cell, then hover at the first gap.
        if(!tip)while(cells<tiles.Length&&Floor(origin+dir*cells)&&RingCanEnter(ring,origin+dir*(cells+1)))cells++;
        Busy=true;StartCoroutine(MoveRing(ring,dir,origin,cells,tip));
        LastRule=flat?"圆环向前一格并推立；推出边缘后悬停":tip?"圆环向推动方向倒下一格，恢复平躺；推出边缘后悬停":"圆环连续滚动，在障碍前或边缘外第一格停下";
        return true;
    }
    IEnumerator MoveRing(MagnetPiece ring,Vector2Int dir,Vector2Int origin,int cells,bool tip){
        BeginPushInterrupt(dir);
        Vector3 start=ring.transform.position,end=Position(origin+dir*cells,start.y);
        Vector3 playerStart=player.position,playerEnd=Position(origin,start.y);
        Vector3 forward=new Vector3(dir.x,0,dir.y),axis=new Vector3(dir.y,0,-dir.x);
        Quaternion pose=ring.Pose;
        var bounds=AssemblyBounds(AssemblyBodies(ring.geometry));
        float radius=Mathf.Max(.1f,bounds.size.y*.5f);
        float degrees=tip?90:cellSize*cells/radius*Mathf.Rad2Deg;
        float seconds=Mathf.Max(.7f,uRollSeconds)*(tip?1:cells);
        ring.walkable=false;player.rotation=Quaternion.LookRotation(forward);
        var pushPose=PlayerPushPose.Begin(player);
        // One continuous interpolation prevents a stop/start at every grid line.
        for(float elapsed=0;elapsed<seconds;elapsed+=Time.deltaTime){
            float progress=Mathf.Clamp01(elapsed/seconds);
            if(tip)progress=Mathf.SmoothStep(0,1,progress);
            ring.transform.position=Vector3.Lerp(start,end,progress);
            ring.geometry.rotation=Quaternion.AngleAxis(degrees*progress,axis)*pose;
            MagnetVisuals.Ground(ring);
            float pushProgress=Mathf.Clamp01(progress*cells);
            player.position=Vector3.Lerp(playerStart,playerEnd,pushProgress);
            pushPose.Sample(pushProgress);
            yield return null;
        }
        ring.transform.position=end;ring.geometry.rotation=Quaternion.AngleAxis(degrees,axis)*pose;
        MagnetVisuals.Ground(ring);ring.walkable=MagnetPiece.FlatU(ring.Pose);
        EndPushInterrupt();pushPose.End();player.position=playerEnd;NotifyLanding(origin);Busy=false;
    }
}
}
