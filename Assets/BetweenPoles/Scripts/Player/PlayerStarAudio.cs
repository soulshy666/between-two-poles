using UnityEngine;
namespace BetweenPoles {
// One achievement cue follows the collection clock, including pauses/slow frames.
[DisallowMultipleComponent]
public sealed class PlayerStarAudio:MonoBehaviour {
    [Range(0f,1f)] public float volume=1f;
    AudioSource source;
    GameBackgroundMusic music;
    bool started,paused;
    void Awake(){
        source=gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;
        source.clip=Resources.Load<AudioClip>("Audio/Player/Actions/StarCollect");
    }
    public static PlayerStarAudio Begin(Transform actor){
        if(!actor||!Application.isPlaying)return null;
        var audio=actor.GetComponent<PlayerStarAudio>();
        if(!audio)audio=actor.gameObject.AddComponent<PlayerStarAudio>();
        audio.Stop();return audio;
    }
    // seconds is measured from hand contact: toss .90, flash 1.90, finish 4.20.
    public void Sample(float seconds){
        if(!isActiveAndEnabled||!source||!source.clip||seconds<0)return;
        if(seconds>=source.clip.length){Stop();return;}
        if(Time.timeScale<=0){if(started&&!paused){source.Pause();paused=true;}return;}
        source.volume=volume;
        if(!started){
            source.time=seconds;source.Play();started=true;
            music=FindObjectOfType<GameBackgroundMusic>();
            if(music)music.BeginStarCue(this);
        }
        if(paused){source.UnPause();paused=false;}
        // The animation advances at most .05s/frame; resync after a long frame.
        if(Mathf.Abs(source.time-seconds)>.12f)source.time=seconds;
    }
    public void Stop(){
        if(source)source.Stop();started=false;paused=false;
        if(music)music.EndStarCue(this);music=null;
    }
    void OnDisable(){Stop();}
    void OnDestroy(){if(source)Destroy(source);}
}
}
