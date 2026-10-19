using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    readonly MapTouchGesture navigationTouch=new MapTouchGesture();
    bool touchUndoRequested;
    void OnApplicationFocus(bool focused){if(!focused){navigationTouch.Reset(true);playerReturnPressed=false;MobileUI.ResetTouches();}}
    void OnApplicationPause(bool paused){if(paused){navigationTouch.Reset(true);playerReturnPressed=false;MobileUI.ResetTouches();}}
    static float PinchScroll(float size,float previous,float current){return previous>1&&current>1?(size-size*previous/current)/1.5f:0;}
    bool UpdateTouchNavigation() {
        if(!MobileUI.Enabled&&Input.touchCount==0)return false;
        ProcessNavigationTouches(Input.touches);
        return true;
    }
    void ProcessNavigationTouches(Touch[] touches){
        navigationTouch.Step(touches,MobileUI.OverUI,Mathf.Max(12,Screen.height*.012f));
        if(navigationTouch.ZoomRatio!=1){float size=navigationSmoothTime>.2f?viewCamera.orthographicSize:viewSize;ApplyNavigationScroll((size-size*navigationTouch.ZoomRatio)/1.5f);}
        Vector3 before,after;
        if(navigationTouch.Panning&&GroundPoint(navigationTouch.Previous,out before)&&GroundPoint(navigationTouch.Position,out after))
            viewFocus+=(before-after)/Mathf.Max(1f,Shader.GetGlobalFloat("_IslandDisplayScale"));
        if(navigationTouch.Tapped){
            if(inspectionBoard)PlayerReturnRequested=HitInspectionPlayer(navigationTouch.Position);
            else SelectTouchPortal(navigationTouch.Position);
        }
    }
    void SelectTouchPortal(Vector2 position){
        if(testPortals==null)return;
        BlackHolePortal selected=null;float best=Mathf.Pow(Mathf.Max(38,Screen.height*.045f),2);
        foreach(var portal in testPortals){if(!portal||!portal.gameObject.activeInHierarchy)continue;var screen=NavigationScreenPoint(portal.transform.position);
            float distance=((Vector2)screen-position).sqrMagnitude;if(screen.z>0&&distance<best){best=distance;selected=portal;}}
        if(selected)SelectTestPortal(selected);
    }
}
}
