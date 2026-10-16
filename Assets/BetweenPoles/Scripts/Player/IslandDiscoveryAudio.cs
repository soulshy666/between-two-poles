using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles {
// Kept on the shared player so a chapter handoff cannot cut off the discovery.
[DisallowMultipleComponent]
public sealed class IslandDiscoveryAudio:MonoBehaviour {
    [Range(0f,1f)] public float volume=1f;
    AudioSource source;
    GameBackgroundMusic music;
    bool paused;
    static readonly HashSet<string> visited=new HashSet<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetVisits(){visited.Clear();}
    public static string[] ExportVisits(){var result=new string[visited.Count];visited.CopyTo(result);return result;}
    public static void ImportVisits(string[] values){visited.Clear();if(values!=null)foreach(var value in values)visited.Add(value);}
    static string VisitKey(GridPlayground board,Transform island){
        var scene=board.gameObject.scene;
        string room=island.name;
        var window=board.GetComponent<FiveIslandWindow>();
        if(window)foreach(var candidate in window.rooms)
            if(candidate.center==island&&!string.IsNullOrEmpty(candidate.id)){room=candidate.id;break;}
        return (string.IsNullOrEmpty(scene.path)?scene.name:scene.path)+"\n"+room;
    }
    static bool CanTrack(GridPlayground board,Transform island){
        return Application.isPlaying&&board&&island&&!ChapterAtlas.IsInspectionScene(board.gameObject.scene);
    }
    public static void Seed(GridPlayground board,Transform island){
        if(CanTrack(board,island))visited.Add(VisitKey(board,island));
    }
    public static void Arrive(GridPlayground board,Transform island){
        if(!CanTrack(board,island)||!board.player||board.RecoilFlying)return;
        // The first cross-chapter arrival belongs to the title sequence, not a reward cue.
        if(ChapterRecoilTravel.Active&&board.gameObject.scene.name=="Chapter02_VerdantWorld"){Seed(board,island);return;}
        if(board.OpeningCinematic){Seed(board,island);return;}
        if(visited.Add(VisitKey(board,island)))Play(board.player);
    }
    void Awake(){
        source=gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;
        source.clip=Resources.Load<AudioClip>("Audio/Player/Actions/IslandDiscovery");
    }
    public static void Play(Transform actor){
        if(!actor||!Application.isPlaying)return;
        var audio=actor.GetComponent<IslandDiscoveryAudio>();
        if(!audio)audio=actor.gameObject.AddComponent<IslandDiscoveryAudio>();
        if(!audio.isActiveAndEnabled||!audio.source.clip)return;
        audio.source.Stop();audio.source.volume=audio.volume;
        audio.source.Play();audio.paused=false;
        audio.music=FindObjectOfType<GameBackgroundMusic>();
        if(audio.music)audio.music.BeginDiscoveryCue(audio);
    }
    void Update(){
        if(!source)return;
        if(Time.timeScale<=0){if(!paused&&source.isPlaying){source.Pause();paused=true;}}
        else if(paused){source.UnPause();paused=false;}
        if(!paused&&!source.isPlaying)ReleaseMusic();
    }
    void ReleaseMusic(){if(music)music.EndDiscoveryCue(this);music=null;}
    void OnDisable(){if(source)source.Stop();paused=false;ReleaseMusic();}
    void OnDestroy(){ReleaseMusic();if(source)Destroy(source);}
}
}
