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
    static bool settlingAnimation;
    static int skipInputFrame=-1;
    public static bool SkipTransitionActive {get{return settlingAnimation||Time.frameCount==skipInputFrame;}}
    static readonly System.Collections.Generic.HashSet<string> watchedAnimations=new System.Collections.Generic.HashSet<string>();
    public static float AnimationSpeed {get{return settlingAnimation?100000f:1f;}}
    public static float AnimationDeltaTime {get{return settlingAnimation?10000f:Time.unscaledDeltaTime;}}
    static bool SkipPressed(){return Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.RightArrow)
        ||Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.DownArrow)
        ||Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.S);}
    public static System.Collections.IEnumerator PlayAnimation(string kind,System.Collections.IEnumerator routine){
        if(Application.isPlaying&&!instance){instance=new GameObject("Portal travel").AddComponent<BlackHoleTravel>();DontDestroyOnLoad(instance.gameObject);}
        bool canSkip=watchedAnimations.Contains(kind),skipped=false;
        int firstFrame=Time.frameCount,guard=100000;
        var stack=new System.Collections.Generic.Stack<System.Collections.IEnumerator>();stack.Push(routine);
        try{
            while(stack.Count>0){
                if(!skipped&&canSkip&&Application.isPlaying&&Time.frameCount>firstFrame&&SkipPressed()){
                    skipped=true;settlingAnimation=true;skipInputFrame=Time.frameCount;
                }
                if(skipped&&--guard<=0)throw new System.InvalidOperationException("Portal skip did not settle");
                var current=stack.Peek();
                if(!current.MoveNext()){(stack.Pop() as System.IDisposable)?.Dispose();continue;}
                if(current.Current is System.Collections.IEnumerator child&&!(child is CustomYieldInstruction)){stack.Push(child);continue;}
                if(!skipped)yield return current.Current;
            }
            if(Application.isPlaying)watchedAnimations.Add(kind);
            settlingAnimation=false;
        }finally{
            while(stack.Count>0)(stack.Pop() as System.IDisposable)?.Dispose();
            settlingAnimation=false;
        }
    }
    static string ExitAnimationKind(ExitPlan plan,MagnetPiece cargo){
        string outcome=plan.failedExit?(cargo?"blocked-cargo":"ledge-return"):plan.kickRebound?"pressed-rebound":plan.bounce?"cargo-impact":
            plan.unstableLanding?(plan.kick?"floating-kick":"spring-return"):plan.receiver?"assembly-"+plan.product:plan.kick?"kick":"landing";
        return "exit/"+outcome+"/"+(cargo?cargo.shape+"/"+cargo.product+"/"+MagnetPiece.VerticalBar(cargo.Pose):"player");
    }
    static System.Collections.IEnumerator AnimationWait(float seconds){
        for(float age=0;age<seconds;age+=AnimationDeltaTime)yield return null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){instance=null;Selecting=false;settlingAnimation=false;skipInputFrame=-1;watchedAnimations.Clear();}
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
    static bool PlanAvailable(BlackHolePortal portal){ExitPlan plan;return PlanJourneyExit(portal,instance.entryDirection,instance.origin.Cargo,out plan);}
    public static void Choose(string scene,string room){
        if(!instance||instance.busy||!Selecting||!instance.origin||instance.origin.gameObject.scene.path!=scene)return;
        var portal=instance.testPortals.FirstOrDefault(p=>p&&p.Room==room&&PlanAvailable(p));
        if(portal)instance.SelectTestPortal(portal);
    }
}
}
