using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
// Retain visited gameplay scenes during a portal trip so magnets and undo history survive.
public sealed class BlackHoleTravel : MonoBehaviour {
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
        instance.StartCoroutine(instance.Depart(portal));
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
        foreach(var root in loaded.GetRootGameObjects())foreach(var p in root.GetComponentsInChildren<BlackHolePortal>(true)){GridTile exit;if(p.Room==room&&p.CanArrive&&(!instance||p.TryExit(instance.entryDirection,out exit)))return true;}
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
        GridTile exitTile=null;Vector3 exitPosition=Vector3.zero;var exitDirection=cancel?-entryDirection:entryDirection;
        foreach(var root in scene.GetRootGameObjects())foreach(var p in root.GetComponentsInChildren<BlackHolePortal>(true))if(p.Room==room&&p.TryExitLanding(exitDirection,out exitTile,out exitPosition)){target=p;break;}
        if(!target){if(!suspended.ContainsKey(scene.handle))Suspend(scene);effect.Hide();busy=false;var atlas=FindObjectOfType<ChapterAtlas>();if(atlas)atlas.ShowPortalBlocked();yield break;}
        Selecting=false;ChapterAtlas.ReturnChapter=-1;
        Resume(scene);SceneManager.SetActiveScene(scene);
        var board=target.board;board.enabled=false;SharedPlayer.Bind(board,true);target.Disarm();
        board.player.position=new Vector3(target.tile.transform.position.x,target.tile.surfaceHeight,target.tile.transform.position.z);
        board.player.rotation=entryFacing;
        board.SendMessage("NotifyLanding",target.Cell);
        PlayerPrefs.SetInt(ChapterAtlas.Key(path,room),1);PlayerPrefs.Save();
        var showcase=SceneManager.GetSceneByPath(Showcase);if(showcase.isLoaded){var unload=SceneManager.UnloadSceneAsync(showcase);while(!unload.isDone)yield return null;}
        // Let the new chapter's Start methods finish before restoring input.
        yield return null;
        yield return effect.Emerge(target);
        // Preserve travel direction and glide out onto the adjacent, validated floor.
        var start=board.player.position;var end=exitPosition;
        for(float t=0;t<.48f;t+=Time.unscaledDeltaTime){float a=Mathf.Clamp01(t/.48f);float move=1-Mathf.Pow(1-a,3);var raised=start;raised.y=Mathf.Max(start.y,end.y);board.player.position=a<.35f?Vector3.Lerp(start,raised,Mathf.SmoothStep(0,1,a/.35f)):Vector3.Lerp(raised,end,Mathf.SmoothStep(0,1,(a-.35f)/.65f));yield return null;}
        board.player.position=end;board.SendMessage("NotifyLanding",target.Cell+exitDirection);
        board.enabled=true;busy=false;
    }
}
}
