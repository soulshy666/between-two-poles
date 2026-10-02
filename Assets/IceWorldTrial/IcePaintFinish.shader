Shader "BetweenPoles/IcePaintFinish" {
Properties { _MainTex("Scene",2D)="white"{} _DepthEdge("Depth",Float)=.16 _NormalEdge("Normal",Float)=.13 }
SubShader { Cull Off ZWrite Off ZTest Always Pass {
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;
float4 _MainTex_TexelSize;
float _DepthEdge,_NormalEdge;
fixed4 frag(v2f_img i):SV_Target {
 // Built-in depth-normal replacement rendering does not apply the island bend.
 // Do not outline that unwarped buffer: island bevels and grid shading provide edges.
 fixed4 c=tex2D(_MainTex,i.uv);
 float3 halo=0;
 for(int j=0;j<4;j++){float2 dir=j==0?float2(1,0):j==1?float2(-1,0):j==2?float2(0,1):float2(0,-1);float3 sample=tex2D(_MainTex,i.uv+dir*_MainTex_TexelSize.xy*2).rgb;float icy=saturate((sample.b-sample.r)*5+.15)*step(sample.r,sample.g);halo+=sample*smoothstep(.76,.96,min(sample.r,min(sample.g,sample.b)))*icy;}
 c.rgb+=halo*.025;return c;
}
ENDCG
} } }
