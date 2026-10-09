using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
// Retain visited gameplay scenes during a portal trip so magnets and undo history survive.
public sealed partial class BlackHoleTravel : MonoBehaviour {
    public const string Showcase="Assets/BetweenPoles/Scenes/PlanetChapterShowcase.unity";
    static BlackHoleTravel instance;
    public static bool Selecting {get;private set;}
    public static bool InTransit {get{return instance&&instance.busy;}}
    readonly Dictionary<int,List<GameObject>> suspended=new Dictionary<int,List<GameObject>>();
    BlackHolePortal origin;
    Vector2Int entryDirection;
    Quaternion entryFacing;
    bool busy;
    BlackHoleScreenEffect effect;
    void Awake(){effect=gameObject.AddComponent<BlackHoleScreenEffect>();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){instance=null;Selecting=false;}
    public static void Begin(BlackHolePortal portal){
        if(Selecting||!portal.CanArrive)return;
        if(!instance){instance=new GameObject("黑洞穿梭").AddComponent<BlackHoleTravel>();DontDestroyOnLoad(instance.gameObject);}
        if(portal.board.GetComponent<MagnetTestLayouts>()||MagnetTestLayouts.Portals(portal.board).Length>1)instance.StartCoroutine(instance.TestTravel(portal));
        else instance.StartCoroutine(instance.Depart(portal));
    }
    void Suspend(Scene scene){var roots=new List<GameObject>();foreach(var root in scene.GetRootGameObjects())if(root.activeSelf){roots.Add(root);root.SetActive(false);}suspended[scene.handle]=roots;}
    void Resume(Scene scene){List<GameObject> roots;if(suspended.TryGetValue(scene.handle,out roots)){foreach(var root in roots)if(root)root.SetActive(true);suspended.Remove(scene.handle);}}
    IEnumerator Depart(BlackHolePortal portal){
        Selecting=true;busy=true;origin=portal;portal.board.enabled=false;
        entryFacing=portal.board.player.rotation;
        // Walking uses Quaternion.LookRotation: the player's local +Z is forward.
        var recorded=portal.EntryDirection;
        var facing=entryFacing*Vector3.forward;
        entryDirection=Mathf.Abs(facing.x)>Mathf.Abs(facing.z)?new Vector2Int(facing.x>=0?1:-1,0):new Vector2Int(0,facing.z>=0?1:-1);
        if(Mathf.Abs(recorded.x)+Mathf.Abs(recorded.y)==1)entryDirection=recorded;
        entryFacing=Quaternion.LookRotation(new Vector3(entryDirection.x,0,entryDirection.y));
        yield return effect.Absorb(BlackHoleScreenEffect.ScreenCenter(portal),2.80f,portal);
        portal.board.enabled=true;
        ChapterAtlas.ReturnChapter=-1;Suspend(portal.gameObject.scene);
        if(SharedPlayer.Instance)SharedPlayer.Instance.gameObject.SetActive(false);
        var load=SceneManager.LoadSceneAsync(Showcase,LoadSceneMode.Additive);while(!load.isDone)yield return null;
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(Showcase));yield return null;yield return effect.FadeToNavigation();busy=false;
    }
    public static bool IsAvailable(string scene,string room){
        var loaded=SceneManager.GetSceneByPath(scene);if(!loaded.isLoaded)return true;
        foreach(var root in loaded.GetRootGameObjects())foreach(var p in root.GetComponentsInChildren<BlackHolePortal>(true)){ExitPlan plan;if(p.Room==room&&p.CanArrive&&(!instance||PlanExit(p,instance.entryDirection,instance.origin?instance.origin.Cargo:null,out plan)))return true;}
        return false;
    }
    public static void Choose(string scene,string room){if(instance&&!instance.busy&&Selecting)instance.StartCoroutine(instance.Arrive(scene,room));}
    public static void Cancel(){if(instance&&!instance.busy&&Selecting&&instance.origin)instance.StartCoroutine(instance.Arrive(instance.origin.gameObject.scene.path,instance.origin.Room,true));}
    IEnumerator Arrive(string path,string room,bool cancel=false){
        busy=true;
        yield return effect.Absorb(new Vector2(.5f,.5f),.95f);
        var scene=SceneManager.GetSceneByPath(path);
        if(!scene.isLoaded){
            if(!Application.CanStreamedLevelBeLoaded(path)){effect.Hide();busy=false;yield break;}
            ChapterAtlas.DestinationIsland=-1;ChapterAtlas.DestinationScene=null;
            var load=SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);while(!load.isDone)yield return null;
        }
        scene=SceneManager.GetSceneByPath(path);
        BlackHolePortal target=null;
        ExitPlan plan=new ExitPlan();var exitDirection=cancel?-entryDirection:entryDirection;var cargo=cancel?null:origin.Cargo;
        foreach(var root in scene.GetRootGameObjects())foreach(var p in root.GetComponentsInChildren<BlackHolePortal>(true))if(p.Room==room&&(cancel?CancelPlan(p,exitDirection,out plan):PlanExit(p,exitDirection,cargo,out plan))){target=p;break;}
        if(!target){if(!suspended.ContainsKey(scene.handle))Suspend(scene);effect.Hide();busy=false;var atlas=FindObjectOfType<ChapterAtlas>();if(atlas)atlas.ShowPortalBlocked();yield break;}
        Selecting=false;ChapterAtlas.ReturnChapter=-1;
        Resume(scene);SceneManager.SetActiveScene(scene);
        var board=target.board;board.enabled=false;SharedPlayer.Bind(board,true);target.Disarm();
        bool transferred=cargo&&origin.board!=board;
        if(transferred){
            var sourceBoard=origin.board;
            var carried=sourceBoard.magnets.Where(m=>m&&m.transform.IsChildOf(cargo.transform)).ToArray();
            sourceBoard.magnets=sourceBoard.magnets.Except(carried).ToArray();
            sourceBoard.ReleasePortalGeometry(carried);
            cargo.transform.SetParent(board.transform,true);
            board.magnets=board.magnets.Concat(carried).ToArray();
            // Cross-scene ownership changes establish a new checkpoint on both boards.
            sourceBoard.CaptureInitialState();
        }
        board.player.position=new Vector3(target.tile.transform.position.x,target.tile.surfaceHeight,target.tile.transform.position.z);
        board.player.rotation=entryFacing;
        board.SendMessage("NotifyLanding",target.Cell);
        PlayerPrefs.SetInt(ChapterAtlas.Key(path,room),1);PlayerPrefs.Save();
        var showcase=SceneManager.GetSceneByPath(Showcase);if(showcase.isLoaded){var unload=SceneManager.UnloadSceneAsync(showcase);while(!unload.isDone)yield return null;}
        // Let the new chapter's Start methods finish before restoring input.
        yield return null;
        var scale=board.player.localScale;board.player.localScale=Vector3.zero;
        yield return effect.Emerge(target);board.player.localScale=scale;
        yield return Eject(target,exitDirection,cargo,plan);
        if(transferred)board.CaptureInitialState();
        board.enabled=true;busy=false;
    }
}
}
