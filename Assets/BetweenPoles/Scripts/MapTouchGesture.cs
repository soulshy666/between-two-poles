using System;
using UnityEngine;
namespace BetweenPoles {
// Deterministic gesture state, shared by island maps and the star chart.
public sealed class MapTouchGesture {
    int finger=-1;
    bool blocked,pinched,dragging;
    Vector2 start,last;
    public bool Tapped {get;private set;}
    public Vector2 Position {get;private set;}
    public Vector2 Previous {get;private set;}
    public bool Panning {get;private set;}
    public float ZoomRatio {get;private set;}=1;
    public void Reset(bool waitForRelease=false){finger=-1;blocked=waitForRelease;pinched=dragging=false;ClearOutput();}
    void ClearOutput(){Tapped=Panning=false;ZoomRatio=1;}
    public void Step(Touch[] touches,Func<Vector2,bool> overUI,float threshold){
        ClearOutput();
        if(touches.Length==0){Reset();return;}
        bool allReleased=true;
        foreach(var touch in touches)if(touch.phase!=TouchPhase.Ended&&touch.phase!=TouchPhase.Canceled)allReleased=false;
        if(allReleased&&(blocked||pinched)){Reset();return;}
        foreach(var touch in touches)if(touch.phase==TouchPhase.Began&&overUI(touch.position))blocked=true;
        if(blocked)return;
        if(touches.Length>=2){
            pinched=true;finger=-1;dragging=true;
            if(touches.Length!=2)return;
            var a=touches[0];var b=touches[1];
            if(!Ongoing(a)||!Ongoing(b)||overUI(a.position)||overUI(b.position))return;
            float previous=Vector2.Distance(a.position-a.deltaPosition,b.position-b.deltaPosition);
            float current=Vector2.Distance(a.position,b.position);
            if(previous>1&&current>1)ZoomRatio=Mathf.Clamp(previous/current,.5f,2f);
            return;
        }
        if(pinched)return;
        var t=touches[0];
        if(t.phase==TouchPhase.Began){finger=t.fingerId;start=last=t.position;dragging=false;}
        if(t.fingerId!=finger)return;
        Position=t.position;Previous=last;
        if((t.position-start).sqrMagnitude>threshold*threshold)dragging=true;
        if(t.phase==TouchPhase.Canceled){Reset(true);return;}
        if(t.phase==TouchPhase.Ended){Tapped=!dragging&&!overUI(t.position);finger=-1;return;}
        Panning=dragging;last=t.position;
    }
    static bool Ongoing(Touch touch)=>touch.phase==TouchPhase.Moved||touch.phase==TouchPhase.Stationary;
}
}
