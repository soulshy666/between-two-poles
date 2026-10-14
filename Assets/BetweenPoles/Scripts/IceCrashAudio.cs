using UnityEngine;

namespace BetweenPoles {
// Driven by the cinematic clock, so the impact freeze never repeats the strike.
[DisallowMultipleComponent]
public sealed class IceCrashAudio:MonoBehaviour {
    [Range(0f,1f)] public float volume=1f;
    AudioSource flight,impact,reveal;
    bool struck,paused,revealed;

    void Awake(){
        flight=CreateSource("ShuttleApproach");
        impact=CreateSource("ShuttleImpact");
        reveal=CreateSource("ColorReveal");
    }
    AudioSource CreateSource(string clip){
        var source=gameObject.AddComponent<AudioSource>();
        source.playOnAwake=false;source.loop=false;source.spatialBlend=0;
        source.dopplerLevel=0;source.volume=volume;
        source.clip=Resources.Load<AudioClip>("Audio/Intro/"+clip);
        return source;
    }
    public void Begin(){
        Stop();struck=false;paused=false;revealed=false;
        if(flight.clip)flight.Play();
    }
    public void Sample(float time){
        if(!Application.isPlaying)return;
        if(time>=IceCrashIntro.ImpactTime){
            if(struck)return;
            struck=true;flight.Stop();
            if(impact.clip)impact.Play();
        }else if(flight.clip&&!paused){
            // The animation caps long frames; keep the flyby on that same clock.
            float sample=Mathf.Min(time,flight.clip.length-.001f);
            if(Mathf.Abs(flight.time-sample)>.08f){flight.time=sample;if(!flight.isPlaying)flight.Play();}
        }
    }
    public void SampleReveal(float age,float expandSeconds){
        if(!Application.isPlaying||revealed||age<IceCrashIntro.RevealBirth+IceCrashIntro.RevealHold)return;
        revealed=true;
        if(reveal.clip){
            reveal.pitch=reveal.clip.length/Mathf.Max(.2f,expandSeconds+IceCrashIntro.RevealTail);
            reveal.Play();
        }
    }
    void Update(){
        bool pause=Time.timeScale<=0;
        if(pause==paused)return;
        paused=pause;
        if(pause){flight.Pause();impact.Pause();reveal.Pause();}
        else{flight.UnPause();impact.UnPause();reveal.UnPause();}
    }
    public void Stop(){if(flight)flight.Stop();if(impact)impact.Stop();if(reveal)reveal.Stop();}
    void OnDisable(){Stop();}
    void OnDestroy(){if(flight)Destroy(flight);if(impact)Destroy(impact);if(reveal)Destroy(reveal);}
}
}
