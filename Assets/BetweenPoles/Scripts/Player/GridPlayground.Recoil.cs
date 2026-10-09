using System.Collections;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    public bool RecoilFlying {get;private set;}
    public MagnetPiece RecoilMagnet {get;private set;}
    public Vector3 RecoilOrigin {get;private set;}
    public Vector3 RecoilCameraFocus {get{return RecoilOrigin+(player.position-RecoilOrigin)*Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"));}}
    Renderer[] recoilRenderers;
    MaterialPropertyBlock recoilBlock;
    Vector3 playerFlightOffset,magnetFlightOffset;
    static Vector3 SurfaceOffset(Vector3 p){
        // Use the same flat-main-island / curved-neighbor frame as every renderer.
        Vector3 focus=Shader.GetGlobalVector("_IceFocus");
        return CrashColorReveal.DisplayPoint(p)-(focus+(p-focus)*Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale")));
    }
    void FlightOffset(Transform actor,Vector3 offset){
        if(recoilBlock==null)recoilBlock=new MaterialPropertyBlock();
        foreach(var r in actor.GetComponentsInChildren<Renderer>()){r.GetPropertyBlock(recoilBlock);recoilBlock.SetVector("_RecoilDisplayOffset",offset);r.SetPropertyBlock(recoilBlock);}
    }
    // Search logical cells, including islands currently hidden by the presentation window.
    // The first foreign shore holds the magnet; the next cell inward holds the player.
    public bool FindRecoilLanding(Vector2Int from,Vector2Int travel,out Vector2Int shore,out Vector3 magnetEnd,out Vector3 playerEnd,out int exitDistance){
        shore=from;magnetEnd=playerEnd=Vector3.zero;exitDistance=1;
        var source=Owner(Tile(from));int nearest=int.MaxValue;
        foreach(var tile in tiles){
            if(!tile||!tile.gameObject.activeInHierarchy)continue;
            var delta=Cell(tile.transform)-from;int along=delta.x*travel.x+delta.y*travel.y;
            if(along<1||delta.x*travel.y-delta.y*travel.x!=0)continue;
            if(Owner(tile)==source){exitDistance=Mathf.Max(exitDistance,along+1);continue;}
        }
        foreach(var tile in tiles){
            if(!tile||!tile.gameObject.activeInHierarchy||Owner(tile)==source)continue;
            var cell=Cell(tile.transform);var delta=cell-from;int along=delta.x*travel.x+delta.y*travel.y;
            if(along<exitDistance||along>=nearest||delta.x*travel.y-delta.y*travel.x!=0)continue;
            var inner=Tile(cell+travel);if(!inner||!inner.gameObject.activeInHierarchy||Owner(inner)!=Owner(tile))continue;
            GridTile support;Vector3 outerTop,innerTop;
            if(!BlackHolePortal.TrySurface(this,cell,out support,out outerTop)||!BlackHolePortal.TrySurface(this,cell+travel,out support,out innerTop))continue;
            // Magnets on the landing cells are knocked onward, not used as platforms.
            nearest=along;shore=cell;magnetEnd=Position(cell,tile.blocked?outerTop.y:tile.surfaceHeight);playerEnd=Position(cell+travel,inner.blocked?innerTop.y:inner.surfaceHeight);
        }
        return nearest!=int.MaxValue;
    }
    void BeginRecoilFlight(MagnetPiece magnet,Vector3 origin,Transform destination,bool preserveOffsets=false){
        foreach(var camera in FindObjectsOfType<CurvedTrialCamera>())
            if(camera.enabled&&camera.island&&camera.island.board==this)camera.ResumeFlight();
        RecoilOrigin=origin;RecoilFlying=true;RecoilMagnet=magnet;
        var list=new System.Collections.Generic.List<Renderer>(player.GetComponentsInChildren<Renderer>());list.AddRange(magnet.GetComponentsInChildren<Renderer>());recoilRenderers=list.ToArray();
        if(recoilBlock==null)recoilBlock=new MaterialPropertyBlock();
        // Shader motion is outside the source mesh bounds. Keep the same conservative
        // bounds used by island surfaces, including after this actor lands in a new frame.
        foreach(var r in recoilRenderers){var bounds=r.localBounds;bounds.Encapsulate(new Bounds(Vector3.zero,Vector3.one*300));r.localBounds=bounds;r.GetPropertyBlock(recoilBlock);recoilBlock.SetFloat("_RecoilFlying",1);r.SetPropertyBlock(recoilBlock);}
        if(!preserveOffsets){playerFlightOffset=SurfaceOffset(player.position);magnetFlightOffset=SurfaceOffset(magnet.transform.position);FlightOffset(player,playerFlightOffset);FlightOffset(magnet.transform,magnetFlightOffset);}
        var window=GetComponent<FiveIslandWindow>();if(window)window.PreviewRecoilTarget(destination);
    }
    void EndRecoilFlight(){
        RecoilFlying=false;RecoilMagnet=null;
        if(recoilRenderers!=null)foreach(var r in recoilRenderers)if(r){r.GetPropertyBlock(recoilBlock);recoilBlock.SetFloat("_RecoilFlying",0);r.SetPropertyBlock(recoilBlock);}
        recoilRenderers=null;
    }
    IEnumerator RecoilAcrossIslands(MagnetPiece magnet,Vector2Int from,Vector2Int travel,Vector3 playerStart,Vector3 magnetStart,Quaternion facing,Vector3 axis){
        Vector2Int shore;Vector3 magnetEnd,playerEnd;int exitDistance;
        bool lands=FindRecoilLanding(from,travel,out shore,out magnetEnd,out playerEnd,out exitDistance);
        var chapterExit=GetComponent<ChapterRecoilExit>();bool nextChapter=chapterExit&&chapterExit.Matches(from,travel);
        if(nextChapter)lands=false;
        var direction=new Vector3(travel.x,0,travel.y);
        if(!lands){playerEnd=Position(from+travel*(exitDistance+5),playerStart.y);magnetEnd=playerEnd-direction*cellSize;}
        var focus=Shader.GetGlobalVector("_IceFocus");
        BeginRecoilFlight(magnet,new Vector3(focus.x,focus.y,focus.z),lands?Owner(Tile(shore)):null);
        if(nextChapter){ChapterRecoilTravel.Begin(chapterExit,magnet);yield break;}
        // Position and rendered offset interpolate together, producing one straight trajectory.
        float duration=Mathf.Max(.8f,Vector3.Distance(playerStart,playerEnd)/(cellSize*5));
        Vector3 endPlayerOffset=lands?SurfaceOffset(playerEnd):playerFlightOffset,endMagnetOffset=lands?SurfaceOffset(magnetEnd):magnetFlightOffset;
        bool contactHandled=false;
        for(float t=0;t<duration;t+=Time.deltaTime){
            float a=Mathf.Clamp01(t/duration);
            player.position=Vector3.Lerp(playerStart,playerEnd,a);magnet.transform.position=Vector3.Lerp(magnetStart,magnetEnd,a);
            FlightOffset(player,Vector3.Lerp(playerFlightOffset,endPlayerOffset,a));FlightOffset(magnet.transform,Vector3.Lerp(magnetFlightOffset,endMagnetOffset,a));
            player.rotation=facing;
            if(lands&&!contactHandled&&Vector3.Distance(player.position,playerEnd)<=cellSize){
                contactHandled=true;LaunchLandingMagnets(magnet,shore,travel);
            }
            yield return null;
        }
        if(lands){
            if(!contactHandled)LaunchLandingMagnets(magnet,shore,travel);
            magnet.transform.position=magnetEnd;player.SetPositionAndRotation(playerEnd,facing);
            var binding=magnet.GetComponent<IslandSurfaceAnchor>();if(binding){binding.center=Owner(Tile(shore));binding.Apply();}
            RecordRecoilArrival(magnet,Owner(Tile(shore)));
            FlightOffset(player,endPlayerOffset);FlightOffset(magnet.transform,endMagnetOffset);
            SettleFlightView(shore+travel);EndRecoilFlight();NotifyLanding(shore+travel);Busy=false;LastRule="反冲抵达另一座岛：磁铁在外侧，主角在内侧";
            var window=GetComponent<FiveIslandWindow>();if(window)window.Show(window.CurrentRoom);
            yield break;
        }
        var playerDrift=playerEnd;var magnetDrift=magnetEnd;
        player.position=playerDrift;magnet.transform.position=magnetDrift;
        LastRule="没有可落地的小岛：在太空漂浮 5 秒后重开本关";
        for(float t=0;t<5f;t+=Time.deltaTime){
            var drift=direction*(t*cellSize*.10f)+Vector3.up*(Mathf.Sin(t*2)*cellSize*.08f);
            player.position=playerDrift+drift;magnet.transform.position=magnetDrift+drift;
            player.rotation=Quaternion.AngleAxis(-20+Mathf.Sin(t)*6,axis)*facing;yield return null;
        }
        EndRecoilFlight();Busy=false;ResetCurrentIsland();history.Clear();ReleaseUnusedGeometry();LastRule="太空漂浮结束，已重开当前小岛";
    }
    void SettleFlightView(Vector2Int cell){
        foreach(var camera in FindObjectsOfType<CurvedTrialCamera>())
            if(camera.enabled&&camera.island&&camera.island.board==this)camera.SettleFlight(RecoilOrigin,Owner(Tile(cell)));
    }
    public void ReleaseChapterFlight(){EndRecoilFlight();Busy=false;}
    public IEnumerator ArriveFromChapter(MagnetPiece magnet,Vector2Int shore,Vector3 origin,float speed){
        Busy=true;var inner=shore+Vector2Int.left;
        var magnetEnd=Position(shore,GroundHeight(shore));var playerEnd=Position(inner,GroundHeight(inner));
        var playerStart=player.position;var magnetStart=magnet.transform.position;
        var initialRotation=player.rotation;
        BeginRecoilFlight(magnet,origin,Owner(Tile(shore)),true);
        var playerProperties=new MaterialPropertyBlock();player.GetComponentInChildren<Renderer>().GetPropertyBlock(playerProperties);
        var magnetProperties=new MaterialPropertyBlock();magnet.GetComponentInChildren<Renderer>().GetPropertyBlock(magnetProperties);
        Vector3 startPlayerOffset=playerProperties.GetVector("_RecoilDisplayOffset"),startMagnetOffset=magnetProperties.GetVector("_RecoilDisplayOffset");
        Vector3 endPlayerOffset=SurfaceOffset(playerEnd),endMagnetOffset=SurfaceOffset(magnetEnd);
        float duration=Mathf.Max(.8f,Mathf.Abs(playerStart.x-playerEnd.x)/speed);
        // Cruise at the same speed, then smoothly brake over the final three cells.
        float braking=Mathf.Min(.9f,duration*.4f);duration+=braking*.5f;
        bool contactHandled=false;
        for(float t=0;t<duration;t+=Time.deltaTime){
            float brake=Mathf.Clamp01((t-(duration-braking))/braking);
            float distance=speed*(t-.5f*braking*brake*brake);
            float a=Mathf.Clamp01(distance/Mathf.Abs(playerStart.x-playerEnd.x));
            player.position=Vector3.Lerp(playerStart,playerEnd,a);magnet.transform.position=Vector3.Lerp(magnetStart,magnetEnd,a);
            FlightOffset(player,Vector3.Lerp(startPlayerOffset,endPlayerOffset,a));FlightOffset(magnet.transform,Vector3.Lerp(startMagnetOffset,endMagnetOffset,a));
            player.rotation=initialRotation;
            if(!contactHandled&&Vector3.Distance(player.position,playerEnd)<=cellSize){contactHandled=true;LaunchLandingMagnets(magnet,shore,Vector2Int.left);}
            yield return null;
        }
        if(!contactHandled)LaunchLandingMagnets(magnet,shore,Vector2Int.left);
        player.SetPositionAndRotation(playerEnd,initialRotation);magnet.transform.position=magnetEnd;
        var binding=magnet.GetComponent<IslandSurfaceAnchor>();if(binding){binding.center=Owner(Tile(shore));binding.Apply();}
        FlightOffset(player,endPlayerOffset);FlightOffset(magnet.transform,endMagnetOffset);
        SettleFlightView(inner);EndRecoilFlight();NotifyLanding(inner);
        while(knockedFlights.Exists(f=>f.lands))yield return null;
        CaptureInitialState();LastRule="已连续飞抵绿色星球";
    }

}
}
