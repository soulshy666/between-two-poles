Shader "BetweenPoles/CoverEarthAtmosphere" {
SubShader { Tags {"Queue"="Geometry-21" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
fixed4 frag(v2f_img i):SV_Target {
float2 p=(floor(i.uv*1000)/1000-.5)*2;float r=length(p);
float a=saturate(1-abs(r-.985)/.015);a=floor(a*5)/5;
float light=saturate(dot(normalize(p),normalize(float2(-.5,1)))*.5+.5);
float3 c=float3(.12,.65,.90);
#ifndef UNITY_COLORSPACE_GAMMA
c=GammaToLinearSpace(c);
#endif
return float4(c,a*light*.8);
}
ENDCG } } }
