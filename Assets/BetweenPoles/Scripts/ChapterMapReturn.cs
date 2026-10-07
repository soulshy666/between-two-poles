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
        if(!scene.name.StartsWith("Chapter0"))return;
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
    void Visit(GridTile tile){var anchor=tile.GetComponentInParent<IslandSurfaceAnchor>();if(!anchor||!anchor.center)return;string key=ChapterAtlas.Key(gameObject.scene.path,anchor.center.name);if(PlayerPrefs.GetInt(key,0)==0){PlayerPrefs.SetInt(key,1);PlayerPrefs.Save();}}
    void OnDestroy(){if(board)board.Landed-=Visit;}
    public void ReturnToMap(){if(loading||BlackHoleTravel.InTransit||board&&board.Busy)return;loading=true;string name=gameObject.scene.name;ChapterAtlas.ReturnChapter=name.Contains("01")?0:name.Contains("02")?1:name.Contains("03")?2:3;SceneManager.LoadSceneAsync(Showcase);}
    void OnGUI(){if(ChapterRecoilTravel.Active)return;if(BlackHoleTravel.InTransit)return;var old=GUI.matrix;float scale=Mathf.Max(.7f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.enabled=!loading&&!BlackHoleTravel.InTransit&&(!board||!board.Busy);if(GUI.Button(new Rect(22,22,165,42),loading?"返回地图中…":"← 章节地图"))ReturnToMap();GUI.enabled=true;GUI.matrix=old;}
}
}
