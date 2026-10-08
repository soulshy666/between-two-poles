using UnityEngine;
namespace BetweenPoles {
// One complete two-footstep clip per walking action, never gated by key release.
[DisallowMultipleComponent]
public sealed class PlayerFootstepAudio:MonoBehaviour {
    [Range(0f,1f)] public float volume=.7f;
    AudioSource source;
    AudioClip clip;
    string chapter;

    void Awake(){
        source=gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=false;
        // The curved-world camera is distant from the logical player position.
        source.spatialBlend=0;source.dopplerLevel=0;
    }
    public void PlayStep(){
        if(!isActiveAndEnabled||!Application.isPlaying)return;
        // The actor can move to another scene during interplanetary travel.
        string current=gameObject.scene.name;
        if(chapter!=current){
            chapter=current;
            switch(chapter){
                case "Chapter01_IceWorld":
                case "Chapter02_VerdantWorld":
                case "Chapter03_RingWorld":
                case "Chapter04_LavaWorld":
                    clip=Resources.Load<AudioClip>("Audio/Player/Footsteps/"+chapter);
                    break;
                default:clip=null;break;
            }
        }
        // Let each one-shot finish, even after stopping or starting the next tile.
        if(clip)source.PlayOneShot(clip,volume);
    }
    void OnDestroy(){if(source)Destroy(source);}
}
}
