using UnityEngine;
namespace BetweenPoles.Generators {
 // The exported asset follows a camera but never changes its projection or movement.
 [ExecuteAlways,DefaultExecutionOrder(1000)]
 public sealed class GeneratedBackgroundFollower:MonoBehaviour {
  [InspectorName("目标游戏相机（留空自动查找）")] public Camera targetCamera;
  [InspectorName("背景配置")] public PixelGeneratorLab environment;
  void OnEnable(){Camera.onPreCull+=BeforeCamera;
#if UNITY_EDITOR
   UnityEditor.EditorApplication.update+=EditorTick;
#endif
  }
  void OnDisable(){Camera.onPreCull-=BeforeCamera;
#if UNITY_EDITOR
   UnityEditor.EditorApplication.update-=EditorTick;
#endif
  }
#if UNITY_EDITOR
  void EditorTick(){if(this&&!Application.isPlaying)Fit();}
#endif
  void LateUpdate(){Fit();}
  Camera Resolve(){if(targetCamera)return targetCamera;var main=Camera.main;if(main&&main.gameObject.scene==gameObject.scene)return main;foreach(var c in Camera.allCameras)if(c.gameObject.scene==gameObject.scene&&c.cullingMask!=0&&c.cameraType==CameraType.Game)return c;return null;}
  void BeforeCamera(Camera c){if(c==Resolve())Fit();}
  public void Fit(){var cam=Resolve();if(!cam||!environment||!environment.spaceMode)return;
   float distance=Mathf.Min(200,cam.farClipPlane*.8f);distance=Mathf.Max(cam.nearClipPlane+1,distance);
   float viewHeight=cam.orthographic?cam.orthographicSize*2:2*Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad*.5f)*distance;
   float aspect=(float)Mathf.Max(1,environment.width)/Mathf.Max(1,environment.height);
   float scale=viewHeight/5*Mathf.Max(1,cam.aspect/aspect)*1.005f;
   transform.SetPositionAndRotation(cam.transform.position+cam.transform.forward*distance,cam.transform.rotation);
   transform.localScale=Vector3.one*scale;
  }
 }
}
