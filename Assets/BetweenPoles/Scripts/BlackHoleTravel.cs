using System.Linq;
using UnityEngine;
namespace BetweenPoles {
// Black holes navigate the real explored islands in the current gameplay scene.
public sealed partial class BlackHoleTravel : MonoBehaviour {
    public const string Showcase="Assets/BetweenPoles/Scenes/PlanetChapterShowcase.unity";
    static BlackHoleTravel instance;
    public static bool Selecting {get;private set;}
    public static bool InTransit {get{return instance&&instance.busy;}}
    BlackHolePortal origin;
    Vector2Int entryDirection;
    Quaternion entryFacing;
    bool busy;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){instance=null;Selecting=false;}
    public static void Begin(BlackHolePortal portal){
        if(Selecting||InTransit||!portal||!portal.CanArrive)return;
        if(!instance){instance=new GameObject("黑洞穿梭").AddComponent<BlackHoleTravel>();DontDestroyOnLoad(instance.gameObject);}
        instance.StartCoroutine(instance.TestTravel(portal));
    }
    // Retained for existing atlas callers; these never load a different scene.
    public static bool IsAvailable(string scene,string room){
        if(!instance||!instance.origin||instance.origin.gameObject.scene.path!=scene||!instance.VisitedRoom(room))return false;
        return instance.testPortals!=null&&instance.testPortals.Any(p=>p&&p.Room==room&&PlanAvailable(p));
    }
    static bool PlanAvailable(BlackHolePortal portal){ExitPlan plan;return PlanExit(portal,instance.entryDirection,instance.origin.Cargo,out plan);}
    public static void Choose(string scene,string room){
        if(!instance||instance.busy||!Selecting||!instance.origin||instance.origin.gameObject.scene.path!=scene)return;
        var portal=instance.testPortals.FirstOrDefault(p=>p&&p.Room==room&&PlanAvailable(p));
        if(portal)instance.SelectTestPortal(portal);
    }
}
}
