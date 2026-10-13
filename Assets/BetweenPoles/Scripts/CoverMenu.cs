using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace BetweenPoles {
[ExecuteAlways]
public sealed class CoverMenu:MonoBehaviour {
 public Texture2D background,solarMask,astronaut,title,startLabel,settingsLabel,exitLabel;
 public Texture2D startHover,settingsHover,exitHover,triangle,menuLine;
 readonly float[] hover=new float[3],hoverStart=new float[3];
 int previousSelection=-1;float hoverAge;
 Vector3 lastMouse;
 public Font font;
 public Shader backgroundShader;
 Material backdrop;RenderTexture backgroundFrame;
 int selected;bool settings,exitPrompt,loading,scanlines=true;
 float fade;Vector2 parallax;
 float AnimationTime {
  get {
#if UNITY_EDITOR
   if(!Application.isPlaying)return (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
   return Time.unscaledTime;
  }
 }
#if UNITY_EDITOR
 double nextPreviewFrame;
 void TickEditorPreview(){
  if(Application.isPlaying||!this||!isActiveAndEnabled)return;
  double now=UnityEditor.EditorApplication.timeSinceStartup;
  if(now<nextPreviewFrame)return;
  nextPreviewFrame=now+1.0/30.0;
  UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
  UnityEditor.SceneView.RepaintAll();
 }
#endif
 void OnEnable(){
  EnsurePreview();if(!Application.isPlaying)hover[0]=1;
#if UNITY_EDITOR
  UnityEditor.EditorApplication.update-=TickEditorPreview;
  UnityEditor.EditorApplication.update+=TickEditorPreview;
#endif
 }
 void EnsurePreview(){
  if(!background||!backgroundShader)return;
  if(!backdrop){backdrop=new Material(backgroundShader){hideFlags=HideFlags.HideAndDontSave};backdrop.SetTexture("_MapTex",solarMask);}
  if(!backgroundFrame)backgroundFrame=new RenderTexture(1376,768,0){hideFlags=HideFlags.HideAndDontSave};
  RenderBackground();
 }
 void ReleasePreview(){
  if(backgroundFrame){backgroundFrame.Release();if(Application.isPlaying)Destroy(backgroundFrame);else DestroyImmediate(backgroundFrame);backgroundFrame=null;}
  if(backdrop){if(Application.isPlaying)Destroy(backdrop);else DestroyImmediate(backdrop);backdrop=null;}
 }
 void OnDisable(){
#if UNITY_EDITOR
  UnityEditor.EditorApplication.update-=TickEditorPreview;
#endif
  ReleasePreview();
 }
 void OnDestroy(){ReleasePreview();}
 void RenderBackground(){
  if(!backdrop||!backgroundFrame)return;
  var previous=RenderTexture.active;
  backdrop.SetFloat("_CoverTime",AnimationTime);
  backdrop.SetVector("_Parallax",Application.isPlaying?new Vector4(-parallax.x*.004f,-parallax.y*.004f,0,0):Vector4.zero);
  Graphics.Blit(background,backgroundFrame,backdrop);RenderTexture.active=previous;
 }
 void LateUpdate(){EnsurePreview();}
 void Update(){
  if(!Application.isPlaying)return;
  if(previousSelection!=selected){previousSelection=selected;hoverAge=0;System.Array.Copy(hover,hoverStart,3);}
  hoverAge+=Time.unscaledDeltaTime;
  float ease=MenuEase(Mathf.Clamp01(hoverAge/.25f));
  for(int i=0;i<3;i++)hover[i]=Mathf.Lerp(hoverStart[i],i==selected?1:0,ease);
  if(loading)return;
  var mouse=Input.mousePosition;
  if(!settings&&!exitPrompt&&(mouse-lastMouse).sqrMagnitude>.01f){
   float k=Mathf.Max(Screen.width/1376f,Screen.height/768f);
   float ox=Screen.width/1376f<Screen.height/768f?0:(Screen.width-1376*k)*.5f;
   var point=new Vector2((mouse.x-ox)/k,(Screen.height-mouse.y-(Screen.height-768*k)*.5f)/k);
   for(int i=0;i<3;i++)if(new Rect(30,330+i*106,350,92).Contains(point))selected=i;
  }
  lastMouse=mouse;
  parallax=Vector2.Lerp(parallax,new Vector2(Mathf.Clamp01(Input.mousePosition.x/Screen.width)-.5f,.5f-Mathf.Clamp01(Input.mousePosition.y/Screen.height))*2,1-Mathf.Exp(-Time.unscaledDeltaTime*2.4f));
  if(settings||exitPrompt){if(Input.GetKeyDown(KeyCode.Escape)){settings=false;exitPrompt=false;}return;}
  if(Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.S))selected=(selected+1)%3;
  if(Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.W))selected=(selected+2)%3;
  if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))Activate(selected);
 }
 // Match the source page's cubic-bezier(.2,.8,.2,1).
 static float MenuEase(float x){
  float lo=0,hi=1,t=0;
  for(int i=0;i<12;i++){t=(lo+hi)*.5f;float u=1-t;float bx=3*u*u*t*.2f+3*u*t*t*.2f+t*t*t;if(bx<x)lo=t;else hi=t;}
  float v=1-t;return 3*v*v*t*.8f+3*v*t*t+t*t*t;
 }
 void Activate(int item){if(!Application.isPlaying||loading)return;if(item==0)StartCoroutine(Begin());else if(item==1)settings=true;else exitPrompt=true;}
 IEnumerator Begin(){
  loading=true;
  for(float t=0;t<.55f;t+=Time.unscaledDeltaTime){fade=t/.55f;yield return null;}
  fade=1;Time.timeScale=1;
  yield return SceneManager.LoadSceneAsync("Chapter01_IceWorld");
 }
 Font exitFont;
 void ExitText(Rect rect,string value,int size){
  if(!exitFont)exitFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},32);
  var style=new GUIStyle(GUI.skin.label){font=exitFont,fontSize=size,alignment=TextAnchor.MiddleCenter};
  style.normal.textColor=new Color(0,0,0,.8f);
  var shadow=rect;shadow.position+=new Vector2(2,2);GUI.Label(shadow,value,style);
  style.normal.textColor=new Color(.96f,.94f,.90f);GUI.Label(rect,value,style);
 }
 bool ExitChoice(Rect rect,string value){
  bool hot=rect.Contains(Event.current.mousePosition);
  ExitText(rect,(hot?"›  ":"")+value,26);
  return GUI.Button(rect,GUIContent.none,GUIStyle.none);
 }
 void OnGUI(){
  if(!background||!astronaut||!title)return;
  EnsurePreview();
  var matrix=GUI.matrix;var color=GUI.color;
  float scale=Mathf.Max(Screen.width/1376f,Screen.height/768f);
  GUI.matrix=Matrix4x4.TRS(new Vector3(Screen.width/1376f<Screen.height/768f?0:(Screen.width-1376*scale)*.5f,(Screen.height-768*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
  float t=AnimationTime;
  GUI.DrawTexture(new Rect(0,0,1376,768),backgroundFrame);
  var before=GUI.matrix;
  float x=620+Mathf.Sin(t*.31f)*26+Mathf.Sin(t*.73f+1.3f)*11+parallax.x*16;
  float y=345+Mathf.Sin(t*.47f)*22+Mathf.Sin(t*1.07f+.4f)*8+parallax.y*12;
  GUIUtility.RotateAroundPivot(Mathf.Sin(t*.23f)*9+Mathf.Sin(t*.61f+2)*3.5f,new Vector2(x+142.5f,y+97));
  GUI.DrawTexture(new Rect(x,y,285,285f*astronaut.height/astronaut.width),astronaut);GUI.matrix=before;
  GUI.DrawTexture(new Rect(12,60,title.width*.5f,title.height*.5f),title);
  var labels=new[]{startLabel,settingsLabel,exitLabel};
  var highlights=new[]{startHover,settingsHover,exitHover};
  for(int i=0;i<3;i++){
   float h=hover[i];
   var rect=new Rect(44+12*h,330+i*106,320,92);
   if(!settings&&!exitPrompt&&!loading&&GUI.Button(rect,GUIContent.none,GUIStyle.none)){selected=i;Activate(i);}
   GUI.color=new Color(1,1,1,1-h);GUI.DrawTexture(new Rect(rect.x,rect.y+6,labels[i].width*.5f,labels[i].height*.5f),labels[i]);
   GUI.color=new Color(1,1,1,h);GUI.DrawTexture(new Rect(rect.x,rect.y+6,highlights[i].width*.5f,highlights[i].height*.5f),highlights[i]);
   GUI.color=new Color(1,1,1,Mathf.Lerp(.75f,1,h));GUI.DrawTexture(new Rect(rect.x+14,rect.y+80.5f,230+70*h,1.5f),menuLine);
   GUI.color=new Color(1,1,1,h);GUI.DrawTexture(new Rect(rect.x-14-8*(1-h),rect.y+32,10,14),triangle);GUI.color=Color.white;
  }
  if(scanlines){GUI.color=new Color(0,0,0,.085f);for(int yLine=0;yLine<768;yLine+=3)GUI.DrawTexture(new Rect(0,yLine,1376,1),Texture2D.whiteTexture);GUI.color=Color.white;}
  var text=new GUIStyle(GUI.skin.label){font=font,fontSize=26,alignment=TextAnchor.MiddleLeft};text.normal.textColor=new Color(.96f,.94f,.90f);
  var button=new GUIStyle(GUI.skin.button){font=font,fontSize=25};
  if(settings||exitPrompt){
   if(settings){
    GUI.color=new Color(0,0,0,.75f);GUI.DrawTexture(new Rect(0,0,1376,768),Texture2D.whiteTexture);GUI.color=Color.white;
    GUI.Box(new Rect(428,225,520,320),GUIContent.none);
    GUI.Label(new Rect(460,245,450,48),"设置",text);
   }
   if(settings){GUI.Label(new Rect(460,315,140,40),"总音量",text);AudioListener.volume=GUI.HorizontalSlider(new Rect(625,330,260,25),AudioListener.volume,0,1);GUI.Label(new Rect(460,375,170,40),"扫描线",text);if(GUI.Button(new Rect(700,375,185,42),scanlines?"开":"关",button))scanlines=!scanlines;if(GUI.Button(new Rect(720,470,165,45),"返回",button))settings=false;}
   else{
    ExitText(new Rect(428,310,520,60),"确定要退出游戏吗？",32);
    if(ExitChoice(new Rect(490,410,160,48),"取消"))exitPrompt=false;
    if(ExitChoice(new Rect(720,410,165,48),"确定")){
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying=false;
#else
    Application.Quit();
#endif
   }}
  }
  GUI.matrix=matrix;if(fade>0){GUI.color=new Color(0,0,0,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);}GUI.color=color;
 }
#if UNITY_EDITOR
 void OnDrawGizmos(){
  if(Application.isPlaying||!background||!astronaut||!title)return;
  var view=UnityEditor.SceneView.currentDrawingSceneView;if(!view)return;
  EnsurePreview();
  UnityEditor.Handles.BeginGUI();
  var old=GUI.matrix;var color=GUI.color;
  float width=view.position.width,height=view.position.height-24;
  float scale=Mathf.Min(width/1376f,height/768f);
  GUI.matrix=Matrix4x4.TRS(new Vector3((width-1376*scale)*.5f,(height-768*scale)*.5f,0),Quaternion.identity,Vector3.one*scale);
  GUI.color=Color.white;
  GUI.DrawTexture(new Rect(0,0,1376,768),backgroundFrame?backgroundFrame:(Texture)background);
  float t=AnimationTime;
  float astroX=620+Mathf.Sin(t*.31f)*26+Mathf.Sin(t*.73f+1.3f)*11;
  float astroY=345+Mathf.Sin(t*.47f)*22+Mathf.Sin(t*1.07f+.4f)*8;
  var stageMatrix=GUI.matrix;
  GUIUtility.RotateAroundPivot(Mathf.Sin(t*.23f)*9+Mathf.Sin(t*.61f+2)*3.5f,new Vector2(astroX+142.5f,astroY+97));
  GUI.DrawTexture(new Rect(astroX,astroY,285,285f*astronaut.height/astronaut.width),astronaut);
  GUI.matrix=stageMatrix;
  GUI.DrawTexture(new Rect(12,60,title.width*.5f,title.height*.5f),title);
  var labels=new[]{startHover?startHover:startLabel,settingsLabel,exitLabel};
  for(int i=0;i<3;i++)if(labels[i]){
   float x=44+(i==0?12:0),y=330+i*106;
   GUI.DrawTexture(new Rect(x,y+6,labels[i].width*.5f,labels[i].height*.5f),labels[i]);
   if(menuLine)GUI.DrawTexture(new Rect(x+14,y+80.5f,i==0?300:230,1.5f),menuLine);
   if(i==0&&triangle)GUI.DrawTexture(new Rect(x-14,y+32,10,14),triangle);
  }
  GUI.matrix=old;GUI.color=color;UnityEditor.Handles.EndGUI();
 }
#endif

}
}
