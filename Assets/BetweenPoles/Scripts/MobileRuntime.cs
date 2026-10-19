using UnityEngine;
namespace BetweenPoles {
public sealed class MobileRuntime:MonoBehaviour {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void StartMobile(){
        if(!Application.isMobilePlatform)return;
        var root=new GameObject("Mobile lifecycle");DontDestroyOnLoad(root);root.AddComponent<MobileRuntime>();
    }
    void OnApplicationPause(bool paused){if(paused){MobileUI.ResetTouches();PlayerPrefs.Save();}}
    void OnApplicationFocus(bool focused){if(!focused)MobileUI.ResetTouches();}
}
}
