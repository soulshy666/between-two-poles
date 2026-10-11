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
        if(!scene.name.StartsWith("Chapter0")||ChapterAtlas.IsInspectionScene(scene))return;
        foreach(var root in scene.GetRootGameObjects()){var b=root.GetComponentInChildren<GridPlayground>();if(b&&!b.GetComponent<ChapterMapReturn>()){b.gameObject.AddComponent<ChapterMapReturn>();break;}}
    }
    IEnumerator Start(){
        board=GetComponent<GridPlayground>();foreach(var candidate in FindObjectsOfType<FiveIslandWindow>())if(candidate.gameObject.scene==gameObject.scene)window=candidate;yield return null;
        if(!board)yield break;
        if(ChapterAtlas.DestinationScene==gameObject.scene.path&&ChapterAtlas.DestinationIsland>0){
            GridTile best=null;float distance=float.PositiveInfinity;
            foreach(var t in board.tiles){var anchor=t.GetComponentInParent<IslandSurfaceAnchor>();if(!anchor||anchor.center.name!=ChapterAtlas.DestinationRoom||t.blocked)continue;var cell=new Vector2Int(Mathf.RoundToInt(t.transform.position.x/board.cellSize),Mathf.RoundToInt(t.transform.position.z/board.cellSize));if(board.MagnetAt(cell))continue;float d=(t.transform.position-anchor.center.position).sqrMagnitude;if(d<distance){distance=d;best=t;}}
            if(best){board.player.position=new Vector3(best.transform.position.x,best.surfaceHeight,best.transform.position.z);board.CaptureInitialState();board.SendMessage("NotifyLanding",board.PlayerCell);if(window)window.Show(ChapterAtlas.DestinationIsland);}
        }
        ChapterAtlas.DestinationIsland=-1;board.Landed+=Visit;
        var tile=board.TileAt(board.PlayerCell);if(tile)Visit(tile);
    }
    void Visit(GridTile tile){var anchor=tile.GetComponentInParent<IslandSurfaceAnchor>();if(!anchor||!anchor.center)return;PlayerPrefs.SetInt(ChapterAtlas.WorldVisitKey(gameObject.scene.path),1);string key=ChapterAtlas.Key(gameObject.scene.path,anchor.center.name);if(PlayerPrefs.GetInt(key,0)==0){PlayerPrefs.SetInt(key,1);PlayerPrefs.Save();}}
    void OnDestroy(){if(localOverview)CloseLocalOverview();if(board)board.Landed-=Visit;}
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
    public bool IsLocalOverview {get{return localOverview;}}
    public void OpenLocalOverview(){
        if(localOverview||loading||!board||board.Busy||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit||ChapterRecoilTravel.Active)return;
        suspended=this;playerWasActive=board.player.gameObject.activeSelf;boardWasEnabled=board.enabled;
        board.enabled=false;
        if(!localNavigation)localNavigation=gameObject.AddComponent<BlackHoleTravel>();
        localNavigation.BeginInspection(board);board.player.gameObject.SetActive(false);localOverview=true;
    }
    public void CloseLocalOverview(){
        if(!localOverview)return;
        localNavigation.FinishInspection();board.player.gameObject.SetActive(playerWasActive);board.enabled=boardWasEnabled;
        localOverview=false;if(suspended==this)suspended=null;
    }
    void Update(){if(!localOverview)return;if(Input.GetKeyDown(KeyCode.Escape)){ReturnDirectlyToGame();return;}if(!loading)localNavigation.TickInspection();}
    public void ReturnToMap(){
        if(loading||BlackHoleTravel.Selecting||BlackHoleTravel.InTransit||ChapterRecoilTravel.Active||board&&board.Busy)return;
        if(localOverview)StartCoroutine(LeaveLocalMap());else StartCoroutine(OpenStarChart());
    }
    bool cancelMapOpening;
    IEnumerator LeaveLocalMap(){
        loading=true;cancelMapOpening=false;
        ChapterAtlas.ChartArrivalFrame=localNavigation.CaptureInspectionFrame();
        CloseLocalOverview();yield return OpenStarChart();
    }
    public void ReturnDirectlyToGame(){if(loading){cancelMapOpening=true;return;}CloseLocalOverview();}
    IEnumerator OpenStarChart(){
        loading=true;string name=gameObject.scene.name;ChapterAtlas.ReturnChapter=name.Contains("01")?0:name.Contains("02")?1:name.Contains("03")?2:3;
        suspended=this;playerWasActive=board.player.gameObject.activeSelf;boardWasEnabled=board.enabled;board.enabled=false;
        var load=SceneManager.LoadSceneAsync(Showcase,LoadSceneMode.Additive);
        if(load==null){loading=false;suspended=null;board.enabled=boardWasEnabled;yield break;}
        yield return load;
        if(cancelMapOpening){loading=false;suspended=null;board.enabled=boardWasEnabled;SceneManager.UnloadSceneAsync(Showcase);yield break;}
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(Showcase));
        board.player.gameObject.SetActive(false);suspendedRoots.Clear();
        foreach(var root in gameObject.scene.GetRootGameObjects())if(root.activeSelf)suspendedRoots.Add(root);
        foreach(var root in suspendedRoots)if(root!=transform.root.gameObject)root.SetActive(false);
        transform.root.gameObject.SetActive(false);
    }
    public static void ResumeGame(){
        if(!suspended)return;var previous=suspended;suspended=null;
        SceneManager.SetActiveScene(previous.gameObject.scene);
        foreach(var root in previous.suspendedRoots)if(root)root.SetActive(true);
        previous.suspendedRoots.Clear();previous.board.player.gameObject.SetActive(previous.playerWasActive);previous.board.enabled=previous.boardWasEnabled;previous.loading=false;
        SceneManager.UnloadSceneAsync(Showcase);
    }
    void OnGUI(){
        if(BlackHoleTravel.Selecting||ChapterRecoilTravel.Active||BlackHoleTravel.InTransit)return;
        if(loading&&ChapterAtlas.ChartArrivalFrame)GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),ChapterAtlas.ChartArrivalFrame);
        var old=GUI.matrix;float scale=Mathf.Max(.7f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        GUI.enabled=!loading&&(!board||!board.Busy);
        if(GUI.Button(new Rect(22,22,185,42),loading?"打开航图中…":localOverview?"← 星际航图":"查看小岛地图")){
            if(localOverview)ReturnToMap();else OpenLocalOverview();
        }
        if(localOverview||loading){
            GUI.enabled=true;
            GUI.Label(new Rect(224,25,850,42),"拖拽查看已探索的小岛，滚轮缩放（仅检视）",new GUIStyle(GUI.skin.label){fontSize=18});
            if(GUI.Button(new Rect(Screen.width/scale-190,22,168,42),"返回游戏（Esc）"))ReturnDirectlyToGame();
        }
        GUI.enabled=true;GUI.matrix=old;
    }
}
}
