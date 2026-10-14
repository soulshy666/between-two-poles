using System.Collections;
using UnityEngine;
namespace BetweenPoles {
// One shared-player channel: repeated undo/reset cues replace, never stack.
[DisallowMultipleComponent]
public sealed class HistoryActionAudio:MonoBehaviour {
    [Range(0f,1f)] public float undoVolume=.6f;
    [Range(0f,1f)] public float resetVolume=.6f;
    readonly AudioClip[] undoClips=new AudioClip[5];
    readonly System.Random random=new System.Random();
    AudioClip resetClip;
    AudioSource source;
    Coroutine replacement;
    int lastUndo=-1;
    public static HistoryActionAudio For(Transform actor){
        if(!actor||!Application.isPlaying)return null;
        var audio=actor.GetComponent<HistoryActionAudio>();
        return audio?audio:actor.gameObject.AddComponent<HistoryActionAudio>();
    }
    void Awake(){
        source=gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=false;source.spatialBlend=0;source.dopplerLevel=0;
        for(int i=0;i<undoClips.Length;i++)undoClips[i]=Resources.Load<AudioClip>("Audio/Player/Actions/Undo"+(i+1));
        resetClip=Resources.Load<AudioClip>("Audio/Player/Actions/IslandReset");
    }
    public void PlayUndo(){
        // Keep sound variation independent of gameplay's random stream.
        int next=random.Next(lastUndo<0?undoClips.Length:undoClips.Length-1);
        if(lastUndo>=0&&next>=lastUndo)next++;
        lastUndo=next;Play(undoClips[next],undoVolume);
    }
    public void PlayReset(){Play(resetClip,resetVolume);}
    void Play(AudioClip clip,float volume){
        if(!isActiveAndEnabled||!source||!clip)return;
        if(replacement!=null){StopCoroutine(replacement);replacement=null;}
        if(source.isPlaying)replacement=StartCoroutine(Replace(clip,volume));
        else StartClip(clip,volume);
    }
    IEnumerator Replace(AudioClip clip,float volume){
        // Brief release prevents a click when an unusually fast press cuts a note.
        float start=source.volume,elapsed=0;
        while(elapsed<.006f){
            yield return null;
            elapsed+=Time.unscaledDeltaTime;
            source.volume=start*(1-Mathf.Clamp01(elapsed/.006f));
        }
        StartClip(clip,volume);replacement=null;
    }
    void StartClip(AudioClip clip,float volume){
        source.Stop();source.clip=clip;source.volume=volume;source.pitch=1f;source.Play();
    }
    void OnDisable(){
        if(replacement!=null){StopCoroutine(replacement);replacement=null;}
        if(source)source.Stop();
    }
    void OnDestroy(){if(source)Destroy(source);}
}
}
