Shader "PixelGenerators/BlackHole" {
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#include "UnityCG.cginc"
struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=float2(v.texcoord.x,1-v.texcoord.y); return o; }
float glmod(float a,float b){return a-b*floor(a/b);}
float2 glmod(float2 a,float2 b){return a-b*floor(a/b);}
float2 glmod(float2 a,float b){return a-b*floor(a/b);}
float clockTime; float2 motion; float warpAmount;
bool should_dither;
float pixels;
float4 colors[3];
float radius;
float light_width;











float4 frag(v2f input) : SV_Target {
	// pixelize uv
	float2 uv = floor(input.uv*pixels)/pixels;
	
	// distance from center
	float d_to_center = distance(uv, float2(0.5,0.5));
	
	float4 col = colors[0];	
	if (d_to_center > radius - light_width) {
		col = colors[1];
	}	
	if (d_to_center > radius - light_width * 0.5) {
		col = colors[2];
	}
	
	float a = step(d_to_center, radius);
	return float4(col.rgb, a * col.a);
}

ENDCG } } }