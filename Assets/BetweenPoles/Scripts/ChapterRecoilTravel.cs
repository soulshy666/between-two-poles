using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using BetweenPoles.Generators;
namespace BetweenPoles {
// Keep the flight's real actors, camera and space background through the scene handoff.
public sealed class ChapterRecoilTravel:MonoBehaviour {
    public static bool Active {get;private set;}
    GridPlayground source;MagnetPiece magnet;CurvedTrialCamera view;Scene destination;
    string destinationPath;readonly List<Renderer> destinationRenderers=new List<Renderer>();
    float speed;Vector3 heading=Vector3.left;
    PixelGeneratorLab flightBackground,targetBackground;
    Color originalBackground;Color[] originalPalette;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Active=false;}
    public static void Begin(ChapterRecoilExit exit,MagnetPiece incoming){
        if(Active)return;
        var runner=new GameObject("连续星际飞行").AddComponent<ChapterRecoilTravel>();DontDestroyOnLoad(runner.gameObject);
        runner.source=exit.GetComponent<GridPlayground>();runner.magnet=incoming;
        runner.view=FindObjectsOfType<CurvedTrialCamera>().First(c=>c.island&&c.island.board==runner.source);
        runner.destinationPath=exit.destinationScene;runner.speed=runner.source.cellSize*5;Active=true;
        runner.StartCoroutine(runner.Travel());
    }
    void HideDestination(Scene scene,LoadSceneMode mode){
        if(scene.path!=destinationPath)return;destination=scene;
        foreach(var root in scene.GetRootGameObjects()){
            foreach(var camera in root.GetComponentsInChildren<Camera>())camera.enabled=false;
            foreach(var pixel in root.GetComponentsInChildren<PixelWorldCamera>())pixel.enabled=false;
            foreach(var curved in root.GetComponentsInChildren<CurvedTrialCamera>())curved.enabled=false;
            foreach(var island in root.GetComponentsInChildren<IslandCamera>())island.enabled=false;
            foreach(var light in root.GetComponentsInChildren<Light>())light.enabled=false;
            foreach(var board in root.GetComponentsInChildren<GridPlayground>())board.enabled=false;
            // Keep star positions and animation continuous; inherit destination colors later.
            if(root.name=="动态太空背景"){targetBackground=root.GetComponent<PixelGeneratorLab>();root.SetActive(false);continue;}
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(renderer.enabled){destinationRenderers.Add(renderer);renderer.enabled=false;}
        }
    }
    bool SourceHasLeftScreen(){
        var camera=view.GetComponent<Camera>();var planet=view.planet;
        if(!planet)return true;
        float radius=view.displayRadius>0?view.displayRadius*view.globeDisplayScale:view.planetRadius;
        float right=Vector3.Dot(planet.position-camera.transform.position,camera.transform.right);
        return right-radius>camera.orthographicSize*camera.aspect+source.cellSize;
    }
    void Advance(){var delta=heading*speed*Time.deltaTime;source.player.position+=delta;magnet.transform.position+=delta;}
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
            flightBackground.Apply();
            // Both suns use their camera-following background's local frame, not world coordinates.
            // Keep the same animated sun and ease its placement and apparent size to the destination.
            if(sun&&targetSun){
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
        SceneManager.sceneLoaded+=HideDestination;
        ChapterAtlas.DestinationIsland=-1;ChapterAtlas.DestinationScene=null;
        var load=SceneManager.LoadSceneAsync(destinationPath,LoadSceneMode.Additive);
        // Loading starts at takeoff. The flight continues even if loading takes longer.
        while(!load.isDone||!SourceHasLeftScreen()){Advance();yield return null;}
        yield return null;
        SceneManager.sceneLoaded-=HideDestination;
        var board=destination.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GridPlayground>()).FirstOrDefault();
        var targetView=destination.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CurvedTrialCamera>()).FirstOrDefault();
        if(!board||!targetView){Abort("目标星球缺少关卡或镜头配置");yield break;}
        GridTile shoreTile=null;float score=float.NegativeInfinity;
        foreach(var tile in board.tiles){
            if(!tile||!tile.gameObject.activeInHierarchy||tile.blocked)continue;
            var cell=Cell(tile.transform.position,board.cellSize);var inner=board.TileAt(cell+Vector2Int.left);
            if(!inner||inner.blocked||board.MagnetAt(cell)||board.MagnetAt(cell+Vector2Int.left))continue;
            float value=tile.transform.position.x*100-Mathf.Abs(tile.transform.position.z);
            if(value>score){score=value;shoreTile=tile;}
        }
        if(!shoreTile){Abort("目标星球没有连续两个空落脚格");yield break;}
        var oldScene=source.gameObject.scene;var actor=source.player;var cellSize=board.cellSize;
        var shore=Cell(shoreTile.transform.position,cellSize);var innerCell=shore+Vector2Int.left;
        // Place the loaded planet ahead of the ongoing flight, aligned to whole grid cells.
        var desiredInner=new Vector2Int(Mathf.FloorToInt(actor.position.x/cellSize)-16,Mathf.RoundToInt(actor.position.z/cellSize));
        var shiftCell=desiredInner-innerCell;var shift=new Vector3(shiftCell.x*cellSize,0,shiftCell.y*cellSize);
        foreach(var root in destination.GetRootGameObjects())root.transform.position+=shift;
        shore+=shiftCell;innerCell+=shiftCell;
        var targetIsland=targetView.island;var targetCenters=targetIsland.islandCenters;
        var targetPlanet=targetView.planet;
        var origin=source.RecoilOrigin;var scale=Shader.GetGlobalFloat("_IslandDisplayScale");
        // Retain source roots only for the shared presentation; puzzle geometry unloads normally.
        var roots=oldScene.GetRootGameObjects();
        flightBackground=roots.Where(r=>r.name=="动态太空背景").Select(r=>r.GetComponent<PixelGeneratorLab>()).FirstOrDefault();
        foreach(var root in roots)if(root==view.transform.root.gameObject||root.name=="动态太空背景"||root.name=="04 LIGHTING")SceneManager.MoveGameObjectToScene(root,destination);
        var oldActor=board.player;oldActor.gameObject.SetActive(false);
        actor.SetParent(null,true);SceneManager.MoveGameObjectToScene(actor.gameObject,destination);board.player=actor;board.playerVisual=source.playerVisual;
        magnet.transform.SetParent(null,true);SceneManager.MoveGameObjectToScene(magnet.gameObject,destination);magnet.transform.SetParent(board.transform,true);
        board.magnets=board.magnets.Concat(new[]{magnet}).ToArray();
        var anchor=actor.GetComponent<IslandSurfaceAnchor>();if(anchor)anchor.ground=board;
        source.enabled=false;source.ReleaseChapterFlight();
        view.island.enabled=false;view.island.board=board;view.island.player=actor;view.island.islandCenters=targetCenters;view.island.enabled=true;
        var window=board.GetComponent<FiveIslandWindow>();if(window)window.view=view.island;
        view.fixedDisplayOrigin=true;view.displayOrigin=origin;
        var planetStyle=targetPlanet.GetComponent<MainPlanetStyle>();if(planetStyle)planetStyle.targetCamera=view.GetComponent<Camera>();
        view.planet=targetPlanet;view.displayRadius=targetView.displayRadius;view.globeDisplayScale=targetView.globeDisplayScale;
        view.islandDisplayScale=scale/(window&&window.enabled?1.1f:1f);
        float radius=view.displayRadius>0?view.displayRadius*view.globeDisplayScale:view.planetRadius;
        // Convert the planet center into the same rendered frame as the translated tiles.
        var center=targetCenters[0].position;var displayedCenter=origin+(center-origin)*scale;
        targetPlanet.position=displayedCenter+view.transform.forward*(radius+1.5f)+view.transform.up*view.globeVerticalOffset;
        targetPlanet.localScale=Vector3.one*(radius/10);targetPlanet.rotation=Quaternion.identity;
        foreach(var renderer in destinationRenderers)if(renderer&&renderer.gameObject.activeInHierarchy)renderer.enabled=true;
        board.enabled=true;SceneManager.SetActiveScene(destination);
        float remainingDistance=Vector3.Distance(actor.position,new Vector3(innerCell.x*cellSize,actor.position.y,innerCell.y*cellSize));
        var colorBlend=StartCoroutine(BlendBackground(Mathf.Max(.5f,remainingDistance/speed*.85f)));
        var arrival=board.StartCoroutine(board.ArriveFromChapter(magnet,shore,origin,speed));
        var unload=SceneManager.UnloadSceneAsync(oldScene);
        yield return arrival;
        yield return colorBlend;
        while(unload!=null&&!unload.isDone)yield return null;
        Active=false;Destroy(gameObject);
    }
    static Vector2Int Cell(Vector3 p,float size){return new Vector2Int(Mathf.RoundToInt(p.x/size),Mathf.RoundToInt(p.z/size));}
    void Abort(string reason){
        Debug.LogError("星际飞行："+reason);SceneManager.sceneLoaded-=HideDestination;
        if(destination.IsValid()&&destination.isLoaded)SceneManager.UnloadSceneAsync(destination);
        if(source){source.ReleaseChapterFlight();source.ResetCurrentIsland();}Active=false;Destroy(gameObject);
    }
    void OnDestroy(){SceneManager.sceneLoaded-=HideDestination;Active=false;}
}
}
