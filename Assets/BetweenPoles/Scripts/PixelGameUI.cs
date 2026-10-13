using UnityEngine;
using UnityEngine.UI;
namespace BetweenPoles {
// Shared chapter HUD styling. Integer-sized details retain hard pixel edges.
public static class PixelGameUI {
 public static readonly Color Ink=new Color32(226,243,239,255);
 static readonly Color Border=new Color32(91,133,148,255),Face=new Color32(17,35,49,245),Blue=new Color32(80,194,219,255),Red=new Color32(255,79,105,255);
 static GUIStyle label;static Texture2D frameTexture;static Sprite frameSprite;
 static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
 public static GUIStyle Text(int size=16,TextAnchor align=TextAnchor.MiddleLeft){
  if(label==null)label=new GUIStyle(GUI.skin.label){font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},16),wordWrap=true,padding=new RectOffset(0,0,0,0)};
  var s=new GUIStyle(label){fontSize=size,alignment=align};s.normal.textColor=Ink;return s;
 }
 public static void Panel(Rect r,string text=""){
  Fill(new Rect(r.x+4,r.y+4,r.width,r.height),new Color(0,0,0,.65f));
  Fill(new Rect(r.x+3,r.y,r.width-6,r.height),Border);Fill(new Rect(r.x,r.y+3,r.width,r.height-6),Border);
  Fill(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),Face);
  Fill(new Rect(r.x+6,r.y+6,r.width-12,2),new Color32(47,73,87,255));
  Fill(new Rect(r.x+9,r.y+12,3,8),Red);Fill(new Rect(r.x+9,r.y+22,3,8),Blue);
  if(!string.IsNullOrEmpty(text))GUI.Label(new Rect(r.x+24,r.y+8,r.width-34,r.height-16),text,Text());
 }
 public static bool Button(Rect r,string text,bool selected=false){
  bool enabled=GUI.enabled,hot=enabled&&r.Contains(Event.current.mousePosition),down=hot&&Input.GetMouseButton(0);
  var visual=r;if(down)visual.y+=2;
  Panel(visual);
  if(hot||selected){Fill(new Rect(visual.x+3,visual.y,visual.width-6,2),Blue);Fill(new Rect(visual.x+3,visual.yMax-3,visual.width-6,2),Red);}
  var style=Text(r.height<40?14:16,TextAnchor.MiddleCenter);if(!enabled)style.normal.textColor=new Color32(126,149,160,255);
  GUI.Label(new Rect(visual.x+20,visual.y,visual.width-26,visual.height),text,style);
  return GUI.Button(r,GUIContent.none,GUIStyle.none);
 }
 public static void Label(Rect r,string text){GUI.Label(r,text,Text());}
 public static void StyleButton(Button b){
  if(!frameSprite){
   frameTexture=new Texture2D(24,24,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
   var pixels=new Color[576];
   for(int y=0;y<24;y++)for(int x=0;x<24;x++){
    bool corner=(x<2||x>21)&&(y<2||y>21);var c=corner?Color.clear:(x<2||x>21||y<2||y>21)?Border:Face;
    if(x==4&&y>=10&&y<=14)c=Blue;if(x==4&&y>=15&&y<=18)c=Red;pixels[y*24+x]=c;
   }
   frameTexture.SetPixels(pixels);frameTexture.Apply();frameSprite=Sprite.Create(frameTexture,new Rect(0,0,24,24),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(7,7,7,7));
  }
  var image=b.GetComponent<Image>();image.sprite=frameSprite;image.type=Image.Type.Sliced;image.color=Color.white;
  var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.2f,1.4f,1.5f);colors.pressedColor=new Color(.7f,.85f,1);colors.disabledColor=new Color(.5f,.5f,.5f,.7f);colors.fadeDuration=0;b.colors=colors;
 }
}
}
