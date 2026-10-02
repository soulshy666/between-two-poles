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
        if(outlineMaterial) {
            outlineMaterial.SetFloat("_DepthEdge",depthEdge);
            outlineMaterial.SetFloat("_NormalEdge",normalEdge);
            Graphics.Blit(source,destination,outlineMaterial);
        } else Graphics.Blit(source,destination);
    }
    void Release() {
        if(world)world.targetTexture=null;
        if(Buffer) { Buffer.Release(); if(Application.isPlaying)Destroy(Buffer);else DestroyImmediate(Buffer); }
        Buffer=null;
    }
    void OnDisable(){Release();}
}
}
