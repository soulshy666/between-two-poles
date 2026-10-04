Shader "BetweenPoles/ChapterPlanetRing" {
Properties {_Color("Ring tint",Color)=(1,1,1,1)}
SubShader {Tags{"Queue"="Geometry-40" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct v2f {float4 pos:SV_POSITION;float3 local:TEXCOORD0;};
v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xyz;return o;}
float4 _Color;
float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float dust(float3 p){float3 k=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(k),hash(k+float3(1,0,0)),f.x),lerp(hash(k+float3(0,1,0)),hash(k+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(k+float3(0,0,1)),hash(k+float3(1,0,1)),f.x),lerp(hash(k+float3(0,1,1)),hash(k+1),f.x),f.y),f.z);}
float4 frag(v2f i):SV_Target {
 float3 toCamera=normalize(mul((float3x3)unity_WorldToObject,UNITY_MATRIX_V[2].xyz));
 float b=dot(i.local,toCamera);float d=b*b-dot(i.local,i.local)+100;
 if(d>0&&-b+sqrt(d)>0)discard;
 // Quantize the dust in the ring's frame, so it stays attached during planet turns.
 float3 p=floor(i.local*7)/7;float r=length(p);float band=(r-19.6)/2.8;
 float clouds=dust(p*1.9)*.65+dust(p*5)*.35;
 float edge=min(band,1-band);clip(edge-(clouds-.35)*.32);
 float lane=abs(band-(.47+.06*dust(p*.7)));
 clip(clouds-(lane<.045?.67:.26));
 float light=saturate(.58-dot(normalize(p),normalize(float3(1,0,1)))*.42);
 float level=floor(saturate(light*.7+clouds*.3)*4)/3;
 float3 color=level>.66?float3(.96,.73,.46):level>.33?float3(.67,.43,.26):float3(.32,.22,.28);
 return float4(color*_Color.rgb,.84);
}
ENDCG}}}
