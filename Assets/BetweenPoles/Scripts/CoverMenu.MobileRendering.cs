using UnityEngine;
namespace BetweenPoles {
public sealed partial class CoverMenu {
 // Compose the actual menu into owned textures. No screen copy/GrabPass, CPU
 // readback, extra camera or previous-frame feedback is used on mobile.
 RenderTexture mobileCover,mobileFiltered,mobilePreviousTarget;
 Material mobileCompose,mobileCRT;
 static CoverMenu mobileDrawing;
 bool mobileRenderFailed;
 bool MobileCRTReady => !mobileRenderFailed&&mobileCover&&mobileCover.IsCreated()
     &&mobileFiltered&&mobileFiltered.IsCreated()&&mobileCompose&&mobileCRT;

 bool PrepareMobileCover(){
  if(mobileRenderFailed)return false;
  try{
   if(!mobileCompose){
    var shader=Resources.Load<Shader>("Shaders/CoverCompose");
    if(!shader||!shader.isSupported)return false;
    mobileCompose=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
   }
   if(!mobileCRT){
    var shader=Resources.Load<Shader>("Shaders/CoverCRTMobile");
    if(!shader||!shader.isSupported)return false;
    mobileCRT=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
   }
   float scale=Mathf.Min(1,1600f/Mathf.Max(Screen.width,Screen.height));
   int width=Mathf.Max(1,Mathf.RoundToInt(Screen.width*scale)),height=Mathf.Max(1,Mathf.RoundToInt(Screen.height*scale));
   if(!mobileCover||mobileCover.width!=width||mobileCover.height!=height){
    ReleaseMobileTexture(ref mobileCover);ReleaseMobileTexture(ref mobileFiltered);
    mobileCover=MobileTexture(width,height,"Mobile cover composition");
    mobileFiltered=MobileTexture(width,height,"Mobile cover CRT");
   }
   if(!mobileCover.IsCreated())mobileCover.Create();
   if(!mobileFiltered.IsCreated())mobileFiltered.Create();
   return MobileCRTReady;
  }catch(System.Exception error){
   mobileRenderFailed=true;Debug.LogWarning("Mobile cover uses direct rendering: "+error.Message);return false;
  }
 }
 static RenderTexture MobileTexture(int width,int height,string label){
  return new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){name=label,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
 }
 bool BeginMobileCover(){
  if(!MobileUI.Enabled||!PrepareMobileCover()||Event.current.type!=EventType.Repaint)return false;
  mobilePreviousTarget=RenderTexture.active;
  RenderTexture.active=mobileCover;GL.Clear(false,true,Color.black);
  mobileDrawing=this;return true;
 }
 void EndMobileCover(){
  mobileDrawing=null;
  var matrix=GUI.matrix;var color=GUI.color;
  try{
   if(UseCRT){
    mobileCRT.SetFloat("_Strength",crtStrength);mobileCRT.SetFloat("_Glow",crtGlow);
    mobileCRT.SetFloat("_Bright",crtBrightness);mobileCRT.SetFloat("_Curvature",CRTCurvature);
    Graphics.Blit(mobileCover,mobileFiltered,mobileCRT);
   }
   RenderTexture.active=mobilePreviousTarget;
   GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;
   GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),UseCRT?mobileFiltered:mobileCover);
  }finally{RenderTexture.active=mobilePreviousTarget;GUI.matrix=matrix;GUI.color=color;}
 }
 static void CoverTexture(Rect rect,Texture texture){CoverTexture(rect,texture,new Rect(0,0,1,1));}
 static void CoverTexture(Rect rect,Texture texture,Rect uv){
  if(!mobileDrawing){GUI.DrawTextureWithTexCoords(rect,texture,uv);return;}
  if(!texture||Event.current.type!=EventType.Repaint)return;
  var owner=mobileDrawing;var previous=RenderTexture.active;bool srgb=GL.sRGBWrite;
  GL.PushMatrix();
  try{
   RenderTexture.active=owner.mobileCover;
   GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;
   GL.LoadPixelMatrix(0,Screen.width,Screen.height,0);
   owner.mobileCompose.mainTexture=texture;owner.mobileCompose.SetPass(0);
   var tint=GUI.color;if(QualitySettings.activeColorSpace==ColorSpace.Linear)tint=tint.linear;
   GL.Begin(GL.QUADS);GL.Color(tint);
   CoverVertex(rect.x,rect.y,uv.x,uv.yMax);
   CoverVertex(rect.xMax,rect.y,uv.xMax,uv.yMax);
   CoverVertex(rect.xMax,rect.yMax,uv.xMax,uv.y);
   CoverVertex(rect.x,rect.yMax,uv.x,uv.y);
   GL.End();
  }finally{GL.PopMatrix();GL.sRGBWrite=srgb;RenderTexture.active=previous;}
 }
 static void CoverVertex(float x,float y,float u,float v){
  GL.TexCoord2(u,v);GL.Vertex(GUI.matrix.MultiplyPoint3x4(new Vector3(x,y,0)));
 }
 // Only the artwork crops to fill. Menu and modal controls retain the safe-area
 // design layout, so a wider phone never clips the last save slot or exit button.
 bool DrawMobileBackground(){
  Texture texture=coverWorld?coverWorld.Frame:(pixelArtwork||classicArtwork)?background:backgroundFrame?backgroundFrame:(Texture)background;
  if(!texture)return false;
  var layout=CoverRect();float scale=layout.width/1376f;
  var rect=new Rect(-layout.x/scale,-layout.y/scale,Screen.width/scale,Screen.height/scale);
  var uv=MobileBackgroundUV(new Vector2(texture.width,texture.height),new Vector2(Screen.width,Screen.height));
  CoverTexture(rect,texture,uv);return true;
 }
 static Rect MobileBackgroundUV(Vector2 source,Vector2 screen){
  float sourceAspect=source.x/source.y,screenAspect=screen.x/screen.y;
  var uv=new Rect(0,0,1,1);
  if(screenAspect>sourceAspect){uv.height=sourceAspect/screenAspect;uv.y=(1-uv.height)*.5f;}
  else{uv.width=screenAspect/sourceAspect;uv.x=(1-uv.width)*.5f;}
  return uv;
 }
 Vector2 MobileCRTContentPoint(Vector2 local){
  var layout=CoverRect();float scale=layout.width/1376f;
  var screen=layout.position+local*scale;
  var warped=WarpMobileUV(new Vector2(screen.x/Screen.width,screen.y/Screen.height),CRTCurvature);
  return (new Vector2(warped.x*Screen.width,warped.y*Screen.height)-layout.position)/scale;
 }
 static Vector2 WarpMobileUV(Vector2 uv,float curvature){
  var c=uv-Vector2.one*.5f;return c*(1+curvature*c.sqrMagnitude)*(1-curvature*.25f)+Vector2.one*.5f;
 }
 void ReleaseMobileCover(){
  if(mobileDrawing==this)mobileDrawing=null;
  ReleaseMobileTexture(ref mobileCover);ReleaseMobileTexture(ref mobileFiltered);
  if(mobileCompose){if(Application.isPlaying)Destroy(mobileCompose);else DestroyImmediate(mobileCompose);mobileCompose=null;}
  if(mobileCRT){if(Application.isPlaying)Destroy(mobileCRT);else DestroyImmediate(mobileCRT);mobileCRT=null;}
 }
 static void ReleaseMobileTexture(ref RenderTexture texture){
  if(!texture)return;texture.Release();if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture);texture=null;
 }
}
}
