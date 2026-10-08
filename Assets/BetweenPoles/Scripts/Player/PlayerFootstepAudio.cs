using UnityEngine;
namespace BetweenPoles {
// Shared action sounds across all worlds, including the mechanics test scene.
[DisallowMultipleComponent]
public sealed class PlayerFootstepAudio:MonoBehaviour {
    [Range(0f,1f)] public float volume=.7f;
    [Range(0f,1f)] public float pushVolume=.7f;
    [Range(0f,1f)] public float mergeVolume=.8f;
    AudioSource steps,pushes,merges;
    AudioClip stepClip,pushClip,mergeClip;
    void Awake(){
        steps=CreateSource();pushes=CreateSource();merges=CreateSource();
        stepClip=Resources.Load<AudioClip>("Audio/Player/Actions/Footstep");
        pushClip=Resources.Load<AudioClip>("Audio/Player/Actions/MagnetPush");
        mergeClip=Resources.Load<AudioClip>("Audio/Player/Actions/MagnetMerge");
    }
    AudioSource CreateSource(){
        var s=gameObject.AddComponent<AudioSource>();
        s.playOnAwake=false;s.loop=false;s.spatialBlend=0;s.dopplerLevel=0;
        return s;
    }
    public static PlayerFootstepAudio For(Transform actor){
        if(!actor)return null;
        var audio=actor.GetComponent<PlayerFootstepAudio>();
        return audio?audio:actor.gameObject.AddComponent<PlayerFootstepAudio>();
    }
    void Play(AudioSource s,AudioClip clip,float gain,float pitch=1f){
        if(!isActiveAndEnabled||!Application.isPlaying||!s||!clip)return;
        // Separate channels keep a footstep from cutting off a magnetic contact.
        s.clip=clip;s.volume=gain;s.pitch=pitch;s.Play();
    }
    int stepVariation;
    public void PlayStep(){
        // Small deterministic variations keep one ice-footstep asset from sounding
        // mechanical without consuming the gameplay random-number stream.
        float phase=++stepVariation*2.399963f;
        float pitch=1f+Mathf.Sin(phase)*.035f;
        float gain=volume*(.96f+Mathf.Sin(phase*1.71f)*.04f);
        Play(steps,stepClip,gain,pitch);
    }
    public void PlayPush(){Play(pushes,pushClip,pushVolume);}
    public void PlayMerge(){if(pushes)pushes.Stop();Play(merges,mergeClip,mergeVolume);}
    void OnDisable(){if(steps)steps.Stop();if(pushes)pushes.Stop();if(merges)merges.Stop();}
    void OnDestroy(){if(steps)Destroy(steps);if(pushes)Destroy(pushes);if(merges)Destroy(merges);}
}
}
