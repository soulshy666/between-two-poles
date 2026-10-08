using UnityEngine;

namespace BetweenPoles {
// Only render buffers are allocated at runtime. All visible geometry is scene-authored.
[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class PixelWorldCamera : MonoBehaviour {
    [Range(180,768)] public int renderHeight = 384;
    public Camera presentationCamera;
    public Material outlineMaterial;
    [Range(0,1)] public float depthEdge = .16f;
    [Range(0,1)] public float normalEdge = .13f;
    public RenderTexture Buffer { get; private set; }
    Camera world;
    IceCrashIntro opening;Material revealMaterial;
    RenderTexture heldFrame;Vector3 heldCenter;
    public void SetOpeningReveal(IceCrashIntro intro){opening=intro;}
    void OnEnable() { world=GetComponent<Camera>(); world.depthTextureMode=DepthTextureMode.DepthNormals; }
    void Update() {
        if (!world) world=GetComponent<Camera>();
        float aspect=presentationCamera ? presentationCamera.aspect : 16f/9f;
        int width=Mathf.Max(1,Mathf.RoundToInt(renderHeight*aspect));
        if (!Buffer || Buffer.width!=width || Buffer.height!=renderHeight) {
            Release();
            Buffer=new RenderTexture(width,renderHeight,24,RenderTextureFormat.ARGB32) {
                name="Pixel scene 384p",filterMode=FilterMode.Point,antiAliasing=1,hideFlags=HideFlags.HideAndDontSave
            };
            Buffer.Create(); world.targetTexture=Buffer;
        }
        world.aspect=aspect;
    }
    void OnRenderImage(RenderTexture source,RenderTexture destination) {
        bool reveal=Application.isPlaying&&opening&&opening.ColorRevealActive;
        if(!reveal)ReleaseHeldFrame();
        if(reveal&&!revealMaterial){
            var shader=Resources.Load<Shader>("Shaders/CrashColorReveal");
            if(shader)revealMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        RenderTexture outlined=null;
        try{
            var frame=source;
            if(outlineMaterial){
                outlineMaterial.SetFloat("_DepthEdge",depthEdge);outlineMaterial.SetFloat("_NormalEdge",normalEdge);
                if(reveal&&revealMaterial){outlined=RenderTexture.GetTemporary(source.width,source.height,0,source.format);outlined.filterMode=FilterMode.Point;Graphics.Blit(source,outlined,outlineMaterial);frame=outlined;}
                else{Graphics.Blit(source,destination,outlineMaterial);return;}
            }
            if(reveal&&revealMaterial){
                var center=world.WorldToViewportPoint(CrashColorReveal.DisplayPoint(opening.CrashPoint));
                if(opening.FreezeOpeningFrame){
                    if(heldFrame&&(heldFrame.width!=frame.width||heldFrame.height!=frame.height))ReleaseHeldFrame();
                    if(!heldFrame){
                        heldFrame=new RenderTexture(frame.width,frame.height,0,frame.format){name="Crash impact freeze",filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};
                        heldFrame.Create();Graphics.Blit(frame,heldFrame);heldCenter=center;
                    }
                    frame=heldFrame;center=heldCenter;
                }else ReleaseHeldFrame();
                revealMaterial.SetFloat("_Age",opening.ColorRevealAge);
                revealMaterial.SetFloat("_Birth",IceCrashIntro.RevealBirth);
                revealMaterial.SetFloat("_Hold",IceCrashIntro.RevealHold);
                revealMaterial.SetFloat("_Expand",Mathf.Max(.2f,opening.colorRevealSeconds));
                revealMaterial.SetVector("_Center",new Vector4(center.x,center.y,0,0));
                revealMaterial.SetFloat("_Progress",opening.ColorRevealProgress);
                Graphics.Blit(frame,destination,revealMaterial);
            }else Graphics.Blit(frame,destination);
        }finally{if(outlined)RenderTexture.ReleaseTemporary(outlined);}
    }
    void ReleaseHeldFrame(){
        if(!heldFrame)return;heldFrame.Release();
        if(Application.isPlaying)Destroy(heldFrame);else DestroyImmediate(heldFrame);heldFrame=null;
    }
    void Release() {
        ReleaseHeldFrame();
        if(world)world.targetTexture=null;
        if(Buffer) { Buffer.Release(); if(Application.isPlaying)Destroy(Buffer);else DestroyImmediate(Buffer); }
        Buffer=null;
    }
    void OnDisable(){Release();if(revealMaterial){if(Application.isPlaying)Destroy(revealMaterial);else DestroyImmediate(revealMaterial);revealMaterial=null;}}
}
}
