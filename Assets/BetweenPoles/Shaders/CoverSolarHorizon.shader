Shader "BetweenPoles/CoverSolarHorizon" {
Properties { _Clock("Time",Float)=0 _Intensity("Brightness",Range(.5,1.5))=1 }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
float _Clock,_Intensity;
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
float fbm(float2 p){return noise(p)*.57+noise(p*2.1)*.28+noise(p*4.3)*.15;}
fixed4 frag(v2f_img i):SV_Target{
 float2 p=(floor(i.uv*4096)/4096-.5)*2;float r=length(p),angle=atan2(p.y,p.x),t=_Clock*.045;
 float swirl=fbm(p*5+float2(t*.4,-t*.3));
 float n=fbm(p*14+float2(swirl*4+t,-swirl*3));
 float edge=.59+(noise(p*13+t*.12)-.5)*.002;
 if(r<edge){
  float bright=floor(saturate(n*.8+swirl*.3)*6)/6;
  float rim=smoothstep(.40,.59,r);
  float3 c=lerp(float3(.80,.10,.018),float3(1,.64,.16),bright);
  c=lerp(c,float3(1,.42,.08),rim*.22);
  float cells=step(.61,n)*step(n,.68);c+=float3(.13,.11,.035)*cells;
  return fixed4(c*_Intensity,1);
 }
 float rays=.5+.5*sin(angle*17+sin(angle*7+t)*2+t*.3);
 float outer=.602+rays*.003+noise(p*9+t*.1)*.002;
 float fall=saturate((outer-r)/(outer-.59));
 float alpha=floor(fall*6)/6*.40;
 float filaments=pow(saturate(sin(angle*23+t+swirl*5)),9)*saturate(1-abs(r-(.596+sin(angle*4+t)*.002))*300);
 alpha=max(alpha,filaments*.65);
 float3 corona=lerp(float3(.85,.16,.02),float3(1,.77,.25),fall);
 return fixed4(corona*_Intensity,alpha*saturate((1-r)*12));
}
ENDCG } } }
