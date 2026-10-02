Shader "PixelGenerators/Backdrop" {
SubShader {Tags {"Queue"="Background" "RenderType"="Transparent"} Cull Off ZWrite Off
Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
float4 background_color;
float4 vert(float4 v:POSITION):SV_POSITION {return UnityObjectToClipPos(v);}
float4 frag():SV_Target {return float4(background_color.rgb,1);}
ENDCG }} }
