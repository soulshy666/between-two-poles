using UnityEngine;
namespace BetweenPoles {
// Chapter footsteps and shared magnetic sounds.
[DisallowMultipleComponent]
public sealed class PlayerFootstepAudio:MonoBehaviour {
    [Range(0f,1f)] public float volume=.56f;
    [Range(0f,2f)] public float iceStepVolume=1.3f;
    [Range(0f,2f)] public float pushVolume=1.6f;
    [Range(0f,1f)] public float mergeVolume=.7f;
    AudioSource steps,pushes,merges,stone,recoil,jump;
    AudioClip jumpLandClip;
    AudioClip recoilClip,recoilChargeClip;
    AudioClip stepClip,pushClip,mergeClip;
    string chapter;
    void Awake(){
        steps=CreateSource();pushes=CreateSource();merges=CreateSource();
        stone=CreateSource();recoil=CreateSource();
        jump=CreateSource();
        jumpLandClip=Resources.Load<AudioClip>("Audio/Player/Actions/JumpDownLand");
        recoilClip=Resources.Load<AudioClip>("Audio/Player/Actions/MagnetRecoil");
        recoilChargeClip=Resources.Load<AudioClip>("Audio/Player/Actions/MagnetRecoilCharge");
        pushClip=Resources.Load<AudioClip>("Audio/Player/magnet_push_grit");
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
    public void PlayStep(){
        if(!isActiveAndEnabled||!Application.isPlaying)return;
        // SharedPlayer lives in DontDestroyOnLoad; its bound ground owns the chapter.
        // Resolve this each walk because portals and recoil can rebind the same actor.
        var binding=GetComponent<IslandSurfaceAnchor>();
        string current=binding&&binding.ground?binding.ground.gameObject.scene.name:gameObject.scene.name;
        if(chapter!=current){
            chapter=current;
            switch(chapter){
                case "Chapter01_IceWorld":
                    stepClip=Resources.Load<AudioClip>("Audio/Player/Footsteps/Chapter01_IceWorld");
                    break;
                case "Chapter02_VerdantWorld":
                case "Chapter03_RingWorld":
                case "Chapter04_LavaWorld":
                    stepClip=Resources.Load<AudioClip>("Audio/Player/Footsteps/"+chapter);
                    break;
                default:stepClip=null;break;
            }
        }
        // Each chapter uses one footstep clip; ice now has a single ground/snow contact.
        if(stepClip)steps.PlayOneShot(stepClip,chapter=="Chapter01_IceWorld"?iceStepVolume:volume);
    }
    public void PlayPush(){
        if(isActiveAndEnabled&&Application.isPlaying&&pushClip)pushes.PlayOneShot(pushClip,pushVolume);
    }
    public void PlayMerge(){if(pushes)pushes.Stop();Play(merges,mergeClip,mergeVolume);}
    public void PlayStoneBlocked(){
        if(isActiveAndEnabled&&Application.isPlaying&&pushClip)stone.PlayOneShot(pushClip,pushVolume);
    }
    public void PlayRecoilCharge(float seconds){
        if(!isActiveAndEnabled||!Application.isPlaying||!recoil||!recoilChargeClip)return;
        recoil.Stop();recoil.volume=1f;
        recoil.pitch=recoilChargeClip.length/Mathf.Max(.05f,seconds);
        recoil.PlayOneShot(recoilChargeClip,2f);
    }
    public void PlayRecoil(){
        if(!isActiveAndEnabled||!Application.isPlaying||!recoil||!recoilClip)return;
        recoil.Stop();recoil.volume=1f;recoil.pitch=1f;
        recoil.PlayOneShot(recoilClip,2f);
    }
    public void StopStoneBlocked(){if(stone)stone.Stop();}
    public void PlayJumpDownLand(){Play(jump,jumpLandClip,1f);}
    public void StopJumpDown(){if(jump)jump.Stop();}
    public void StopMovementSounds(){if(steps)steps.Stop();if(pushes)pushes.Stop();if(merges)merges.Stop();if(recoil)recoil.Stop();StopStoneBlocked();StopJumpDown();}
    void OnDisable(){if(steps)steps.Stop();if(pushes)pushes.Stop();if(merges)merges.Stop();if(recoil)recoil.Stop();StopStoneBlocked();StopJumpDown();}
    void OnDestroy(){if(steps)Destroy(steps);if(pushes)Destroy(pushes);if(merges)Destroy(merges);if(stone)Destroy(stone);if(recoil)Destroy(recoil);if(jump)Destroy(jump);}
}
}
