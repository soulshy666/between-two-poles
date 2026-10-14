using UnityEngine;
namespace BetweenPoles {
// Shared-player ownership keeps the cargo sound alive after its mesh is hidden.
[DisallowMultipleComponent]
public sealed class PortalAudio:MonoBehaviour {
    [Range(0f,1f)] public float volume=1f;
    const float LandingMarker=1.05f;
    readonly AudioSource[] sources=new AudioSource[2];
    readonly AudioClip[] entries=new AudioClip[2],exits=new AudioClip[2],landings=new AudioClip[2];
    readonly bool[] unscaled=new bool[2],paused=new bool[2];
    public static PortalAudio For(Transform actor){
        if(!actor||!Application.isPlaying)return null;
        var audio=actor.GetComponent<PortalAudio>();
        return audio?audio:actor.gameObject.AddComponent<PortalAudio>();
    }
    void Awake(){
        for(int i=0;i<2;i++){
            sources[i]=gameObject.AddComponent<AudioSource>();
            sources[i].playOnAwake=false;sources[i].loop=false;
            sources[i].spatialBlend=0;sources[i].dopplerLevel=0;
            string prefix="Audio/Player/Actions/Portal"+(i==0?"Player":"Magnet");
            entries[i]=Resources.Load<AudioClip>(prefix+"Enter");
            var clip=Resources.Load<AudioClip>(prefix+"Exit");
            if(!clip){Debug.LogWarning("Missing portal exit sound: "+prefix,this);continue;}
            int split=Mathf.Clamp(Mathf.RoundToInt(LandingMarker*clip.frequency),1,clip.samples-1);
            exits[i]=Slice(clip,0,split);landings[i]=Slice(clip,split,clip.samples-split);
        }
    }
    static AudioClip Slice(AudioClip clip,int offset,int samples){
        var data=new float[samples*clip.channels];
        if(!clip.GetData(data,offset))return null;
        var result=AudioClip.Create(clip.name+" phase "+offset,samples,clip.channels,clip.frequency,false);
        result.SetData(data,0);return result;
    }
    void Play(int channel,AudioClip clip,bool realTime,float pitch=1f){
        if(!isActiveAndEnabled||!clip)return;
        if(BlackHoleTravel.SkipTransitionActive){StopAll();return;}
        var source=sources[channel];source.Stop();source.clip=clip;
        source.volume=volume;source.pitch=pitch;unscaled[channel]=realTime;paused[channel]=false;source.Play();
    }
    public void Enter(bool magnet,float seconds,bool realTime=false){
        int i=magnet?1:0;var clip=entries[i];
        if(clip)Play(i,clip,realTime,clip.length/Mathf.Max(.05f,seconds));
    }
    public void Exit(bool magnet){int i=magnet?1:0;Play(i,exits[i],true);}
    public void Land(bool magnet){int i=magnet?1:0;Play(i,landings[i],true);}
    public void Stop(bool magnet){int i=magnet?1:0;if(sources[i])sources[i].Stop();paused[i]=false;}
    public void StopAll(){Stop(false);Stop(true);}
    void Update(){
        for(int i=0;i<2;i++){
            var source=sources[i];if(!source)continue;
            // Entry uses the movement clock; portal exit animations use unscaled time.
            if(ElementGameUI.Paused||(!unscaled[i]&&Time.timeScale<=0)){if(!paused[i]&&source.isPlaying){source.Pause();paused[i]=true;}}
            else if(paused[i]){source.UnPause();paused[i]=false;}
        }
    }
    void OnDisable(){StopAll();}
    void OnDestroy(){for(int i=0;i<2;i++){if(sources[i])Destroy(sources[i]);if(exits[i])Destroy(exits[i]);if(landings[i])Destroy(landings[i]);}}
}
}
