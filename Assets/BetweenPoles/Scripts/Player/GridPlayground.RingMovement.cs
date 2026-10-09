using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    bool RingCanEnter(MagnetPiece ring,Vector2Int from,float height,Vector2Int cell,out float landing){
        return TravelSurface(from,height,cell,ring,out landing)
            &&!TransportObstacle(cell,ring)&&cell!=PlayerCell;
    }
    bool BeginPushRing(MagnetPiece ring,Vector2Int dir,Vector2Int origin){
        if(!Supported(ring))return Reject("边缘外的磁铁无法直接推动");
        var portal=BlackHolePortal.At(this,origin+dir);if(portal)return BeginPortalPush(ring,portal,dir);
        bool flat=MagnetPiece.FlatU(ring.Pose);
        Vector3 forward=new Vector3(dir.x,0,dir.y);
        // A push along the wheel axle tips the upright ring onto its flat face.
        // A push along the circumference keeps it upright and rolling.
        bool tip=flat||Mathf.Abs(Vector3.Dot(ring.Pose*Vector3.up,forward))>.95f;
        float height;
        if(!RingCanEnter(ring,origin,ring.transform.position.y,origin+dir,out height))return Reject("圆环前方有障碍、磁铁或不同高度的地面，无法推动");
        int cells=1;
        // Roll off the last supported cell, then hover at the first gap.
        if(!tip)while(cells<tiles.Length+magnets.Length*2){
            var cell=origin+dir*cells;float nextHeight;
            if(BlackHolePortal.At(this,cell+dir))break;
            if(!(Floor(cell)||Deck(cell,ring))||!RingCanEnter(ring,cell,height,cell+dir,out nextHeight))break;
            height=nextHeight;cells++;
        }
        Busy=true;StartMovement(MoveRing(ring,dir,origin,cells,tip));
        LastRule=flat?"圆环向前一格并推立；推出边缘后悬停":tip?"圆环向推动方向倒下一格，恢复平躺；推出边缘后悬停":"圆环连续滚动，在障碍前或边缘外第一格停下";
        return true;
    }
    IEnumerator MoveRing(MagnetPiece ring,Vector2Int dir,Vector2Int origin,int cells,bool tip){
        BeginPushInterrupt(dir);
        pushHoverParticipants.Add(ring);
        Vector3 start=ring.transform.position;
        var path=new Vector3[cells+1];path[0]=start;
        for(int i=1;i<=cells;i++){
            float height;TravelSurface(origin+dir*(i-1),path[i-1].y,origin+dir*i,ring,out height);
            path[i]=Position(origin+dir*i,height);
        }
        Vector3 end=path[cells];
        Vector3 playerStart=player.position,playerEnd=PushPlayerEnd(origin,ring);
        Vector3 forward=new Vector3(dir.x,0,dir.y),axis=new Vector3(dir.y,0,-dir.x);
        Quaternion pose=ring.Pose;
        var bounds=AssemblyBounds(AssemblyBodies(ring.geometry));
        float radius=Mathf.Max(.1f,bounds.size.y*.5f);
        float degrees=tip?90:cellSize*cells/radius*Mathf.Rad2Deg;
        float seconds=Mathf.Max(.7f,uRollSeconds)*(tip?1:cells);
        ring.walkable=false;player.rotation=Quaternion.LookRotation(forward);
        var pushPose=PlayerPushPose.Begin(player);
        // One continuous interpolation prevents a stop/start at every grid line.
        for(float elapsed=0;elapsed<seconds;elapsed+=MovementDeltaTime){
            float progress=Mathf.Clamp01(elapsed/seconds);
            if(tip)progress=Mathf.SmoothStep(0,1,progress);
            float distance=progress*cells;int segment=Mathf.Min(cells-1,Mathf.FloorToInt(distance));
            ring.transform.position=Vector3.Lerp(path[segment],path[segment+1],distance-segment);
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
