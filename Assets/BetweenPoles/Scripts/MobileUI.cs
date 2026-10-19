using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetweenPoles {
// IMGUI touch controls track their own finger, so the movement finger is never
// mistaken for a tap on a second control. Rectangles use top-left screen pixels.
public static class MobileUI {
    public static bool PreviewInEditor;
    public static bool Enabled => Application.isMobilePlatform || (Application.isEditor && PreviewInEditor);
    sealed class Control {
        public Rect rect;
        public int finger=-1, frame=-1, seen=-10;
        public bool clicked,held;
        public Vector2 position;
    }
    static readonly Dictionary<string,Control> controls=new Dictionary<string,Control>();
    static readonly List<RaycastResult> hits=new List<RaycastResult>();
    public static Rect SafeRect => ToGUI(Enabled?Screen.safeArea:new Rect(0,0,Screen.width,Screen.height));
    public static Rect ToGUI(Rect screen) => new Rect(screen.x,Screen.height-screen.yMax,screen.width,screen.height);
    public static Rect Fit(Rect safe,Vector2 design) {
        float scale=Mathf.Min(safe.width/design.x,safe.height/design.y);
        return new Rect(safe.center.x-design.x*scale/2,safe.center.y-design.y*scale/2,design.x*scale,design.y*scale);
    }
    static Rect ScreenRect(Rect rect){
        var min=GUI.matrix.MultiplyPoint3x4(new Vector3(rect.x,rect.y));
        var max=GUI.matrix.MultiplyPoint3x4(new Vector3(rect.xMax,rect.yMax));
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    public static bool OverUI(Vector2 screen){
        var point=new Vector2(screen.x,Screen.height-screen.y);
        if(!SafeRect.Contains(point))return true;
        foreach(var control in controls.Values)if(Time.frameCount-control.seen<=1&&control.rect.Contains(point))return true;
        if(EventSystem.current){
            hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
            if(hits.Count>0)return true;
        }
        return false;
    }
    static Control Sample(string id,Rect rect){
        if(!controls.TryGetValue(id,out var control)){control=new Control();controls.Add(id,control);}
        if(control.seen<Time.frameCount-1)control.finger=-1;
        control.seen=Time.frameCount;control.rect=ScreenRect(rect);
        if(!GUI.enabled||(!Application.isFocused&&!(Application.isEditor&&PreviewInEditor))||TelevisionSceneTransition.Busy){control.finger=-1;control.held=control.clicked=false;return control;}
        if(control.frame==Time.frameCount)return control;
        control.frame=Time.frameCount;control.clicked=false;control.held=false;
        bool found=false;
        for(int i=0;i<Input.touchCount;i++){
            var touch=Input.GetTouch(i);var point=new Vector2(touch.position.x,Screen.height-touch.position.y);
            if(control.finger<0&&touch.phase==TouchPhase.Began&&control.rect.Contains(point))control.finger=touch.fingerId;
            if(touch.fingerId!=control.finger)continue;
            found=true;control.position=point;
            if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled){
                control.clicked=touch.phase==TouchPhase.Ended&&control.rect.Contains(point);control.finger=-1;
            }else control.held=true;
            break;
        }
        if(!found)control.finger=-1;
        return control;
    }
    public static bool Button(string id,Rect rect){
        if(!Enabled)return GUI.Button(rect,GUIContent.none,GUIStyle.none);
        var control=Sample(id,rect);
        if(Application.isEditor&&Input.touchCount==0)return GUI.Button(rect,GUIContent.none,GUIStyle.none);
        bool clicked=control.clicked;control.clicked=false;return clicked;
    }
    public static bool ActionIcon(string id,string asset,bool right){
        var safe=SafeRect;float k=ElementGameUI.K;
        var rect=new Rect(right?safe.xMax-25*k:safe.x+6*k,safe.y+6*k,19*k,21*k);
        ElementGameUI.Draw(rect,asset+"-normal");return Button(id,rect);
    }
    public static bool SkipButton(string id){
        var safe=SafeRect;float k=ElementGameUI.K;
        var rect=new Rect(safe.xMax-32*k,safe.y+6*k,26*k,18*k);
        ElementGameUI.Panel(rect);
        GUI.Label(rect,">>",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(8*k)});
        return Button(id,rect);
    }
    public static float Slider(string id,Rect rect,float value){
        if(!Enabled)return GUI.HorizontalSlider(rect,value,0,1);
        var control=Sample(id,rect);
        if(Application.isEditor&&Input.touchCount==0)value=GUI.HorizontalSlider(rect,value,0,1,GUIStyle.none,GUIStyle.none);
        else if(control.held||control.clicked)value=Mathf.Clamp01((control.position.x-control.rect.x)/control.rect.width);
        control.clicked=false;
        var old=GUI.color;
        GUI.color=ElementGameUI.Ink;GUI.DrawTexture(new Rect(rect.x,rect.center.y-2,rect.width,4),Texture2D.whiteTexture);
        GUI.color=Color.white;float knob=Mathf.Min(rect.height,rect.width*.1f);
        GUI.DrawTexture(new Rect(rect.x+(rect.width-knob)*value,rect.y,knob,rect.height),Texture2D.whiteTexture);GUI.color=old;
        return value;
    }
    public static void ResetTouches(){controls.Clear();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){PreviewInEditor=false;ResetTouches();}
}
}
