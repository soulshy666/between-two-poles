using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetweenPoles {
// A single music source survives chapter changes without restarting or overlapping.
[DisallowMultipleComponent]
public sealed class GameBackgroundMusic:MonoBehaviour {
    [Range(0f,1f)] public float volume=1f;
    AudioSource source;
    IceCrashIntro[] openings;
    GridPlayground[] boards;
    CoverMenu cover;
    bool started,held;
    float fade;
    const float StarDuckVolume=.2f;
    float duck=1f;
    PlayerStarAudio starCue;
    IslandDiscoveryAudio discoveryCue;
    AudioListener fallbackListener;
    AudioListener[] sceneListeners;
    public static void SyncListener(){foreach(var music in FindObjectsOfType<GameBackgroundMusic>())music.LateUpdate();}

    // The atlas has no listener, and suspends the chapter camera. Keep audio
    // audible there without introducing a second listener during gameplay.
    void LateUpdate(){
        if(!fallbackListener){fallbackListener=GetComponent<AudioListener>();if(!fallbackListener)fallbackListener=gameObject.AddComponent<AudioListener>();fallbackListener.enabled=false;}
        bool otherListener=false;
        if(sceneListeners!=null)foreach(var listener in sceneListeners)
            if(listener&&listener!=fallbackListener&&listener.isActiveAndEnabled){otherListener=true;break;}
        fallbackListener.enabled=!otherListener;
    }

    public void BeginStarCue(PlayerStarAudio cue){starCue=cue;}
    public void EndStarCue(PlayerStarAudio cue){if(starCue==cue)starCue=null;}
    public void BeginDiscoveryCue(IslandDiscoveryAudio cue){discoveryCue=cue;}
    public void EndDiscoveryCue(IslandDiscoveryAudio cue){if(discoveryCue==cue)discoveryCue=null;}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize(){
        // Also reuse the source when entering Play Mode without a scene/domain reload.
        if(FindObjectOfType<GameBackgroundMusic>())return;
        new GameObject("Game Background Music").AddComponent<GameBackgroundMusic>();
    }

    void Awake(){
        DontDestroyOnLoad(gameObject);
        source=gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;
        source.loop=true;
        source.spatialBlend=0;
        source.dopplerLevel=0;
        source.volume=0;
        source.clip=Resources.Load<AudioClip>("Audio/GameBackgroundMusic");
        if(!source.clip)Debug.LogWarning("Game background music clip is missing.",this);
    }
    void OnEnable(){SceneManager.sceneLoaded+=SceneLoaded;SceneManager.sceneUnloaded+=SceneUnloaded;RefreshOpenings();}
    void OnDisable(){SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneUnloaded-=SceneUnloaded;if(source)source.Pause();if(fallbackListener)fallbackListener.enabled=false;held=true;}
    void SceneLoaded(Scene scene,LoadSceneMode mode){RefreshOpenings();LateUpdate();UpdateMusic();}
    void SceneUnloaded(Scene scene){RefreshOpenings();LateUpdate();UpdateMusic();}
    void RefreshOpenings(){openings=FindObjectsOfType<IceCrashIntro>(true);boards=FindObjectsOfType<GridPlayground>(true);cover=FindObjectOfType<CoverMenu>();sceneListeners=FindObjectsOfType<AudioListener>(true);}
    void Update(){UpdateMusic();}
    void UpdateMusic(){
        if(!source||!source.clip)return;
        if(ChapterFlightTitle.HoldsMusic){if(!held)source.Pause();held=true;fade=0;source.volume=0;return;}
        // Map inspection temporarily hides the gameplay roots; it is still the
        // same music session, including atlas loading and the return animation.
        bool gameplay=ChapterRecoilTravel.Active||ChapterMapReturn.HasSuspendedGame;
        if(boards!=null)foreach(var board in boards)
            if(board&&board.gameObject.activeInHierarchy&&!ChapterAtlas.IsInspectionScene(board.gameObject.scene)&&!ChapterRecoilTravel.IsPreparing(board.gameObject.scene)){gameplay=true;break;}
        if(!gameplay||(cover&&cover.isActiveAndEnabled)||ElementGameUI.Paused){
            if(!held)source.Pause();
            held=true;
            if(!gameplay||(cover&&cover.isActiveAndEnabled)){fade=0;source.volume=0;}
            return;
        }
        float target=starCue&&starCue.isActiveAndEnabled?StarDuckVolume:1f;
        if(discoveryCue&&discoveryCue.isActiveAndEnabled)target=Mathf.Min(target,.4f);
        // Overlapping rewards use the quieter target; release only when each cue ends.
        // Lower quickly and restore gradually without restarting the music.
        duck=Mathf.MoveTowards(duck,target,Time.unscaledDeltaTime*(1-StarDuckVolume)/(target<duck?.2f:.8f));
        bool opening=false;
        if(openings!=null)foreach(var intro in openings)
            if(intro&&intro.isActiveAndEnabled&&intro.Playing&&!ChapterAtlas.IsInspectionScene(intro.gameObject.scene)&&!ChapterRecoilTravel.IsPreparing(intro.gameObject.scene)){opening=true;break;}
        if(opening){
            if(!held)source.Pause();
            held=true;fade=0;source.volume=0;return;
        }
        // Playing ends only after the recovery animation and input unlock.
        if(!started){source.Play();started=true;}
        else if(held)source.UnPause();
        held=false;fade=Mathf.MoveTowards(fade,1,Time.deltaTime/1.2f);
        source.volume=volume*fade*duck;
    }
    void OnValidate(){if(source)source.volume=volume*fade*duck;}
}
}
