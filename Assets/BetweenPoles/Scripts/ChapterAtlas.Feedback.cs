using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
public sealed partial class ChapterAtlas {
    readonly List<Transform> chartPlanets=new List<Transform>();
    readonly Dictionary<Transform,Quaternion> shakingPlanets=new Dictionary<Transform,Quaternion>();
    AudioSource rejectionSource;AudioClip rejectionClip;
    bool chartTransitioning;
    Vector3 chartCameraPosition;float chartCameraSize;
    Texture2D transitionFrame;float transitionOpacity;
    public static string WorldVisitKey(string scene){return "ChapterAtlas.worldVisited.v1."+scene;}
    void CacheChartPlanets(){
        foreach(var sceneRoot in gameObject.scene.GetRootGameObjects())foreach(var planet in sceneRoot.GetComponentsInChildren<Generators.PixelGeneratorLab>())
            if(!planet.spaceMode)chartPlanets.Add(planet.transform);
        foreach(var chapter in chapters)if(chapter.planet&&!chartPlanets.Contains(chapter.planet))chartPlanets.Add(chapter.planet);
    }
    public bool CanInspectChapter(int chapter){
        if(chapter<0||chapter>=chapters.Length)return false;
        var data=chapters[chapter];if(string.IsNullOrEmpty(data.scene)||!Application.CanStreamedLevelBeLoaded(data.scene))return false;
        var loaded=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(data.scene);
        if(loaded.IsValid()&&loaded.isLoaded){
            foreach(var sceneRoot in loaded.GetRootGameObjects()){
                var window=sceneRoot.GetComponentInChildren<FiveIslandWindow>(true);if(!window)continue;
                foreach(var room in window.rooms)if(SessionExploration.GetInt(Key(data.scene,room.id),0)>0||room.center&&SessionExploration.GetInt(Key(data.scene,room.center.name),0)>0)return true;
            }
            return false;
        }
        if(data.islands==null||data.islands.Length==0)return false;
        if(SessionExploration.GetInt(WorldVisitKey(data.scene),0)>0)return true;
        foreach(var island in data.islands)if(SessionExploration.GetInt(Key(data.scene,island.id),0)>0)return true;
        return false;
    }
    Transform PlanetAt(Vector2 screen){
        var ray=showcaseCamera.ViewportPointToRay(new Vector3(screen.x/Screen.width,screen.y/Screen.height,0));
        Transform selected=null;float closest=float.PositiveInfinity;
        foreach(var planet in chartPlanets){
            if(!planet||!planet.gameObject.activeInHierarchy)continue;float distance;
            if(!new Plane(showcaseCamera.transform.forward,planet.position).Raycast(ray,out distance))continue;
            float radius=0;
            foreach(var renderer in planet.GetComponentsInChildren<Renderer>())if(renderer.enabled)radius=Mathf.Max(radius,Mathf.Min(renderer.bounds.extents.x,renderer.bounds.extents.y));
            if(hoverScales.TryGetValue(planet,out var baseScale)&&planet.localScale.x!=0)
                radius*=Mathf.Abs(baseScale.x/planet.localScale.x);
            for(int i=0;i<chapters.Length;i++)if(chapters[i].planet==planet){radius=chapters[i].hitRadius;break;}
            float hit=Vector3.Distance(ray.GetPoint(distance),planet.position)/Mathf.Max(.1f,radius);
            if(hit<=1&&hit<closest){closest=hit;selected=planet;}
        }
        return selected;
    }
    public void ClickPlanet(Vector2 screen){
        if(IsInspecting||returning||chartTransitioning)return;
        var selected=PlanetAt(screen);
        if(!selected)return;
        ClearPlanetHover();
        for(int i=0;i<chapters.Length;i++)if(chapters[i].planet==selected){OpenInspection(i);return;}
        RejectPlanet(selected);
    }
    void RejectPlanet(Transform planet){
        if(!planet||shakingPlanets.ContainsKey(planet))return;
        if(!rejectionSource){rejectionSource=gameObject.AddComponent<AudioSource>();rejectionSource.playOnAwake=false;rejectionSource.spatialBlend=0;rejectionSource.volume=.28f;}
        if(!rejectionClip){
            const int rate=44100;var samples=new float[(int)(rate*.24f)];double phase=0;
            for(int i=0;i<samples.Length;i++){
                float t=i/(float)rate,local=t<.12f?t:t-.12f;float hz=t<.12f?220:155;
                phase+=2*System.Math.PI*hz/rate;
                float envelope=Mathf.Min(1,local/.008f)*Mathf.Clamp01((.105f-local)/.025f);
                samples[i]=envelope*(float)(System.Math.Sin(phase)*.7+System.Math.Sin(phase*3)*.2);
            }
            rejectionClip=AudioClip.Create("星球无法检视 · 双音提示",samples.Length,1,rate,false);rejectionClip.SetData(samples,0);
        }
        rejectionSource.PlayOneShot(rejectionClip);StartCoroutine(ShakePlanet(planet));
    }
    IEnumerator ShakePlanet(Transform planet){
        Quaternion original=planet.localRotation;shakingPlanets.Add(planet,original);
        try{for(float age=0;age<.6f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)){if(!planet)yield break;float t=age/.6f;planet.localRotation=original*Quaternion.Euler(0,0,Mathf.Sin(t*Mathf.PI*4)*8*(1-t));yield return null;}}
        finally{if(planet)planet.localRotation=original;shakingPlanets.Remove(planet);}
    }
    IEnumerator ApproachPlanet(Transform planet){
        if(MobileUI.Enabled){chartCameraPosition=showcaseCamera.transform.position;chartCameraSize=showcaseCamera.orthographicSize;chartTouch.Reset(true);}
        chartTransitioning=true;
        canvas.enabled=false;
        for(float age=0;age<1.25f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)){
            SetPlanetApproachPose(planet,Mathf.SmoothStep(0,1,age/1.25f));yield return null;
        }
        SetPlanetApproachPose(planet,1);
        // Hold the final approaching view while loading, then dissolve it over the real island view.
        yield return new WaitForEndOfFrame();
        transitionFrame=ScreenCapture.CaptureScreenshotAsTexture();transitionFrame.filterMode=FilterMode.Point;transitionOpacity=1;
    }
    IEnumerator ReturnToGameAnimated(){
        // Finish an in-flight chart arrival before taking ownership of its camera.
        while(chartTransitioning)yield return null;
        int chapter=Mathf.Clamp(ReturnChapter,0,chapters.Length-1);
        yield return ApproachPlanet(chapters[chapter].planet);
        // Keep the captured planet close-up over the first restored gameplay frame.
        // The atlas stays alive until the dissolve finishes, avoiding a one-frame cut.
        HideChart();
        ChapterMapReturn.ResumeGame(true);
        yield return RevealInspection();
        UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(gameObject.scene);
    }
    IEnumerator RevealInspection(){
        yield return null;
        for(float age=0;age<.45f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)){transitionOpacity=1-Mathf.SmoothStep(0,1,age/.45f);yield return null;}
        transitionOpacity=0;if(transitionFrame)Destroy(transitionFrame);transitionFrame=null;chartTransitioning=false;
    }
    void DrawInspectionTransition(){
        if(!transitionFrame||transitionOpacity<=0)return;
        var previous=GUI.color;GUI.color=new Color(1,1,1,transitionOpacity);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),transitionFrame);GUI.color=previous;
    }
    public static Texture2D ChartArrivalFrame;
    IEnumerator ArriveAtChart(){
        transitionFrame=ChartArrivalFrame;ChartArrivalFrame=null;transitionOpacity=1;
        chartTransitioning=true;
        yield return RetreatToChart(CurrentChapter);
    }
    IEnumerator RetreatToChart(int chapter){
        chartTransitioning=true;canvas.enabled=false;
        var planet=chapters[chapter].planet;
        // Reverse the arrival: dissolve the island into the planet close-up first,
        // then run the same camera path backwards to the full star chart.
        SetPlanetApproachPose(planet,1);
        for(float age=0;age<.45f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)){
            SetPlanetApproachPose(planet,1);
            transitionOpacity=1-Mathf.SmoothStep(0,1,age/.45f);yield return null;
        }
        transitionOpacity=0;if(transitionFrame)Destroy(transitionFrame);transitionFrame=null;
        for(float age=0;age<1.25f;age+=Mathf.Min(Time.unscaledDeltaTime,.05f)){
            SetPlanetApproachPose(planet,1-Mathf.SmoothStep(0,1,age/1.25f));yield return null;
        }
        RestoreChartCamera();canvas.enabled=true;chartTransitioning=false;
    }
    void SetPlanetApproachPose(Transform planet,float progress){
        Vector3 destination=planet?planet.position:chartCameraPosition;destination.z=chartCameraPosition.z;
        showcaseCamera.transform.position=Vector3.Lerp(chartCameraPosition,destination,progress);
        showcaseCamera.orthographicSize=Mathf.Exp(Mathf.Lerp(Mathf.Log(chartCameraSize),Mathf.Log(.65f),progress));
    }
    void RestoreChartCamera(){showcaseCamera.transform.position=chartCameraPosition;showcaseCamera.orthographicSize=chartCameraSize;transitionOpacity=0;if(transitionFrame)Destroy(transitionFrame);transitionFrame=null;}
    void ReleaseChartFeedback(){
        ReleasePlanetHover();
        foreach(var pair in shakingPlanets)if(pair.Key)pair.Key.localRotation=pair.Value;
        shakingPlanets.Clear();if(rejectionClip)Destroy(rejectionClip);if(transitionFrame)Destroy(transitionFrame);
    }
}
}
