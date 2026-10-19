using UnityEngine;
namespace BetweenPoles {
public sealed partial class CoverMenu {
 [Header("封面 CRT（灰烬王冠曲面屏）")]
 public bool crtEnabled=true;
 [Range(0,1)] public float crtStrength=.75f;
 [Range(0,1)] public float crtGlow=.35f;
 [Range(.5f,1.5f)] public float crtBrightness=1f;
 Material crtMaterial;
 bool UseCRT => crtEnabled&&crtStrength>0&&(!MobileUI.Enabled||MobileCRTReady);
 float CRTCurvature => .06f+.22f*crtStrength;
 // Same screen-to-content mapping as the reference renderer, used for clicks and hover.
 Vector2 CRTContentPoint(Vector2 point){
  if(!UseCRT)return point;
  if(MobileUI.Enabled)return MobileCRTContentPoint(point);
  var c=new Vector2(point.x/1376f-.5f,point.y/768f-.5f);
  c*=(1+CRTCurvature*c.sqrMagnitude)*(1-CRTCurvature*.25f);
  return new Vector2((c.x+.5f)*1376,(c.y+.5f)*768);
 }
 void DrawCRT(){
  if(MobileUI.Enabled||!UseCRT||Event.current.type!=EventType.Repaint)return;
  if(!crtMaterial){
   var shader=Resources.Load<Shader>("Shaders/CoverCRT");if(!shader||!shader.isSupported)return;
   crtMaterial=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
  }
  crtMaterial.SetFloat("_Strength",crtStrength);
  crtMaterial.SetFloat("_Glow",crtGlow);
  crtMaterial.SetFloat("_Bright",crtBrightness);
  crtMaterial.SetFloat("_Curvature",CRTCurvature);
  // Grab the composed menu once, including the astronaut and all IMGUI text.
  // No CPU readback or persistent screenshots; TV shutters are drawn later.
  Graphics.DrawTexture(new Rect(0,0,1376,768),Texture2D.whiteTexture,crtMaterial);
 }
 void ReleaseCRT(){
  ReleaseMobileCover();
  if(crtMaterial){if(Application.isPlaying)Destroy(crtMaterial);else DestroyImmediate(crtMaterial);crtMaterial=null;}
 }
}
}
