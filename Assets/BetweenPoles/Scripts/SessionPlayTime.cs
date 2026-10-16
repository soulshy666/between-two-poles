using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
// Shared across chapters and map inspection; restored by the selected save slot.
public sealed class SessionPlayTime:MonoBehaviour {
    public static double Seconds {get;private set;}
    static SessionPlayTime instance;
    public static void Restore(double seconds){Seconds=System.Math.Max(0,seconds);}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){Seconds=0;instance=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize(){if(!instance){instance=new GameObject("本次游玩计时").AddComponent<SessionPlayTime>();DontDestroyOnLoad(instance.gameObject);}}
    void OnEnable(){SceneManager.sceneLoaded+=Loaded;}
    void OnDisable(){SceneManager.sceneLoaded-=Loaded;}
    void Loaded(Scene scene,LoadSceneMode mode){if(scene.name=="Cover"&&mode==LoadSceneMode.Single)Seconds=0;}
    void Update(){
        if(GameSaveSlots.Restoring||ElementGameUI.Paused||Time.timeScale<=0)return;
        var name=SceneManager.GetActiveScene().name;
        if(!name.StartsWith("Chapter0")&&!ChapterMapReturn.HasSuspendedGame&&!ChapterRecoilTravel.Active)return;
        Seconds+=Time.unscaledDeltaTime;
    }
    public static string Display {
        get{long seconds=(long)Seconds;return (seconds/3600).ToString("00")+":"+(seconds/60%60).ToString("00")+":"+(seconds%60).ToString("00");}
    }
}
}
