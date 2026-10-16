using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class GridPlayground {
    public bool GhostMode {get;private set;}
    Vector3 ghostOrigin;
    WorldState ghostUndo;
    readonly List<Behaviour> ghostBehaviours=new List<Behaviour>();
    readonly List<Collider> ghostColliders=new List<Collider>();

    public bool ToggleGhostMode(){
        if(GhostMode){
            GridTile tile;Vector3 landing;
            var cell=Cell(player);
            // A portal mouth is not a safe debug landing position.
            if(!TryLandingSurface(cell,out tile,out landing)||float.IsNaN(landing.y)||float.IsInfinity(landing.y)
                ||(tile&&tile.GetComponentInChildren<BlackHolePortal>(true)))
                return Reject("此处不能站立，请移动到地面或物体顶面后再按 4");
            RecordHistory(ghostUndo);
            player.position=landing;
            RestoreGhostComponents();
            ClearMovementInput();NotifyLanding(Cell(player));
            var anchor=player.GetComponent<IslandSurfaceAnchor>();if(anchor&&tile){anchor.center=Owner(tile);anchor.Apply();}
            LastRule="已到达调试位置，恢复正常操作";return true;
        }
        var map=GetComponent<ChapterMapReturn>();
        var debugPanel=GetComponent<MagnetDebugPanel>();
        if(!player||!enabled||Busy||OpeningCinematic||ElementGameUI.Paused||Time.timeScale<=0
            ||ChapterRecoilTravel.Active||BlackHoleTravel.InTransit||BlackHoleTravel.Selecting
            ||BlackHoleTravel.SkipTransitionActive||(map&&map.MapSessionActive)||(debugPanel&&debugPanel.IsOpen)
            ||knockedFlights.Count>0||liveRings.Count>0)return false;
        ghostUndo=SaveWorld();ghostOrigin=player.position;ClearMovementInput();
        var pose=player.GetComponent<PlayerPushPose>();if(pose)pose.End();
        foreach(var behaviour in player.GetComponentsInChildren<Behaviour>(true))
            if(behaviour.enabled&&(behaviour is PlayerPushPose||behaviour is Animator||behaviour is Animation)){ghostBehaviours.Add(behaviour);behaviour.enabled=false;}
        foreach(var collider in player.GetComponentsInChildren<Collider>(true))if(collider.enabled){ghostColliders.Add(collider);collider.enabled=false;}
        var audio=player.GetComponent<PlayerFootstepAudio>();if(audio)audio.StopMovementSounds();
        GhostMode=true;LastRule="幽灵模式：方向键 / WASD 移动，Shift 加速，4 确认落点";return true;
    }
    void TickGhostMode(){
        if(ElementGameUI.Paused||Time.timeScale<=0)return;
        if(Input.GetKeyDown(KeyCode.Alpha4)||Input.GetKeyDown(KeyCode.Keypad4)){ToggleGhostMode();return;}
        var direction=Vector3.zero;
        if(DirectionHeld(Vector2Int.right))direction.x++;
        if(DirectionHeld(Vector2Int.left))direction.x--;
        if(DirectionHeld(Vector2Int.up))direction.z++;
        if(DirectionHeld(Vector2Int.down))direction.z--;
        float speed=cellSize*5*(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?3:1);
        player.position+=Vector3.ClampMagnitude(direction,1)*speed*Time.deltaTime;
        GridTile tile;Vector3 landing;
        // Only preview the support height; no landing events or puzzle actions.
        if(TryLandingSurface(Cell(player),out tile,out landing)&&!float.IsInfinity(landing.y)&&!float.IsNaN(landing.y)){
            var p=player.position;p.y=landing.y;player.position=p;
        }
    }
    void RestoreGhostComponents(){
        GhostMode=false;
        foreach(var behaviour in ghostBehaviours)if(behaviour)behaviour.enabled=true;
        foreach(var collider in ghostColliders)if(collider)collider.enabled=true;
        ghostBehaviours.Clear();ghostColliders.Clear();ghostUndo=null;
    }
    void CancelGhostMode(){if(!GhostMode)return;if(player)player.position=ghostOrigin;RestoreGhostComponents();}
}
}
