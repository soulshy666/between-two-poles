using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
// Exploration records are independent of puzzle undo. They only reveal visited islands.
public sealed class ChapterMapReturn : MonoBehaviour {
    const string Showcase="Assets/BetweenPoles/Scenes/PlanetChapterShowcase.unity";
    GridPlayground board;FiveIslandWindow window;bool loading;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    static void Loaded(Scene scene,LoadSceneMode mode){
        if(!scene.name.StartsWith("Chapter0")||ChapterAtlas.IsInspectionScene(scene)||ChapterRecoilTravel.IsPreparing(scene))return;
        foreach(var root in scene.GetRootGameObjects()){var b=root.GetComponentInChildren<GridPlayground>();if(b&&!b.GetComponent<ChapterMapReturn>()){b.gameObject.AddComponent<ChapterMapReturn>();break;}}
    }
    void OnEnable(){
        board=GetComponent<GridPlayground>();window=GetComponent<FiveIslandWindow>();
        if(!GetComponent<ElementGameUI>())gameObject.AddComponent<ElementGameUI>();
        if(board){board.Landed-=Visit;board.Landed+=Visit;}
    }
    void OnDisable(){if(board)board.Landed-=Visit;}
    IEnumerator Start(){
        if(ChapterRecoilTravel.IsPreparing(gameObject.scene))yield break;
        board=GetComponent<GridPlayground>();foreach(var candidate in FindObjectsOfType<FiveIslandWindow>())if(candidate.gameObject.scene==gameObject.scene)window=candidate;yield return null;
        if(!board)yield break;
        if(ChapterAtlas.DestinationScene==gameObject.scene.path&&ChapterAtlas.DestinationIsland>0){
            GridTile best=null;float distance=float.PositiveInfinity;
            foreach(var t in board.tiles){var anchor=t.GetComponentInParent<IslandSurfaceAnchor>();if(!anchor||anchor.center.name!=ChapterAtlas.DestinationRoom||t.blocked)continue;var cell=new Vector2Int(Mathf.RoundToInt(t.transform.position.x/board.cellSize),Mathf.RoundToInt(t.transform.position.z/board.cellSize));if(board.MagnetAt(cell))continue;float d=(t.transform.position-anchor.center.position).sqrMagnitude;if(d<distance){distance=d;best=t;}}
            if(best){board.player.position=new Vector3(best.transform.position.x,best.surfaceHeight,best.transform.position.z);board.CaptureInitialState();board.SendMessage("NotifyLanding",board.PlayerCell);if(window)window.Show(ChapterAtlas.DestinationIsland);}
        }
        ChapterAtlas.DestinationIsland=-1;
        var tile=board.TileAt(board.PlayerCell);if(tile)Visit(tile);
    }
    void Visit(GridTile tile){SessionExploration.VisitTile(board,tile);}

