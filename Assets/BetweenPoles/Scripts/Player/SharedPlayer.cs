using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
// Chapter scene actors are spawn markers; the live actor comes from one shared prefab.
public static class SharedPlayer {
    public static Transform Instance {get;private set;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){Instance=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register(){SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
    static void Loaded(Scene scene,LoadSceneMode mode){
        if(ChapterRecoilTravel.Active||BlackHoleTravel.Selecting)return;
        if(!scene.name.StartsWith("Chapter0")&&Instance)Instance.gameObject.SetActive(false);
    }
    public static void Prepare(GridPlayground board){
        if(!Application.isPlaying||!board.gameObject.scene.name.StartsWith("Chapter0"))return;
        if(Instance&&(ChapterRecoilTravel.Active||BlackHoleTravel.Selecting)){
            if(board.player!=Instance)board.player.gameObject.SetActive(false);return;
        }
        Bind(board,false);
    }
    public static void Bind(GridPlayground board,bool preservePose){
        var marker=board.player;if(!marker)return;
        var position=marker.position;var rotation=marker.rotation;
        if(!Instance){
            var definition=Resources.Load<SharedPlayerDefinition>("SharedPlayerDefinition");
            Instance=definition&&definition.prefab?Object.Instantiate(definition.prefab).transform:marker;Instance.name="Player · Shared";
        }
        if(marker!=Instance){marker.gameObject.SetActive(false);Object.Destroy(marker.gameObject);}
        Instance.SetParent(null,true);Object.DontDestroyOnLoad(Instance.gameObject);
        if(!preservePose)Instance.SetPositionAndRotation(position,rotation);
        Instance.gameObject.SetActive(true);board.player=Instance;board.playerVisual=Instance.Find("Visual");
        var pose=Instance.GetComponent<PlayerPushPose>();if(!pose)pose=Instance.gameObject.AddComponent<PlayerPushPose>();pose.BindBoard(board);
        var binding=Instance.GetComponent<IslandSurfaceAnchor>();if(!binding)binding=Instance.gameObject.AddComponent<IslandSurfaceAnchor>();binding.ground=board;
        var tile=board.TileAt(board.PlayerCell);var owner=tile?tile.GetComponentInParent<IslandSurfaceAnchor>():null;if(owner){binding.center=owner.center;binding.Apply();}
        foreach(var root in board.gameObject.scene.GetRootGameObjects())foreach(var camera in root.GetComponentsInChildren<IslandCamera>(true))if(camera.board==board)camera.player=Instance;
    }
}
}
