using UnityEngine;
namespace BetweenPoles {
public sealed partial class ChapterAtlas {
    readonly MapTouchGesture chartTouch=new MapTouchGesture();
    RectTransform chartReturn,chartHeading;
    Vector3 chartHome;
    float chartHomeSize;
    void UpdateChartTouch(){
        ProcessChartTouches(Input.touches);
    }
    void ProcessChartTouches(Touch[] touches){
        chartTouch.Step(touches,MobileUI.OverUI,Mathf.Max(12,Screen.height*.012f));
        if(chartTouch.ZoomRatio!=1)showcaseCamera.orthographicSize=Mathf.Clamp(showcaseCamera.orthographicSize*chartTouch.ZoomRatio,chartHomeSize*.5f,chartHomeSize*2);
        if(chartTouch.Panning){
            float distance=10;
            // The pixel camera renders to a smaller texture; normalize phone pixels first.
            var before=showcaseCamera.ViewportToWorldPoint(new Vector3(chartTouch.Previous.x/Screen.width,chartTouch.Previous.y/Screen.height,distance));
            var after=showcaseCamera.ViewportToWorldPoint(new Vector3(chartTouch.Position.x/Screen.width,chartTouch.Position.y/Screen.height,distance));
            var candidate=showcaseCamera.transform.position+before-after;
            // Keep the solar system reachable even after repeated pans.
            var offset=candidate-chartHome;float limit=chartHomeSize*2;
            showcaseCamera.transform.position=chartHome+Vector3.ClampMagnitude(offset,limit);
        }
        if(chartTouch.Tapped)ClickPlanet(chartTouch.Position);
    }
    void LayoutChartTouch(){
        if(!MobileUI.Enabled||!canvas)return;
        var safe=MobileUI.SafeRect;float scale=canvas.scaleFactor;
        if(chartReturn){chartReturn.anchorMin=chartReturn.anchorMax=chartReturn.pivot=Vector2.one;
            chartReturn.anchoredPosition=new Vector2(-(Screen.width-safe.xMax+6*ElementGameUI.K)/scale,-(safe.y+6*ElementGameUI.K)/scale);}
        if(chartHeading){chartHeading.anchorMin=chartHeading.anchorMax=chartHeading.pivot=new Vector2(0,1);
            chartHeading.anchoredPosition=new Vector2((safe.x+6*ElementGameUI.K)/scale,-(safe.y+6*ElementGameUI.K)/scale);}
    }
    void OnApplicationFocus(bool focused){if(!focused){chartTouch.Reset(true);MobileUI.ResetTouches();}}
    void OnApplicationPause(bool paused){if(paused){chartTouch.Reset(true);MobileUI.ResetTouches();}}
}
}