    void OnDestroy(){if(localOverview)CloseLocalOverview();if(mapPose&&mapSession)mapPose.End();if(board)board.Landed-=Visit;}
    static ChapterMapReturn suspended;
    readonly System.Collections.Generic.List<GameObject> suspendedRoots=new System.Collections.Generic.List<GameObject>();
    bool playerWasActive,boardWasEnabled;
    public static bool TrySuspendedPosition(string scene,out Vector3 position){position=Vector3.zero;if(!suspended||suspended.gameObject.scene.path!=scene||!suspended.board.player)return false;position=suspended.board.player.position;return true;}
    public static bool WasSuspendedRoot(GameObject root){return suspended&&suspended.suspendedRoots.Contains(root);}
    public static bool HasSuspendedGame {get{return suspended;}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetNavigation(){suspended=null;}
    BlackHoleTravel localNavigation;
    bool localOverview;
    bool mapAnimating,mapSession;
    PlayerPushPose mapPose;
    public bool MapSessionActive {get{return mapSession||localOverview||loading||mapAnimating;}}
    public bool IsLocalOverview {get{return localOverview;}}
    public void OpenLocalOverview(){
        if(localOverview||loading||mapAnimating||!board||board.Busy||board.OpeningCinematic||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit||ChapterRecoilTravel.Active)return;
        SessionExploration.RecordStandingIsland(board);
        StartCoroutine(OpenMapWithPose());
    }
    IEnumerator OpenMapWithPose(){
        mapAnimating=true;mapSession=true;cancelMapOpening=false;
        suspended=this;playerWasActive=board.player.gameObject.activeSelf;boardWasEnabled=board.enabled;
        board.enabled=false;
        mapPose=PlayerPushPose.BeginMap(board.player);
        yield return mapPose.AnimateMap(true);
        mapAnimating=false;
        if(cancelMapOpening){StartCoroutine(StowMap());yield break;}
        if(!localNavigation)localNavigation=gameObject.AddComponent<BlackHoleTravel>();
        localNavigation.BeginInspection(board,true);localOverview=true;
    }
    IEnumerator StowMap(){
        mapAnimating=true;loading=false;
        if(localOverview){localNavigation.FinishInspection();localOverview=false;}
        board.player.gameObject.SetActive(playerWasActive);
        if(mapPose)yield return mapPose.AnimateMap(false);
        board.enabled=boardWasEnabled;mapSession=false;mapAnimating=false;
        if(suspended==this)suspended=null;
    }
    public void CloseLocalOverview(){
        if(!localOverview)return;
        localNavigation.FinishInspection();board.player.gameObject.SetActive(playerWasActive);board.enabled=boardWasEnabled;
        localOverview=false;if(suspended==this)suspended=null;
    }
    void Update(){
        if(ElementGameUI.Paused)return;
        if(Input.GetKeyDown(KeyCode.Tab)){ToggleMap();return;}
        if(Input.GetKeyDown(KeyCode.Escape)&&(localOverview||mapAnimating||loading)){ReturnDirectlyToGame();return;}
        if(localOverview&&!loading&&!mapAnimating){localNavigation.TickInspection();if(localNavigation.PlayerReturnRequested)ReturnDirectlyToGame();}
    }
    public void ToggleMap(){if(mapSession||localOverview||loading||mapAnimating)ReturnDirectlyToGame();else OpenLocalOverview();}
    public void ReturnToMap(){
        if(loading||mapAnimating||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit||ChapterRecoilTravel.Active||board&&board.Busy)return;
        if(localOverview)StartCoroutine(LeaveLocalMap());else StartCoroutine(OpenStarChart());
    }
    bool cancelMapOpening;
    IEnumerator LeaveLocalMap(){
        loading=true;cancelMapOpening=false;
        yield return localNavigation.PullBackInspection();
        if(cancelMapOpening){yield return StowMap();yield break;}
        yield return new WaitForEndOfFrame();
        if(cancelMapOpening){yield return StowMap();yield break;}
        ChapterAtlas.ChartArrivalFrame=localNavigation.CaptureInspectionFrame();
        localNavigation.FinishInspection();localOverview=false;yield return OpenStarChart();
    }
    public void ReturnDirectlyToGame(){if(loading||mapAnimating){cancelMapOpening=true;return;}if(mapSession)StartCoroutine(StowMap());else CloseLocalOverview();}
    IEnumerator OpenStarChart(){
        loading=true;string name=gameObject.scene.name;ChapterAtlas.ReturnChapter=name.Contains("01")?0:name.Contains("02")?1:name.Contains("03")?2:3;
        suspended=this;if(!mapSession){playerWasActive=board.player.gameObject.activeSelf;boardWasEnabled=board.enabled;}board.enabled=false;
        var load=SceneManager.LoadSceneAsync(Showcase,LoadSceneMode.Additive);
        if(load==null){ClearArrivalFrame();loading=false;if(mapSession)yield return StowMap();else{ suspended=null;board.enabled=boardWasEnabled;}yield break;}
        yield return load;
        if(cancelMapOpening){ClearArrivalFrame();loading=false;SceneManager.UnloadSceneAsync(Showcase);if(mapSession)yield return StowMap();else{suspended=null;board.enabled=boardWasEnabled;}yield break;}
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(Showcase));
        board.player.gameObject.SetActive(false);suspendedRoots.Clear();
        foreach(var root in gameObject.scene.GetRootGameObjects())if(root.activeSelf)suspendedRoots.Add(root);
        foreach(var root in suspendedRoots)if(root!=transform.root.gameObject)root.SetActive(false);
        transform.root.gameObject.SetActive(false);
    }
    static void ClearArrivalFrame(){if(ChapterAtlas.ChartArrivalFrame)Destroy(ChapterAtlas.ChartArrivalFrame);ChapterAtlas.ChartArrivalFrame=null;}
    public static void ResumeGame(bool keepChartForTransition=false){
        if(!suspended)return;var previous=suspended;suspended=null;
        SceneManager.SetActiveScene(previous.gameObject.scene);
        foreach(var root in previous.suspendedRoots)if(root)root.SetActive(true);
        previous.suspendedRoots.Clear();previous.board.player.gameObject.SetActive(previous.playerWasActive);previous.loading=false;
        if(previous.mapSession)previous.StartCoroutine(previous.StowMap());else previous.board.enabled=previous.boardWasEnabled;
        if(!keepChartForTransition)SceneManager.UnloadSceneAsync(Showcase);
    }
    void OnGUI(){
        if(BlackHoleTravel.Selecting||ChapterRecoilTravel.Active||BlackHoleTravel.InTransit)return;
        if(loading&&ChapterAtlas.ChartArrivalFrame)GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),ChapterAtlas.ChartArrivalFrame);
        if(localOverview||loading){
            GUI.enabled=!loading&&!mapAnimating;
            ElementGameUI.NavigationButton(loading?"loading":"chart",false,ReturnToMap);
            GUI.enabled=true;
            ElementGameUI.NavigationButton("return",true,ReturnDirectlyToGame);
        }
    }
}
}
