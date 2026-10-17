Shader "BetweenPoles/CoverModel" {
Properties {_Color("Tint",Color)=(1,1,1,1)}
SubShader {Tags {"RenderType"="Opaque"} Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
fixed4 _Color;
struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;};
v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);return o;}
fixed4 frag(v2f i):SV_Target {float3 n=normalize(i.normal);float key=saturate(dot(n,normalize(float3(-.5,.7,-.8))));float rim=saturate(dot(n,normalize(float3(-1,-.4,.1))));float light=.48+floor(key*4)/4*.7;return fixed4(_Color.rgb*light+_Color.rgb*float3(.25,.10,.025)*rim,1);}
ENDCG
}}}
