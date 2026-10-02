using UnityEngine;
namespace BetweenPoles.Generators {
 [ExecuteAlways] public sealed class GeneratedPlanetBillboard:MonoBehaviour {
  [InspectorName("面向相机")] public bool faceCamera=true;
  [InspectorName("目标相机（留空使用主相机）")] public Camera targetCamera;
  void OnEnable(){Camera.onPreCull+=BeforeCamera;}
  void OnDisable(){Camera.onPreCull-=BeforeCamera;}
  void LateUpdate(){Face(targetCamera?targetCamera:Camera.main);}
  void BeforeCamera(Camera camera){if(camera==(targetCamera?targetCamera:Camera.main))Face(camera);}
  void Face(Camera camera){if(faceCamera&&camera)transform.rotation=camera.transform.rotation;}
 }
}
