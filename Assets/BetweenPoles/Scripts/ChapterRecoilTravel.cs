using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using BetweenPoles.Generators;
namespace BetweenPoles {
// Keep the flight's real actors, camera and space background through the scene handoff.
[DefaultExecutionOrder(-400)]
public sealed partial class ChapterRecoilTravel:MonoBehaviour {
    public static bool Active {get;private set;}
    public static CurvedTrialCamera FlightView {get;private set;}
    public static float FlightSize {get;private set;}
    public static bool TryGetFlightFocus(CurvedTrialCamera camera,out Vector3 focus){
        var flight=activeFlight;
        if(Active&&FlightView==camera&&flight&&flight.source&&flight.source.player
            &&(!flight.arrivalStarted||(flight.arrivalBoard&&flight.arrivalBoard.RecoilFlying))){
            // The source and destination boards briefly have no RecoilFlying owner
            // while the handoff is spread across frames. Keep tracking the shared actor.
            focus=flight.source.RecoilCameraFocus+flight.flightFramingOffset;return true;
        }
        focus=Vector3.zero;return false;
    }
    GridPlayground source;MagnetPiece magnet;CurvedTrialCamera view;Scene destination;
    string destinationPath;readonly List<Renderer> destinationRenderers=new List<Renderer>();
    float speed;Vector3 heading=Vector3.left;
    Vector2Int travelDirection=Vector2Int.left;
    PixelGeneratorLab flightBackground,targetBackground;
    Color originalBackground;Color[] originalPalette;
    float sourceScreenEdge=float.NaN;
    Rect[] departureTiles;
    bool WaitForVerdantDeparture => source&&source.gameObject.scene.name=="Chapter02_VerdantWorld";
    bool VerdantIslandsVisible(Camera camera){
        if(departureTiles==null){
            var bounds=new List<Rect>();
            float scale=Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"));
            Vector3 focus=Shader.GetGlobalVector("_IceFocus");
            var right=camera.transform.right;var up=camera.transform.up;
            // Include the island rim, side wall and a full cell of decoration clearance.
            float margin=source.cellSize*scale*1.5f;
            foreach(var tile in source.tiles){
                if(!tile||!tile.gameObject.activeInHierarchy)continue;
                var point=focus+(tile.transform.position-focus)*scale;
                bounds.Add(new Rect(Vector3.Dot(point,right)-margin,Vector3.Dot(point,up)-margin,margin*2,margin*2));
            }
            departureTiles=bounds.ToArray();
        }
        float halfHeight=camera.orthographicSize,halfWidth=halfHeight*camera.aspect;
        var position=camera.transform.position;
        var screen=new Rect(Vector3.Dot(position,camera.transform.right)-halfWidth,
            Vector3.Dot(position,camera.transform.up)-halfHeight,halfWidth*2,halfHeight*2);
        foreach(var tile in departureTiles)if(screen.Overlaps(tile))return true;
        return false;
    }
    ThreadPriority previousLoadingPriority;
    bool changedLoadingPriority;
    bool releasedControl;
    static ChapterRecoilTravel prepared;
    AsyncOperation loading;
    ChapterFlightTitle flightTitle;float flightBeganAt;
    float launchAge;
    Vector3 flightFramingOffset;
    bool arrivalStarted;
    Coroutine preloadRoutine;
    readonly List<GameObject> sleepingRoots=new List<GameObject>();
    public static void Preload(ChapterRecoilExit exit){
        if(Active||prepared)return;
        Prepare(exit);
        if(prepared&&prepared.source==exit.GetComponent<GridPlayground>())
            prepared.preloadRoutine=prepared.StartCoroutine(prepared.LoadAhead());
    }
    void LowerLoadingPriority(){
        if(changedLoadingPriority)return;
        previousLoadingPriority=Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority=ThreadPriority.Low;changedLoadingPriority=true;
    }
    void RestoreLoadingPriority(){
        if(!changedLoadingPriority)return;
        Application.backgroundLoadingPriority=previousLoadingPriority;changedLoadingPriority=false;
    }
    IEnumerator LoadAhead(){
        LowerLoadingPriority();
        loading=SceneManager.LoadSceneAsync(destinationPath,LoadSceneMode.Additive);
        if(loading!=null)yield return loading;
        RestoreLoadingPriority();preloadRoutine=null;
    }
    IEnumerator WakeDestination(){
        // Hidden renderers and disabled chapter controllers remain suspended during warm-up.
        foreach(var root in sleepingRoots){
            if(root)root.SetActive(true);
            Advance();yield return null;
        }
        sleepingRoots.Clear();
    }
    public static bool IsPreparing(Scene scene){return prepared&&scene.path==prepared.destinationPath;}
    public static IEnumerator CancelPreloadForMenu(){
        if(!Active&&prepared){
            if(prepared.preloadRoutine!=null){prepared.StopCoroutine(prepared.preloadRoutine);prepared.preloadRoutine=null;}
            yield return prepared.ReleaseCancelledDestination();
        }
    }
    public static void Prepare(ChapterRecoilExit exit){
        if(Active||prepared||!Application.CanStreamedLevelBeLoaded(exit.destinationScene))return;
        // Do not adopt an atlas inspection scene as a flight destination.
        if(SceneManager.GetSceneByPath(exit.destinationScene).isLoaded)return;
        if(exit.gameObject.scene.name=="Chapter01_IceWorld")ChapterFlightTitle.Preload();
        prepared=new GameObject("下一章节预加载").AddComponent<ChapterRecoilTravel>();
        DontDestroyOnLoad(prepared.gameObject);
        prepared.source=exit.GetComponent<GridPlayground>();prepared.destinationPath=exit.destinationScene;
        SceneManager.sceneLoaded+=prepared.HideDestination;
        // Reserve ownership before Awake/OnEnable so the destination cannot take the player/view.
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Active=false;FlightView=null;FlightSize=0;prepared=null;activeFlight=null;UndoInputFrame=-1;}
    public static void Begin(ChapterRecoilExit exit,MagnetPiece incoming){
        if(Active)return;
        Prepare(exit);
        var runner=prepared&&prepared.source==exit.GetComponent<GridPlayground>()?prepared:new GameObject("连续星际飞行").AddComponent<ChapterRecoilTravel>();DontDestroyOnLoad(runner.gameObject);
        if(runner.preloadRoutine!=null){runner.StopCoroutine(runner.preloadRoutine);runner.preloadRoutine=null;}
        runner.source=exit.GetComponent<GridPlayground>();runner.magnet=incoming;
        runner.travelDirection=exit.exitDirection;
        runner.heading=new Vector3(exit.exitDirection.x,0,exit.exitDirection.y);
        runner.view=FindObjectsOfType<CurvedTrialCamera>().First(c=>c.isActiveAndEnabled&&c.GetComponent<Camera>().enabled&&c.island&&c.island.board==runner.source);
        // Preserve the departure's vertical framing instead of snapping from island
        // center to the launcher's row as soon as flight begins.
        var framing=runner.view.transform.position-runner.view.island.viewingOffset-runner.source.RecoilCameraFocus;
        runner.flightFramingOffset=Vector3.ProjectOnPlane(framing,runner.heading);
        FlightView=runner.view;FlightSize=runner.view.GetComponent<Camera>().orthographicSize;
        runner.view.navigationZoomSmoothTime=0;runner.view.ResetZoomVelocity();
        runner.destinationPath=exit.destinationScene;runner.speed=runner.source.cellSize*5;Active=true;
        runner.CaptureUndoView();activeFlight=runner;runner.flightBeganAt=Time.time;
        PlayerPushPose.BeginRecoil(runner.source.player);
        var movementAudio=PlayerFootstepAudio.For(runner.source.player);if(movementAudio)movementAudio.StopMovementSounds();
        if(runner.source.gameObject.scene.name=="Chapter01_IceWorld"){var titleObject=new GameObject("跨章标题 · 落地交互后消散");DontDestroyOnLoad(titleObject);runner.flightTitle=titleObject.AddComponent<ChapterFlightTitle>();runner.flightTitle.Begin(incoming,runner.view);}
        runner.StartCoroutine(runner.Travel());
    }
    void HideDestination(Scene scene,LoadSceneMode mode){
        // A direct load/restart replaces the preloaded world. It must never be
        // adopted as a dormant destination or later unloaded by this old owner.
        if(mode==LoadSceneMode.Single){
            if(prepared==this)prepared=null;
            SceneManager.sceneLoaded-=HideDestination;
            RestoreLoadingPriority();Destroy(gameObject);return;
        }
        if(scene.path!=destinationPath)return;destination=scene;
        foreach(var root in scene.GetRootGameObjects()){
            foreach(var camera in root.GetComponentsInChildren<Camera>())camera.enabled=false;
            foreach(var listener in root.GetComponentsInChildren<AudioListener>())listener.enabled=false;
            foreach(var pixel in root.GetComponentsInChildren<PixelWorldCamera>())pixel.enabled=false;
            foreach(var curved in root.GetComponentsInChildren<CurvedTrialCamera>())curved.enabled=false;
            foreach(var island in root.GetComponentsInChildren<IslandCamera>())island.enabled=false;
            foreach(var light in root.GetComponentsInChildren<Light>())light.enabled=false;
            foreach(var board in root.GetComponentsInChildren<GridPlayground>())board.enabled=false;
            foreach(var map in root.GetComponentsInChildren<ChapterMapReturn>())map.enabled=false;
            foreach(var exit in root.GetComponentsInChildren<ChapterRecoilExit>())exit.enabled=false;
            foreach(var window in root.GetComponentsInChildren<FiveIslandWindow>())window.enabled=false;
            // Keep star positions and animation continuous; inherit destination colors later.
            if(root.name=="动态太空背景"){targetBackground=root.GetComponent<PixelGeneratorLab>();root.SetActive(false);continue;}
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(renderer.enabled){destinationRenderers.Add(renderer);renderer.enabled=false;}
        }
        // Dormant scenes retain assets but do not simulate physics, particles or scripts.
        if(!Active)foreach(var root in scene.GetRootGameObjects())if(root.activeSelf){
            sleepingRoots.Add(root);root.SetActive(false);
        }
    }
    bool SourceHasLeftScreen(){
        var camera=view.GetComponent<Camera>();var planet=view.planet;
        if(travelDirection!=Vector2Int.left)return !VerdantIslandsVisible(camera);
        if(WaitForVerdantDeparture)return !VerdantIslandsVisible(camera);
        if(view.PlanarChapter){
            // Wait until the departing island, rather than a hidden globe, is off screen.
            float scale=Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"));
            if(!float.IsNaN(sourceScreenEdge))return sourceScreenEdge-Vector3.Dot(camera.transform.position,camera.transform.right)>camera.orthographicSize*camera.aspect+source.cellSize;
            float left=float.PositiveInfinity;var window=source.GetComponent<FiveIslandWindow>();
            foreach(var tile in source.tiles){if(!tile)continue;var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();
                if(window&&(!owner||owner.center!=window.rooms[window.CurrentRoom].center))continue;
                var p=source.RecoilOrigin+(tile.transform.position-source.RecoilOrigin)*scale;
                left=Mathf.Min(left,Vector3.Dot(p-camera.transform.position,camera.transform.right)-source.cellSize*scale);
            }
            sourceScreenEdge=left+Vector3.Dot(camera.transform.position,camera.transform.right);
            return left>camera.orthographicSize*camera.aspect+source.cellSize;
        }
        if(!planet)return true;
        float radius=view.displayRadius>0?view.displayRadius*view.globeDisplayScale:view.planetRadius;
        float right=Vector3.Dot(planet.position-camera.transform.position,camera.transform.right);
        return right-radius>camera.orthographicSize*camera.aspect+source.cellSize;
    }
    void Advance(){
        // Local cinematic slow motion: audio, UI, pause and undo retain their normal clocks.
        float dt=Mathf.Min(Time.deltaTime,.05f);
        launchAge+=dt;
        float acceleration=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1.65f,launchAge));
        float multiplier=Mathf.Lerp(.18f,1,acceleration);
        var delta=heading*speed*dt*multiplier;
        source.player.position+=delta;magnet.transform.position+=delta;
        var pose=source.player.GetComponent<PlayerPushPose>();if(pose)pose.SampleRecoilFlight(Time.time-flightBeganAt,magnet);
    }
    IEnumerator BlendBackground(float duration){
        if(!flightBackground||!targetBackground)yield break;
        flightBackground.EnsureData();targetBackground.EnsureData();
        originalBackground=flightBackground.background;
        originalPalette=(Color[])flightBackground.spaceColors.Clone();
        var targetColor=targetBackground.background;var palette=(Color[])targetBackground.spaceColors.Clone();
        var sun=FindBackgroundSun(flightBackground);var targetSun=FindBackgroundSun(targetBackground);
        var sunPosition=sun?sun.localPosition:Vector3.zero;
        var sunScale=sun?sun.localScale:Vector3.one;
        var sunRotation=sun?sun.localRotation:Quaternion.identity;
        // Advance from an exact unchanged frame, then ease both ends of the color transition.
        for(float elapsed=0;;elapsed+=Time.deltaTime){
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/duration));
            flightBackground.background=Color.Lerp(originalBackground,targetColor,t);
            for(int i=0;i<originalPalette.Length;i++)flightBackground.spaceColors[i]=Color.Lerp(originalPalette[i],palette[i],t);
            // PixelGeneratorLab.Update uploads these colors once per frame already.
            // Both suns use their camera-following background's local frame, not world coordinates.
            // Keep the same animated sun and ease its placement and apparent size to the destination.
            if(sun&&targetSun&&sun.parent==flightBackground.transform){
                sun.localPosition=Vector3.Lerp(sunPosition,targetSun.localPosition,t);
                sun.localScale=Vector3.Lerp(sunScale,targetSun.localScale,t);
                sun.localRotation=Quaternion.Slerp(sunRotation,targetSun.localRotation,t);
            }
            if(t>=1)break;
            yield return null;
        }
    }
    static Transform FindBackgroundSun(PixelGeneratorLab background){
        return background.GetComponentsInChildren<SpaceMotion>(true)
            .Where(s=>s.name.StartsWith("太阳")&&s.transform.parent==background.transform)
            .Select(s=>s.transform).FirstOrDefault();
    }
    IEnumerator Travel(){
        if(!Application.CanStreamedLevelBeLoaded(destinationPath)){Abort("目标星球没有加入构建列表");yield break;}
        SceneManager.sceneLoaded-=HideDestination;SceneManager.sceneLoaded+=HideDestination;
        LowerLoadingPriority();
        ChapterAtlas.DestinationIsland=-1;ChapterAtlas.DestinationScene=null;
        // Present the launch before loading can spend time integrating destination objects.
        Advance();yield return null;
        var load=loading=loading??SceneManager.LoadSceneAsync(destinationPath,LoadSceneMode.Additive);
        // Reuse the dormant chapter; early launches fall back to loading during flight.
        float clearSkySeconds=0;
        while(true){
            bool clear=SourceHasLeftScreen();
            clearSkySeconds=clear?clearSkySeconds+Mathf.Min(Time.deltaTime,.05f):0;
            bool departureReady=clear&&(!WaitForVerdantDeparture||(clearSkySeconds>=1f&&launchAge>=4.5f));
            if(load.isDone&&departureReady&&(!flightTitle||Time.time-flightBeganAt>=ChapterFlightTitle.CruiseSeconds))break;
            Advance();yield return null;
        }
        yield return WakeDestination();
        yield return null;
        SceneManager.sceneLoaded-=HideDestination;
        if(prepared==this)prepared=null;
        var board=destination.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridPlayground>()).FirstOrDefault();
        var targetView=destination.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CurvedTrialCamera>()).FirstOrDefault();
        if(!board||!targetView){Abort("目标星球缺少关卡或镜头配置");yield break;}
        var targetWindow=board.GetComponent<FiveIslandWindow>();
        int startRoom=targetWindow?Mathf.Clamp(targetWindow.initialRoom,0,targetWindow.rooms.Length-1):0;
        var landingIsland=targetWindow&&targetWindow.rooms.Length>0?targetWindow.rooms[startRoom].center:targetView.island.CurrentIsland;
        GridTile shoreTile=null;float score=float.NegativeInfinity;
        foreach(var tile in board.tiles){
            if(!tile||!tile.gameObject.activeInHierarchy||tile.blocked)continue;
            var owner=tile.GetComponentInParent<IslandSurfaceAnchor>();
            if(!owner||owner.center!=landingIsland)continue;
            var cell=Cell(tile.transform.position,board.cellSize);var inner=board.TileAt(cell+travelDirection);
            var innerOwner=inner?inner.GetComponentInParent<IslandSurfaceAnchor>():null;
            if(!inner||!inner.gameObject.activeInHierarchy||inner.blocked||!innerOwner||innerOwner.center!=landingIsland)continue;
            if(tile.GetComponentInChildren<BlackHolePortal>()||inner.GetComponentInChildren<BlackHolePortal>())continue;
            // Landing magnets are handled by the shared impact rule on arrival.
            var offset=tile.transform.position-landingIsland.position;
            float value=-Vector3.Dot(offset,heading)*100-Mathf.Abs(Vector3.Dot(offset,new Vector3(-heading.z,0,heading.x)));
            if(value>score){score=value;shoreTile=tile;}
        }
        if(!shoreTile){Abort("目标星球的初始房间没有沿飞行方向相邻的两个可落脚地块，请预留落点");yield break;}
        arrivalBoard=board;
        var oldScene=source.gameObject.scene;var actor=source.player;var cellSize=board.cellSize;
        var shore=Cell(shoreTile.transform.position,cellSize);var innerCell=shore+travelDirection;
        // Place the loaded planet ahead of the ongoing flight, aligned to whole grid cells.
        var desiredInner=Cell(actor.position,cellSize)+travelDirection*16;
        var shiftCell=desiredInner-innerCell;var shift=new Vector3(shiftCell.x*cellSize,0,shiftCell.y*cellSize);
        foreach(var root in destination.GetRootGameObjects()){
            root.transform.position+=shift;Advance();yield return null;
        }
        // These authored grid coordinates are not Transforms. Rebase them with
        // the destination, or a later chapter exit fails after playing through
        // the preceding chapter despite working when opened directly.
        var destinationExit=board.GetComponent<ChapterRecoilExit>();
        if(destinationExit){destinationExit.exitRow+=shiftCell.y;destinationExit.exitColumn+=shiftCell.x;destinationExit.testCell+=shiftCell;}
        var destinationEnding=board.GetComponent<SolarEnding>();
        if(destinationEnding)destinationEnding.exitRow+=shiftCell.y;
        board.InvalidateTileLookup();
        shore+=shiftCell;innerCell+=shiftCell;
        var targetIsland=targetView.island;var targetCenters=targetIsland.islandCenters;
        var targetPlanet=targetView.planet;
        var origin=source.RecoilOrigin;
        // The active view owns the scale; an additively loaded scene must not supply it.
        var scale=view.islandDisplayScale*(source.GetComponent<FiveIslandWindow>()?1.1f:1f);
        // Retain source roots only for the shared presentation; puzzle geometry unloads normally.
        var roots=oldScene.GetRootGameObjects();
        flightBackground=roots.Where(r=>r.name=="动态太空背景").Select(r=>r.GetComponent<PixelGeneratorLab>()).FirstOrDefault();
        // The carried background remains visible after arrival. The finale must
        // take over that same sun, not detach the hidden destination's duplicate.
        var carriedSun=flightBackground?FindBackgroundSun(flightBackground):null;
        if(destinationEnding&&carriedSun)destinationEnding.sunTemplate=carriedSun;
        foreach(var root in roots)if(root==view.transform.root.gameObject||root.name=="动态太空背景"||root.name=="04 LIGHTING"){movedRoots.Add(root);SceneManager.MoveGameObjectToScene(root,destination);}
        var oldActor=board.player;oldActor.gameObject.SetActive(false);
        SharedPlayer.Bind(board,true);actor=board.player;
        magnet.transform.SetParent(null,true);SceneManager.MoveGameObjectToScene(magnet.gameObject,destination);magnet.transform.SetParent(board.transform,true);
        board.magnets=board.magnets.Concat(new[]{magnet}).ToArray();
        var anchor=actor.GetComponent<IslandSurfaceAnchor>();if(anchor)anchor.ground=board;
        var magnetAnchor=magnet.GetComponent<IslandSurfaceAnchor>();if(magnetAnchor)magnetAnchor.ground=board;
        source.enabled=false;source.ReleaseChapterFlight();
        view.island.enabled=false;view.island.board=board;view.island.player=actor;view.island.islandCenters=targetCenters;view.island.SelectCurrentIsland();view.island.enabled=true;
        var window=board.GetComponent<FiveIslandWindow>();if(window){window.view=view.island;window.enabled=true;window.RefreshLayout();window.Show(startRoom);}
        view.rotationOrigin=shift;view.fixedDisplayOrigin=true;view.displayOrigin=origin;view.curveOrigin=landingIsland.position;
        Shader.SetGlobalVector("_IceCurveFocus",view.curveOrigin);
        var planetStyle=targetPlanet?targetPlanet.GetComponent<MainPlanetStyle>():null;if(planetStyle)planetStyle.targetCamera=view.GetComponent<Camera>();
        view.planet=targetPlanet;view.displayRadius=targetView.displayRadius;view.globeDisplayScale=targetView.globeDisplayScale;
        view.islandDisplayScale=scale/(window&&window.enabled?1.1f:1f);
        // Planar chapters do not need a legacy globe behind their islands.
        if(targetPlanet){
            float radius=view.displayRadius>0?view.displayRadius*view.globeDisplayScale:view.planetRadius;
            var center=landingIsland.position;var displayedCenter=origin+(center-origin)*scale;
            targetPlanet.position=displayedCenter+view.transform.forward*(radius+1.5f)+view.transform.up*view.globeVerticalOffset;
            targetPlanet.localScale=Vector3.one*(radius/10);targetPlanet.rotation=Quaternion.identity;
        }
        int revealed=0;
        foreach(var renderer in destinationRenderers){
            if(renderer&&renderer.gameObject.activeInHierarchy)renderer.enabled=true;
            if(++revealed%128==0){Advance();yield return null;}
        }
        if(window)window.Show(startRoom);
        // The arrival offsets must be evaluated against destination bounds, never
        // the previous world's gradually moving flat rectangle.
        view.planningSize=targetView.planningSize;view.fitIsland=targetView.fitIsland;
        view.planetRadius=targetView.planetRadius;view.centeredGlobe=targetView.centeredGlobe;
        view.globeVerticalOffset=targetView.globeVerticalOffset;view.zoomSmoothTime=targetView.zoomSmoothTime;
        view.ResetSurfaceFrame();view.Apply(view.transform.position-view.island.viewingOffset);
        board.enabled=true;SceneManager.SetActiveScene(destination);
        var mapReturn=board.GetComponent<ChapterMapReturn>();if(!mapReturn)mapReturn=board.gameObject.AddComponent<ChapterMapReturn>();mapReturn.enabled=true;
        var nextExit=board.GetComponent<ChapterRecoilExit>();if(nextExit)nextExit.enabled=true;
        float remainingDistance=Vector3.Distance(actor.position,new Vector3(innerCell.x*cellSize,actor.position.y,innerCell.y*cellSize));
        var colorBlend=StartCoroutine(BlendBackground(Mathf.Max(.5f,remainingDistance/speed*.85f)));
        arrivalStarted=true;
        var arrival=board.StartCoroutine(board.ArriveFromChapter(magnet,shore,origin,speed,travelDirection));
        // Keep the authored source state alive until touchdown so in-flight Z can
        // return across the scene handoff without rebuilding or losing puzzle state.
        foreach(var root in oldScene.GetRootGameObjects())if(root.activeSelf){suspendedSourceRoots.Add(root);root.SetActive(false);}
        yield return arrival;
        // Input depends on a completed landing, not on the camera reaching an exact float position.
        board.ReleaseChapterFlight();
        releasedControl=true;
        if(changedLoadingPriority){Application.backgroundLoadingPriority=previousLoadingPriority;changedLoadingPriority=false;}
        FlightView=null;
        Active=false;activeFlight=null;
        if(flightTitle)flightTitle.Landed(board);
        var unload=SceneManager.UnloadSceneAsync(oldScene);
        yield return colorBlend;
        while(unload!=null&&!unload.isDone)yield return null;
        Destroy(gameObject);
    }
    static Vector2Int Cell(Vector3 p,float size){return new Vector2Int(Mathf.RoundToInt(p.x/size),Mathf.RoundToInt(p.z/size));}
    void Abort(string reason){
        if(flightTitle)flightTitle.Cancel();
        Debug.LogError("星际飞行："+reason);SceneManager.sceneLoaded-=HideDestination;
        if(destination.IsValid()&&destination.isLoaded)SceneManager.UnloadSceneAsync(destination);
        if(source){source.ReleaseChapterFlight();source.ResetCurrentIsland();}Active=false;Destroy(gameObject);
    }
    void OnDestroy(){if(!releasedControl&&flightTitle)flightTitle.Cancel();SceneManager.sceneLoaded-=HideDestination;if(changedLoadingPriority)Application.backgroundLoadingPriority=previousLoadingPriority;if(!releasedControl&&FlightView==view){Active=false;FlightView=null;}}
}
}
