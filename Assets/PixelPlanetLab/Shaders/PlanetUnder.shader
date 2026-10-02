Shader "PixelPlanetLab/PlanetUnder" {
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.0
#include "UnityCG.cginc"
struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
v2f vert(appdata_base v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=float2(v.texcoord.x,1-v.texcoord.y); return o; }
float glmod(float a,float b){ return a-b*floor(a/b); }
float2 glmod(float2 a,float2 b){ return a-b*floor(a/b); }
float pixels;
float rotation;
float2 light_origin;
float time_speed;
float dither_size;
float light_border_1;
float light_border_2;
float4 colors[3];
float size;
int OCTAVES;
float seed;
float time;
bool should_dither;

















float rand(float2 coord) {
	// land has to be tiled
	// tiling only works for integer values, thus the rounding
	// it would probably be better to only allow integer sizes
	// multiply by float2(2,1) to simulate planet having another side
	coord = glmod(coord, float2(2.0,1.0)*round(size));
	return frac(sin(dot(coord.xy ,float2(12.9898,78.233))) * 15.5453 * seed);
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

bool dither(float2 uv1, float2 uv2) {
	return glmod(uv1.x+uv2.y,2.0/pixels) <= 1.0 / pixels;
}

float2 rotate(float2 coord, float angle){
	coord -= 0.5;
	coord = mul(coord, float2x2(cos(angle), -sin(angle), sin(angle), cos(angle)));
	return coord + 0.5;
}

float2 spherify(float2 uv) {
	float2 centered= uv *2.0-1.0;
	float z = sqrt(max(0.0,1.0 - dot(centered.xy, centered.xy)));
	float2 sphere = centered/(z + 1.0);
	return sphere * 0.5+0.5;
}


float4 frag(v2f input) : SV_Target {
	//pixelize uv
	float2 uv = floor(input.uv*pixels)/pixels;
	
	bool dith = dither(uv, input.uv);
	
	// check distance distance to light
	float d_light = distance(uv , light_origin);
	
	// cut out a circle
	float d_circle = distance(uv, float2(0.5,0.5));
	// stepping over 0.5 instead of 0.49999 makes some pixels a little buggy
	float a = step(d_circle, 0.49999);
	
	uv = spherify(uv);
	uv = rotate(uv, rotation);
	
	// get a noise value with light distance added
	d_light += fbm(uv*size+float2(time*time_speed, 0.0))*0.3; // change the magic 0.3 here for different light strengths
	
	// size of edge in which colors should be dithered
	float dither_border = (1.0/pixels)*dither_size;

	// now we can assign colors based on distance to light origin
	float4 col = colors[0];
	if (d_light > light_border_1) {
		col = colors[1];
		if (d_light < light_border_1 + dither_border && (dith || !should_dither)) {
			col = colors[0];
		}
	}
	if (d_light > light_border_2) {
		col = colors[2];
		if (d_light < light_border_2 + dither_border && (dith || !should_dither)) {
			col = colors[1];
		}
	}
	
	return float4(col.rgb, a * col.a);
}

ENDCG } } }