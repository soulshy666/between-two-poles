using UnityEngine;
namespace BetweenPoles {
public static class GameAudioSettings {
    const string VolumeKey="BetweenPoles.MasterVolume", AudibleKey="BetweenPoles.AudibleVolume";
    static float audible=1;
    public static void SetVolume(float value){
        AudioListener.volume=Mathf.Clamp01(value);
        if(AudioListener.volume>0)audible=AudioListener.volume;
        PlayerPrefs.SetFloat(VolumeKey,AudioListener.volume);PlayerPrefs.SetFloat(AudibleKey,audible);
    }
    public static void ToggleMute(){SetVolume(AudioListener.volume>0?0:audible);PlayerPrefs.Save();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load(){audible=Mathf.Clamp(PlayerPrefs.GetFloat(AudibleKey,1),.01f,1);AudioListener.volume=Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,1));}
}
}
