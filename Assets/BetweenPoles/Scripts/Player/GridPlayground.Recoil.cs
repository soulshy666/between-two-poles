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
            nearest=along;shore=cell;magnetEnd=Position(cell,outerTop.y);playerEnd=Position(cell+travel,innerTop.y);
        }
        return nearest!=int.MaxValue;
    }
    void BeginRecoilFlight(MagnetPiece magnet,Vector3 origin,Transform destination){
        RecoilOrigin=origin;RecoilFlying=true;RecoilMagnet=magnet;
        var list=new System.Collections.Generic.List<Renderer>(player.GetComponentsInChildren<Renderer>());list.AddRange(magnet.GetComponentsInChildren<Renderer>());recoilRenderers=list.ToArray();
        if(recoilBlock==null)recoilBlock=new MaterialPropertyBlock();
        foreach(var r in recoilRenderers){r.GetPropertyBlock(recoilBlock);recoilBlock.SetFloat("_RecoilFlying",1);r.SetPropertyBlock(recoilBlock);}
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
        // Constant travel speed across a gap; the takeoff and landing alone ease vertically.
        float duration=Mathf.Max(.8f,Vector3.Distance(playerStart,playerEnd)/(cellSize*5));
        float cruiseHeight=Mathf.Max(playerStart.y,Mathf.Max(playerEnd.y,magnetEnd.y))+cellSize*.65f;
        for(float t=0;t<duration;t+=Time.deltaTime){
            float a=Mathf.Clamp01(t/duration);float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.24f));
            float settle=lands?Mathf.SmoothStep(0,1,Mathf.Clamp01((t-(duration-.3f))/.3f)):0;
            player.position=Vector3.Lerp(playerStart,playerEnd,a);magnet.transform.position=Vector3.Lerp(magnetStart,magnetEnd,a);
            player.position=new Vector3(player.position.x,Mathf.Lerp(Mathf.Lerp(playerStart.y,cruiseHeight,rise),playerEnd.y,settle),player.position.z);
            magnet.transform.position=new Vector3(magnet.transform.position.x,Mathf.Lerp(Mathf.Lerp(magnetStart.y,cruiseHeight,rise),magnetEnd.y,settle),magnet.transform.position.z);
            player.rotation=Quaternion.AngleAxis(-20*rise*(1-settle),axis)*facing;yield return null;
        }
        if(lands){
            magnet.transform.position=magnetEnd;player.SetPositionAndRotation(playerEnd,facing);
            var binding=magnet.GetComponent<IslandSurfaceAnchor>();if(binding){binding.center=Owner(Tile(shore));binding.Apply();}
            EndRecoilFlight();NotifyLanding(shore+travel);Busy=false;LastRule="反冲抵达另一座岛：磁铁在外侧，主角在内侧";
            var window=GetComponent<FiveIslandWindow>();if(window)window.Show(window.CurrentRoom);
            yield break;
        }
        var playerDrift=new Vector3(playerEnd.x,cruiseHeight,playerEnd.z);var magnetDrift=new Vector3(magnetEnd.x,cruiseHeight,magnetEnd.z);
        player.position=playerDrift;magnet.transform.position=magnetDrift;
        LastRule="没有可落地的小岛：在太空漂浮 5 秒后重开本关";
        for(float t=0;t<5f;t+=Time.deltaTime){
            var drift=direction*(t*cellSize*.10f)+Vector3.up*(Mathf.Sin(t*2)*cellSize*.08f);
            player.position=playerDrift+drift;magnet.transform.position=magnetDrift+drift;
            player.rotation=Quaternion.AngleAxis(-20+Mathf.Sin(t)*6,axis)*facing;yield return null;
        }
        EndRecoilFlight();Busy=false;ResetCurrentIsland();history.Clear();ReleaseUnusedGeometry();LastRule="太空漂浮结束，已重开当前小岛";
    }
    public void ReleaseChapterFlight(){EndRecoilFlight();Busy=false;}
    public IEnumerator ArriveFromChapter(MagnetPiece magnet,Vector2Int shore,Vector3 origin,float speed){
        Busy=true;var inner=shore+Vector2Int.left;
        var magnetEnd=Position(shore,GroundHeight(shore));var playerEnd=Position(inner,GroundHeight(inner));
        var playerStart=player.position;var magnetStart=magnet.transform.position;
        var initialRotation=player.rotation;
        BeginRecoilFlight(magnet,origin,Owner(Tile(shore)));
        float duration=Mathf.Max(.8f,Mathf.Abs(playerStart.x-playerEnd.x)/speed);
        // Cruise at the same speed, then smoothly brake over the final three cells.
        float braking=Mathf.Min(.9f,duration*.4f);duration+=braking*.5f;
        for(float t=0;t<duration;t+=Time.deltaTime){
            float brake=Mathf.Clamp01((t-(duration-braking))/braking);
            float distance=speed*(t-.5f*braking*brake*brake);
            float a=Mathf.Clamp01(distance/Mathf.Abs(playerStart.x-playerEnd.x));
            player.position=Vector3.Lerp(playerStart,playerEnd,a);magnet.transform.position=Vector3.Lerp(magnetStart,magnetEnd,a);
            player.rotation=Quaternion.Slerp(initialRotation,Quaternion.LookRotation(Vector3.left),Mathf.SmoothStep(0,1,brake));yield return null;
        }
        player.SetPositionAndRotation(playerEnd,Quaternion.LookRotation(Vector3.left));magnet.transform.position=magnetEnd;
        var binding=magnet.GetComponent<IslandSurfaceAnchor>();if(binding){binding.center=Owner(Tile(shore));binding.Apply();}
        EndRecoilFlight();NotifyLanding(inner);Busy=false;CaptureInitialState();LastRule="已连续飞抵绿色星球";
    }

}
}
