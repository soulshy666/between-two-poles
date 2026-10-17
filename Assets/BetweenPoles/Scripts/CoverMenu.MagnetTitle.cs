using UnityEngine;
namespace BetweenPoles {
public sealed partial class CoverMenu {
 // Pixel-built pole marks and field lines share the title's canvas in play and edit mode.
 void DrawMagneticSignature(Rect titleRect){
  var previous=GUI.color;
  const float signatureWidth=280;
  float x=Mathf.Round(titleRect.center.x-signatureWidth*.5f),y=Mathf.Round(titleRect.yMax+24);
  Color north=new Color(.94f,.30f,.27f),south=new Color(.23f,.68f,.83f);
  MagnetPixel(x,y,60,16,north);MagnetPixel(x,y,16,16,new Color(.77f,.78f,.73f));
  MagnetPixel(x+220,y,60,16,south);MagnetPixel(x+264,y,16,16,new Color(.77f,.78f,.73f));
  // Small N / S engravings, drawn as pixels instead of a second font.
  DrawPoleGlyph(x+36,y+3,true);DrawPoleGlyph(x+233,y+3,false);
  var field=new Color(.40f,.65f,.69f,.40f);
  for(int i=0;i<40;i++){
   float px=x+60+i*4;
   float arch=Mathf.Round(Mathf.Sin(i/39f*Mathf.PI)*5)*2;
   MagnetPixel(px,y+5-arch,4,2,field);
   MagnetPixel(px,y+9+arch,4,2,field);
  }
  float phase=Mathf.Floor(AnimationTime*8)/8;
  for(int i=0;i<3;i++){
   float u=Mathf.Repeat(phase*.12f+i/3f,1);
   float px=x+60+Mathf.Floor(u*40)*4;
   float arch=Mathf.Round(Mathf.Sin(u*Mathf.PI)*5)*2;
   MagnetPixel(px,y+5-arch,4,2,Color.Lerp(north,south,u));
   MagnetPixel(px,y+9+arch,4,2,Color.Lerp(north,south,u));
  }
  GUI.color=previous;
 }
 static void MagnetPixel(float x,float y,float w,float h,Color color){GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);}
 static void DrawPoleGlyph(float x,float y,bool north){
  string bits=north?"10011101101110011001":"111100111001111";
  int width=north?4:3;
  for(int i=0;i<bits.Length;i++)if(bits[i]=='1')MagnetPixel(x+i%width*2,y+i/width*2,2,2,new Color(.96f,.94f,.86f));
 }
}
}
