using UnityEngine;
namespace BetweenPoles {
[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class PixelPresentation : MonoBehaviour {
    public PixelWorldCamera world;
    void OnRenderImage(RenderTexture source,RenderTexture destination) {
        if(world && world.Buffer)Graphics.Blit(world.Buffer,destination);
        else Graphics.Blit(source,destination);
    }
}
}
