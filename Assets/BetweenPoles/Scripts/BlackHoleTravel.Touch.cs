using UnityEngine;
namespace BetweenPoles {
public sealed partial class BlackHoleTravel {
    int navigationFinger=-1;
    bool touchGestureBlocked;
    Vector2 touchStart,touchLast;
    void OnApplicationFocus(bool focused){if(!focused){navigationFinger=-1;touchGestureBlocked=true;playerReturnPressed=false;}}
    void OnApplicationPause(bool paused){if(paused){navigationFinger=-1;touchGestureBlocked=true;playerReturnPressed=false;}}
    static float PinchScroll(float size,float previous,float current){return previous>1&&current>1?(size-size*previous/current)/1.5f:0;}
    bool TouchOverNavigationUI(Vector2 point) {
        var safe=ElementGameUI.SafeRect;
        return !Screen.safeArea.Contains(point)||Screen.height-point.y<safe.y+Mathf.Max(100,40*ElementGameUI.K);
    }
    bool UpdateTouchNavigation() {
        if(!Application.isMobilePlatform&&Input.touchCount==0)return false;
        int count=Input.touchCount;
        if(count==0){navigationFinger=-1;touchGestureBlocked=false;playerReturnPressed=false;return true;}
        if(count>=2){
            touchGestureBlocked=true;navigationFinger=-1;playerReturnPressed=false;dragging=true;
            var a=Input.GetTouch(0);var b=Input.GetTouch(1);
            if(TouchOverNavigationUI(a.position)||TouchOverNavigationUI(b.position))return true;
            if(a.phase==TouchPhase.Began||b.phase==TouchPhase.Began||a.phase==TouchPhase.Ended||b.phase==TouchPhase.Ended||a.phase==TouchPhase.Canceled||b.phase==TouchPhase.Canceled)return true;
            float previous=Vector2.Distance(a.position-a.deltaPosition,b.position-b.deltaPosition);
            float current=Vector2.Distance(a.position,b.position);
            ApplyNavigationScroll(PinchScroll(viewSize,previous,current));
            return true;
        }
        var touch=Input.GetTouch(0);
        // A pinch owns the whole gesture until every finger has lifted.
        if(touchGestureBlocked)return true;
        if(touch.phase==TouchPhase.Began){
            if(TouchOverNavigationUI(touch.position)){touchGestureBlocked=true;return true;}
            navigationFinger=touch.fingerId;touchStart=touchLast=touch.position;dragging=false;
            playerReturnPressed=HitInspectionPlayer(touch.position);
        }
        if(touch.fingerId!=navigationFinger)return true;
        float threshold=Mathf.Max(12,Screen.height*.012f);
        if((touch.position-touchStart).sqrMagnitude>threshold*threshold)dragging=true;
        if(touch.phase==TouchPhase.Canceled){navigationFinger=-1;playerReturnPressed=false;return true;}
        if(touch.phase==TouchPhase.Ended){
            if(!dragging&&!TouchOverNavigationUI(touch.position)){
                if(inspectionBoard)PlayerReturnRequested=playerReturnPressed&&HitInspectionPlayer(touch.position);
                else SelectTouchPortal(touch.position);
            }
            navigationFinger=-1;playerReturnPressed=false;return true;
        }
        Vector3 before,after;
        if(dragging&&GroundPoint(touchLast,out before)&&GroundPoint(touch.position,out after))
            viewFocus+=(before-after)/Mathf.Max(1f,Shader.GetGlobalFloat("_IslandDisplayScale"));
        touchLast=touch.position;return true;
    }
    void SelectTouchPortal(Vector2 position){
        BlackHolePortal selected=null;float best=Mathf.Pow(Mathf.Max(38,Screen.height*.04f),2);
        foreach(var portal in testPortals){if(!portal)continue;var screen=NavigationScreenPoint(portal.transform.position);
            float distance=((Vector2)screen-position).sqrMagnitude;if(screen.z>0&&distance<best){best=distance;selected=portal;}}
        if(selected)SelectTestPortal(selected);
    }
}
}
