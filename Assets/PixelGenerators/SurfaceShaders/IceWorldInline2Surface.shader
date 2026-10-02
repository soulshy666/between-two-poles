// 3D surface adaptation of the bundled Pixel Planets shader; see PixelPlanets-LICENSE.txt.
Shader "PixelGenerators/Surface/IceWorldInline2" {
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#include "UnityCG.cginc"
struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float3 objectP:TEXCOORD1; };
v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=float2(v.texcoord.x,1-v.texcoord.y); o.objectP=v.vertex.xyz; return o; }
float glmod(float a,float b){return a-b*floor(a/b);}
float2 glmod(float2 a,float2 b){return a-b*floor(a/b);}
float2 glmod(float2 a,float b){return a-b*floor(a/b);}
float clockTime; float2 motion; float warpAmount;
bool should_dither;
float pixels;
float rotation;
float2 light_origin;
float time_speed;
float light_border_1;
float light_border_2;
float lake_cutoff;
float4 colors[3];
float size;
int OCTAVES;
float seed;
float time;



















float rand(float2 coord) {
	coord = glmod(coord, float2(2.0,1.0)*round(size));
	return frac(sin(dot(coord.xy ,float2(12.9898,78.233))) * 43758.5453 * seed);
}

float noise(float2 coord){
	float2 i = floor(coord);
	float2 f = frac(coord);
		
	float a = rand(i);
	float b = rand(i + float2(1.0, 0.0));
	float c = rand(i + float2(0.0, 1.0));
	float d = rand(i + float2(1.0, 1.0));

	float2 cubic = f * f * (3.0 - 2.0 * f);

	return lerp(a, b, cubic.x) + (c - a) * cubic.y * (1.0 - cubic.x) + (d - b) * cubic.x * cubic.y;
}

float fbm(float2 coord){
	float value = 0.0;
	float scale = 0.5;

	for(int i = 0; i < OCTAVES ; i++){
		value += noise(coord) * scale;
		coord *= 2.0;
		scale *= 0.5;
	}
	return value;
}

static float2 surfaceUV;
float2 spherify(float2 uv) { return surfaceUV; }

float2 rotate(float2 coord, float angle){
	coord -= 0.5;
	coord = mul(coord, float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)));
	return coord + 0.5;
}

float4 frag(v2f input) : SV_Target {
float3 normal=normalize(input.objectP);
 float3 viewNormal=normalize(mul((float3x3)UNITY_MATRIX_V,UnityObjectToWorldNormal(normal)));
 input.uv=float2(.5+viewNormal.x*.499,.5-viewNormal.y*.499);
 float cs=cos(rotation),sn=sin(rotation);normal.xy=float2(cs*normal.x-sn*normal.y,sn*normal.x+cs*normal.y);
 surfaceUV=float2(atan2(normal.x,normal.z)/UNITY_PI+1,asin(clamp(normal.y,-1,1))/UNITY_PI+.5);
 surfaceUV=floor(surfaceUV*max(pixels,1))/max(pixels,1);

	// pixelize uv
	float2 uv = floor(input.uv*pixels)/pixels;
	
	float d_light = distance(uv , light_origin);
	
	// give planet a tilt
	
	float d_circle = distance(uv, float2(0.5,0.5));
//	// map to sphere
	uv = spherify(uv);
	
	// some scrolling noise for landmasses
	float fbm1 = fbm(uv*size+float2(time*time_speed,0.0));
	float lake = fbm(uv*size+float2(time*time_speed,0.0));
	
	// increase contrast on d_light
	d_light = pow(d_light, 2.0)*0.4;
	d_light -= d_light * lake;

	
	float4 col = colors[0];
	if (d_light > light_border_1) {
		col = colors[1];
	}
	if (d_light > light_border_2) {
		col = colors[2];
	}
	
	float a = step(lake_cutoff, lake);
	a *= 1.0;
	return float4(col.rgb, a * col.a);
}

ENDCG } } }