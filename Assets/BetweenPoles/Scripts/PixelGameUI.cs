using UnityEngine;
using UnityEngine.UI;
namespace BetweenPoles {
// Shared chapter HUD styling. Integer-sized details retain hard pixel edges.
public static class PixelGameUI {
 public static Color Ink {get{return ElementGameUI.Ink;}}
 static readonly Color Border=new Color32(91,133,148,255),Face=new Color32(17,35,49,245),Blue=new Color32(80,194,219,255),Red=new Color32(255,79,105,255);
 static GUIStyle label;static Texture2D frameTexture;static Sprite frameSprite;
 static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
 public static GUIStyle Text(int size=16,TextAnchor align=TextAnchor.MiddleLeft){
  if(label==null)label=new GUIStyle(GUI.skin.label){font=Font.CreateDynamicFontFromOSFont(new[]{"SimSun","NSimSun","Microsoft YaHei"},16),wordWrap=true,padding=new RectOffset(0,0,0,0)};
  var s=new GUIStyle(label){fontSize=size,alignment=align};s.normal.textColor=Ink;return s;
 }
 public static void Panel(Rect r,string text=""){
  ElementGameUI.Panel(r);
  if(!string.IsNullOrEmpty(text))GUI.Label(new Rect(r.x+12,r.y+8,r.width-24,r.height-16),text,Text());
 }
 public static bool Button(Rect r,string text,bool selected=false){
  bool enabled=GUI.enabled,hot=enabled&&r.Contains(Event.current.mousePosition),down=hot&&Input.GetMouseButton(0);
  var visual=r;if(down)visual.y+=2;
  Panel(visual);
  var texture=ElementGameUI.TextTexture(text);
  if(texture){float k=Mathf.Max(1,Mathf.Floor(Mathf.Min((r.width-16)/texture.width,(r.height-8)/texture.height)));GUI.DrawTexture(new Rect(Mathf.Round(visual.center.x-texture.width*k/2),Mathf.Round(visual.center.y-texture.height*k/2),texture.width*k,texture.height*k),texture);}
  else if(MobileUI.Enabled&&CoverMenu.HasPixelText(text)){var color=GUI.color;GUI.color=Ink;CoverMenu.PixelText(visual,text,Mathf.Max(1,Mathf.Floor((r.height-8)/15)),true,false);GUI.color=color;}
  else{var style=Text(r.height<40?14:16,TextAnchor.MiddleCenter);GUI.Label(visual,text,style);}
  return MobileUI.Button("pixel-"+text,r);
 }
 public static void Label(Rect r,string text){GUI.Label(r,text,Text());}
 public static void StyleButton(Button b){
  var source=ElementGameUI.Asset("panel");
  if(!frameSprite||frameTexture!=source){
   frameTexture=source;
   if(frameSprite)Object.Destroy(frameSprite);
   frameSprite=Sprite.Create(frameTexture,new Rect(0,0,frameTexture.width,frameTexture.height),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(2,5,2,2));
  }
  var image=b.GetComponent<Image>();image.sprite=frameSprite;image.type=Image.Type.Sliced;image.color=Color.white;
  var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.2f,1.4f,1.5f);colors.pressedColor=new Color(.7f,.85f,1);colors.disabledColor=new Color(.5f,.5f,.5f,.7f);colors.fadeDuration=0;b.colors=colors;
 }
}
}
