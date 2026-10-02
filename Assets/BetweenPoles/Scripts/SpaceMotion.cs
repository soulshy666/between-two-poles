using UnityEngine;
namespace BetweenPoles {
[ExecuteAlways,RequireComponent(typeof(Renderer))]
public sealed class SpaceMotion:MonoBehaviour {
    MaterialPropertyBlock block;
    double last;
#if UNITY_EDITOR
    void OnEnable(){UnityEditor.EditorApplication.update-=EditorTick;UnityEditor.EditorApplication.update+=EditorTick;}
    void OnDisable(){UnityEditor.EditorApplication.update-=EditorTick;}
    void EditorTick(){
        if(Application.isPlaying||!this||!isActiveAndEnabled)return;
        if(UnityEditor.EditorApplication.timeSinceStartup-last<.033)return;
        Update();
        UnityEditor.SceneView.RepaintAll();
    }
#endif
    void Update(){
        double clock=Time.time;
#if UNITY_EDITOR
        if(!Application.isPlaying)clock=UnityEditor.EditorApplication.timeSinceStartup;
#endif
        if(block==null)block=new MaterialPropertyBlock();
        block.SetFloat("_Clock",(float)clock);GetComponent<Renderer>().SetPropertyBlock(block);
#if UNITY_EDITOR
        if(!Application.isPlaying&&clock-last>.033){last=clock;UnityEditor.EditorApplication.QueuePlayerLoopUpdate();}
#endif
    }
}
}
