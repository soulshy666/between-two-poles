using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
public sealed partial class ChapterAtlas {
    public bool IsInspecting {get;private set;}
    public static string InspectionScenePath {get;private set;}
    public static bool IsInspectionScene(Scene scene){return !string.IsNullOrEmpty(InspectionScenePath)&&scene.path==InspectionScenePath;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInspection(){InspectionScenePath=null;}
    bool inspectionLoading,inspectionOwnsScene,returnAfterInspection;
    int inspectionChapter;
    Scene inspectedScene;
    BlackHoleTravel inspectionNavigation;
    readonly Dictionary<MonoBehaviour,bool> inspectedScripts=new Dictionary<MonoBehaviour,bool>();
    readonly Dictionary<GameObject,bool> inspectedRoots=new Dictionary<GameObject,bool>();
    readonly Dictionary<Behaviour,bool> hiddenChartBehaviours=new Dictionary<Behaviour,bool>();
    readonly Dictionary<Renderer,bool> hiddenChartRenderers=new Dictionary<Renderer,bool>();
    string inspectionMessage;
    public void OpenInspection(int chapter){
        if(IsInspecting||returning||chartTransitioning||chapter<0||chapter>=chapters.Length)return;
        if(!CanInspectChapter(chapter)){RejectPlanet(chapters[chapter].planet);return;}
        StartCoroutine(InspectChapter(chapter));
    }
    IEnumerator InspectChapter(int chapter){
        IsInspecting=true;inspectionLoading=true;inspectionChapter=chapter;InspectionScenePath=chapters[chapter].scene;
        inspectionMessage="正在打开"+chapters[chapter].title+"…";
        yield return ApproachPlanet(chapters[chapter].planet);
        inspectedScene=SceneManager.GetSceneByPath(InspectionScenePath);inspectionOwnsScene=!inspectedScene.IsValid()||!inspectedScene.isLoaded;
        if(inspectionOwnsScene){
            if(!Application.CanStreamedLevelBeLoaded(InspectionScenePath)){inspectionMessage="这个星球暂时无法打开";inspectionLoading=false;yield break;}
            // Runs before Start, so inspecting another chapter cannot start gameplay or record exploration.
            SceneManager.sceneLoaded+=PrepareInspectionScene;
            var operation=SceneManager.LoadSceneAsync(InspectionScenePath,LoadSceneMode.Additive);
            if(operation!=null)yield return operation;
            SceneManager.sceneLoaded-=PrepareInspectionScene;
            inspectedScene=SceneManager.GetSceneByPath(InspectionScenePath);
        }else PrepareInspectionScene(inspectedScene,LoadSceneMode.Additive);
        if(!inspectedScene.IsValid()||!inspectedScene.isLoaded){inspectionLoading=false;yield break;}
        GridPlayground board=null;
        foreach(var sceneRoot in inspectedScene.GetRootGameObjects()){
            var candidate=sceneRoot.GetComponentInChildren<GridPlayground>(true);if(candidate)board=candidate;
        }
        if(!board){inspectionMessage="这个星球没有可检视的关卡";inspectionLoading=false;yield break;}
        HideChart();SceneManager.SetActiveScene(inspectedScene);
        foreach(var pair in inspectedRoots)if(pair.Value||ChapterMapReturn.WasSuspendedRoot(pair.Key))pair.Key.SetActive(true);
        if(board.player)board.player.gameObject.SetActive(false);
        if(!inspectionNavigation)inspectionNavigation=gameObject.AddComponent<BlackHoleTravel>();
        inspectionNavigation.BeginInspection(board);
        bool explored=false;var window=board.GetComponent<FiveIslandWindow>();
        if(window)foreach(var room in window.rooms)if(SessionExploration.GetInt(Key(inspectedScene.path,room.id),0)>0||room.center&&SessionExploration.GetInt(Key(inspectedScene.path,room.center.name),0)>0){explored=true;break;}
        inspectionMessage=explored?chapters[chapter].title+" · 已探索的小岛  |  拖拽查看，滚轮缩放（仅检视）":"尚未探索这颗星球的小岛";
        yield return RevealInspection();
        inspectionLoading=false;if(returnAfterInspection)StartCoroutine(FinishInspectionView());
    }
    void PrepareInspectionScene(Scene scene,LoadSceneMode mode){
        if(!IsInspectionScene(scene))return;
        foreach(var sceneRoot in scene.GetRootGameObjects()){
            inspectedRoots[sceneRoot]=sceneRoot.activeSelf;
            foreach(var script in sceneRoot.GetComponentsInChildren<MonoBehaviour>(true)){
                if(!script)continue;
                // Leave only the actual scene's visual pipeline active. No puzzle, portal, intro or input code.
                string type=script.GetType().Name;
                bool visual=script is IslandCamera||script is CurvedTrialCamera||script is PixelWorldCamera||script is PixelPresentation||script is FiveIslandWindow
                    ||type=="PixelGeneratorLab"||type=="GeneratedBackgroundFollower"||type=="AmbientAsteroids"||type=="SkyStreaks"||type=="SpaceMotion";
                if(!visual){inspectedScripts[script]=script.enabled;script.enabled=false;}
            }
        }
    }
    void HideChart(){
        canvas.enabled=false;
        foreach(var sceneRoot in gameObject.scene.GetRootGameObjects()){
            foreach(var renderer in sceneRoot.GetComponentsInChildren<Renderer>(true)){hiddenChartRenderers[renderer]=renderer.enabled;renderer.enabled=false;}
            foreach(var behaviour in sceneRoot.GetComponentsInChildren<Behaviour>(true)){
                if(behaviour==this||behaviour==inspectionNavigation||behaviour is UnityEngine.EventSystems.EventSystem||behaviour is UnityEngine.EventSystems.BaseInputModule||behaviour is Canvas||behaviour is UnityEngine.UI.Graphic||behaviour is UnityEngine.UI.Selectable)continue;
                hiddenChartBehaviours[behaviour]=behaviour.enabled;behaviour.enabled=false;
            }
        }
    }
    public void CloseInspection(){if(!IsInspecting||inspectionLoading)return;StartCoroutine(FinishInspectionView());}
    IEnumerator FinishInspectionView(){
        inspectionLoading=true;
        if(!returnAfterInspection&&inspectionNavigation){
            transitionFrame=inspectionNavigation.CaptureInspectionFrame();transitionOpacity=1;
        }
        if(inspectionNavigation)inspectionNavigation.FinishInspection();
        // Hide preview roots before restoring scripts, so suspended gameplay never gets a frame of input.
        foreach(var pair in inspectedRoots)if(pair.Key)pair.Key.SetActive(false);
        if(inspectionOwnsScene&&inspectedScene.IsValid()&&inspectedScene.isLoaded)yield return SceneManager.UnloadSceneAsync(inspectedScene);
        else foreach(var pair in inspectedScripts)if(pair.Key)pair.Key.enabled=pair.Value;
        inspectedRoots.Clear();inspectedScripts.Clear();InspectionScenePath=null;
        SceneManager.SetActiveScene(gameObject.scene);
        foreach(var pair in hiddenChartRenderers)if(pair.Key)pair.Key.enabled=pair.Value;
        foreach(var pair in hiddenChartBehaviours)if(pair.Key)pair.Key.enabled=pair.Value;
        hiddenChartRenderers.Clear();hiddenChartBehaviours.Clear();canvas.enabled=true;
        if(returnAfterInspection){RestoreChartCamera();IsInspecting=false;inspectionLoading=false;returnAfterInspection=false;ReturnToGame();yield break;}
        yield return RetreatToChart(inspectionChapter);
        IsInspecting=false;inspectionLoading=false;if(returnAfterInspection){returnAfterInspection=false;ReturnToGame();}
    }
    void OnGUI(){
        DrawInspectionTransition();
        if(!IsInspecting){
            if(canvas&&!canvas.enabled&&PixelGameUI.Button(new Rect(Screen.width-240,22,218,52),"返回游戏（Esc）"))ReturnToGame();
            return;
        }
        var matrix=GUI.matrix;float scale=Mathf.Max(.7f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        GUI.enabled=true;
        if(PixelGameUI.Button(new Rect(22,22,210,48),"返回岛屿（M）"))ReturnToGame();
        GUI.enabled=!inspectionLoading;
        if(PixelGameUI.Button(new Rect(22,80,210,48),"返回星际航图"))CloseInspection();
        GUI.enabled=true;
        GUI.Label(new Rect(248,25,900,48),inspectionMessage,PixelGameUI.Text(17));
        GUI.matrix=matrix;
    }
}
}
