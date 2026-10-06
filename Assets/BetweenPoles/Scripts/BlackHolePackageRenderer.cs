using UnityEngine;
namespace BetweenPoles {
// Full-screen black-hole suction renderer. The supplied package is intentionally not composited here.
public sealed class BlackHolePackageRenderer : MonoBehaviour {
    RenderTexture output;
    Material material;
    public void Initialize(){ material=new Material(Resources.Load<Material>("BlackHoleTransit")); }
    public RenderTexture Render(RenderTexture source,float progress,bool flip,Vector2 center){
        if(!output||output.width!=source.width||output.height!=source.height){
            if(output){output.Release();Destroy(output);}
            output=new RenderTexture(source.width,source.height,0,RenderTextureFormat.ARGB32){filterMode=FilterMode.Bilinear};output.Create();
        }
        material.SetFloat("_Progress",progress);material.SetFloat("_FlipY",flip?1:0);material.SetFloat("_Aspect",(float)source.width/source.height);material.SetVector("_Center",new Vector4(center.x,center.y,0,0));
        Graphics.Blit(source,output,material);return output;
    }
    void OnDestroy(){if(output){output.Release();Destroy(output);}if(material)Destroy(material);}
}
}
