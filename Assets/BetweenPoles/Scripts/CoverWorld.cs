using UnityEngine;
namespace BetweenPoles {
// Scene-authored models; only the small pixel render target is created at runtime.
[ExecuteAlways,DefaultExecutionOrder(300)]
public sealed partial class CoverWorld:MonoBehaviour {
    public Camera sceneCamera;
    public Transform actor,ship,sun;
    public CoverActorFloat actorFloat;
    public Vector3 shipPosition=new Vector3(6.5f,3.2f,5);
    public Vector3 shipAngles=new Vector3(22,-32,-25);
    public RenderTexture Frame {get;private set;}
    public void PoseActor(Vector2 coverPoint,float tilt){
        if(!actor||PickupInProgress)return;
        coverPoint=new Vector2(Mathf.Round(coverPoint.x/2)*2,Mathf.Round(coverPoint.y/2)*2);
        actor.localPosition=new Vector3((coverPoint.x/1376f-.5f)*(10*1376f/768f),(.5f-coverPoint.y/768f)*10,0);
        actor.localRotation=Quaternion.Euler(12,170,-68+Mathf.Round(tilt*.8f/2)*2);
    }
    void LateUpdate(){
        if(!sceneCamera)return;
        float t=Time.unscaledTime;
#if UNITY_EDITOR
        if(!Application.isPlaying)t=(float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
        if(actorFloat)actorFloat.Sample(t);
        if(ship&&!PickupInProgress&&ship.gameObject.activeSelf)ship.gameObject.SetActive(false);
        if(!Frame){Frame=new RenderTexture(688,384,24,RenderTextureFormat.ARGB32){name="封面 · 实时像素场景",filterMode=FilterMode.Point,antiAliasing=1,hideFlags=HideFlags.HideAndDontSave};Frame.Create();}
        sceneCamera.enabled=false;sceneCamera.targetTexture=Frame;sceneCamera.aspect=1376f/768f;sceneCamera.Render();
    }
    void OnDisable(){ResetPickup();if(sceneCamera)sceneCamera.targetTexture=null;if(Frame){Frame.Release();if(Application.isPlaying)Destroy(Frame);else DestroyImmediate(Frame);Frame=null;}}
}
}
