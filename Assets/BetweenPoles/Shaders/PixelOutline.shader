Shader "BetweenPoles/PixelOutline" {
Properties { _MainTex("Scene",2D)="white"{} _DepthEdge("Depth",Float)=.16 _NormalEdge("Normal",Float)=.13 }
SubShader { Cull Off ZWrite Off ZTest Always Pass {
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex, _CameraDepthNormalsTexture;
float4 _MainTex_TexelSize;
float _DepthEdge,_NormalEdge;
fixed4 frag(v2f_img i):SV_Target {
 float d;float3 n; DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture,i.uv),d,n);
 float de=0,ne=0;
 for(int k=0;k<4;k++) {
  float2 dir=k==0?float2(1,0):k==1?float2(-1,0):k==2?float2(0,1):float2(0,-1);
  float nd;float3 nn;DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture,i.uv+dir*_MainTex_TexelSize.xy),nd,nn);
  de=max(de,step(.0015,abs(d-nd)));
  ne=max(ne,smoothstep(.2,.8,length(n-nn)));
 }
 fixed4 c=tex2D(_MainTex,i.uv);c.rgb*=1-de*_DepthEdge-ne*_NormalEdge;return c;
}
ENDCG
} } }
