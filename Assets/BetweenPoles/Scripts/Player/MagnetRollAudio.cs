using UnityEngine;
namespace BetweenPoles {
// Refreshed by actual rolling frames, including independently moving rings.
[DisallowMultipleComponent]
public sealed class MagnetRollAudio:MonoBehaviour {
    AudioSource source;
    int rollingFrame=-1;
    bool paused;
    public static void Tick(MagnetPiece ring){
        if(!Application.isPlaying||!ring)return;
        var audio=ring.GetComponent<MagnetRollAudio>();
        if(!audio)audio=ring.gameObject.AddComponent<MagnetRollAudio>();
        audio.rollingFrame=Time.frameCount;
    }
    public static void Stop(MagnetPiece ring){
        if(!ring)return;
        var audio=ring.GetComponent<MagnetRollAudio>();
        if(audio)audio.StopLoop();
    }
    void Awake(){
        source=gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=true;source.spatialBlend=0;
        source.dopplerLevel=0;source.volume=.65f;
        source.clip=Resources.Load<AudioClip>("Audio/Player/Actions/MagnetRollLoop");
    }
    void LateUpdate(){
        // No timer can outlive a cancelled coroutine, undo, or skipped movement.
        if(rollingFrame!=Time.frameCount){StopLoop();return;}
        if(ElementGameUI.Paused||Time.timeScale<=0){
            if(source.isPlaying){source.Pause();paused=true;}
            return;
        }
        if(paused){source.UnPause();paused=false;}
        if(source.clip&&!source.isPlaying)source.Play();
    }
    void StopLoop(){rollingFrame=-1;paused=false;if(source)source.Stop();}
    void OnDisable(){StopLoop();}
    void OnDestroy(){if(source)Destroy(source);}
}
}
