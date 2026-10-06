using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace BetweenPoles {
// A persistent overlay hides scene loading and warps the complete rendered view.
public sealed class BlackHoleScreenEffect : MonoBehaviour {
    Material material;
    BlackHolePackageRenderer package;
    bool flip;
    RenderTexture frame;
    Canvas canvas;
    RawImage image;
    CanvasGroup group;
    public float Progress {get;private set;}
    void Awake(){
        material=new Material(Resources.Load<Material>("BlackHoleTransit"));
        var go=new GameObject("黑洞全屏穿梭",typeof(Canvas),typeof(CanvasGroup),typeof(GraphicRaycaster));
        go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
        group=go.GetComponent<CanvasGroup>();
        var panel=new GameObject("空间扭曲",typeof(RectTransform),typeof(RawImage));panel.transform.SetParent(go.transform,false);
        image=panel.GetComponent<RawImage>();image.material=material;image.raycastTarget=true;
        var rect=(RectTransform)panel.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        canvas.enabled=false;
        package=gameObject.AddComponent<BlackHolePackageRenderer>();package.Initialize();
        image.material=null;
    }
    void Allocate(){
        int width=Mathf.Max(1,Screen.width),height=Mathf.Max(1,Screen.height);
        if(frame&&frame.width==width&&frame.height==height)return;
        if(frame){frame.Release();Destroy(frame);}
        frame=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){name="Black hole captured view",filterMode=FilterMode.Bilinear};frame.Create();image.texture=frame;
    }
    void Set(float p){Progress=p;material.SetFloat("_Progress",p);material.SetFloat("_Aspect",(float)Screen.width/Mathf.Max(1,Screen.height));if(package&&frame)image.texture=package.Render(frame,p,flip);}
    public IEnumerator Absorb(Vector2 center,float seconds){
        canvas.enabled=false;
        yield return new WaitForEndOfFrame();
        Allocate();ScreenCapture.CaptureScreenshotIntoRenderTexture(frame);flip=SystemInfo.graphicsUVStartsAtTop;material.SetFloat("_FlipY",flip?1:0);
        material.SetVector("_Center",new Vector4(center.x,center.y,0,0));group.alpha=1;Set(0);canvas.enabled=true;
        for(float t=0;t<seconds;t+=Time.unscaledDeltaTime){Set(Mathf.SmoothStep(0,1,t/seconds));yield return null;}
        Set(1);
    }
    public IEnumerator FadeToNavigation(){
        // Hold the warped source view across loading, then reveal the star chart.
        for(float t=0;t<.55f;t+=Time.unscaledDeltaTime){group.alpha=1-Mathf.SmoothStep(0,1,t/.55f);yield return null;}
        Hide();
    }
    public IEnumerator Emerge(BlackHolePortal portal){
        // Sample the destination's offscreen world buffer under the warped source view.
        yield return new WaitForEndOfFrame();
        PixelWorldCamera world=null;foreach(var root in portal.gameObject.scene.GetRootGameObjects())foreach(var w in root.GetComponentsInChildren<PixelWorldCamera>())world=w;
        if(!world||!world.Buffer){yield return FadeToNavigation();yield break;}
        Allocate();Graphics.Blit(world.Buffer,frame);flip=false;material.SetFloat("_FlipY",0);var center=ScreenCenter(portal);material.SetVector("_Center",new Vector4(center.x,center.y,0,0));group.alpha=1;canvas.enabled=true;
        for(float t=0;t<1.15f;t+=Time.unscaledDeltaTime){Set(1-Mathf.SmoothStep(0,1,t/1.15f));yield return null;}
        Hide();
    }
    public void Hide(){Set(0);canvas.enabled=false;group.alpha=1;}
    public static Vector2 ScreenCenter(BlackHolePortal portal){
        var binding=portal.tile.GetComponentInParent<IslandSurfaceAnchor>(true);Camera camera=null;
        foreach(var root in portal.gameObject.scene.GetRootGameObjects())foreach(var w in root.GetComponentsInChildren<PixelWorldCamera>())camera=w.GetComponent<Camera>();
        if(!camera||!binding||!binding.center)return new Vector2(.5f,.5f);
        // Match the island shader's logical-to-sphere transform before projection.
        Vector3 focus=Shader.GetGlobalVector("_IceFocus"),reference=Vector3.zero,anchor=binding.center.position;
        float radius=Mathf.Max(1,Shader.GetGlobalFloat("_IceRadius"));Vector3 relative=anchor-focus+reference;float d=new Vector2(relative.x,relative.z).magnitude;
        Vector3 axis=d>.001f?new Vector3(relative.z,0,-relative.x)/d:Vector3.forward;
        Vector3 delta=anchor-focus;float upper=Mathf.SmoothStep(0,1,Mathf.Clamp01((delta.z-Mathf.Abs(delta.x))/6));float lower=Mathf.SmoothStep(0,1,Mathf.Clamp01((-delta.z-Mathf.Abs(delta.x))/6));
        var turn=Quaternion.AngleAxis(d/radius*Mathf.Rad2Deg,axis);var local=Quaternion.AngleAxis(d/radius*(1-.1f*upper)*Mathf.Rad2Deg,axis);
        var original=Quaternion.identity;
        Vector3 point=focus+Vector3.down*radius+original*(turn*(Vector3.up*radius)+local*(portal.transform.position-anchor));
        Vector3 up=Shader.GetGlobalVector("_IceDiskUp");point+=up*(.45f*upper+.25f*lower);point=focus+(point-focus)*Mathf.Max(1,Shader.GetGlobalFloat("_IslandDisplayScale"));
        Vector3 screen=camera.WorldToViewportPoint(point);return new Vector2(Mathf.Clamp(screen.x,.1f,.9f),Mathf.Clamp(screen.y,.1f,.9f));
    }
    void OnDestroy(){if(frame){frame.Release();Destroy(frame);}if(material)Destroy(material);}
}
}
