Shader "PixelGenerators/Star" {
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
float pixels;
float time_speed;
float time;
float rotation;
float4 colors[4];
int n_colors;
bool should_dither;
float seed;
float size;
int OCTAVES;
float TILES;

















float rand(float2 co) {
	co = glmod(co, float2(1.0,1.0)*round(size));
    return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 15.5453 * seed);
}

float2 rotate(float2 vec, float angle) {
	vec -=float2(0.5,0.5);
	vec = mul(vec, float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)));
	vec += float2(0.5,0.5);
	return vec;
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

float2 Hash2(float2 p) {
	float r = 523.0*sin(dot(p, float2(53.3158, 43.6143)));
	return float2(frac(15.32354 * r), frac(17.25865 * r));
	
}

// Tileable cell noise by Dave_Hoskins from shadertoy: https://www.shadertoy.com/view/4djGRh
float Cells(in float2 p, in float numCells) {
	p *= numCells;
	float d = 1.0e10;
	for (int xo = -1; xo <= 1; xo++)
	{
		for (int yo = -1; yo <= 1; yo++)
		{
			float2 tp = floor(p) + float2(float(xo), float(yo));
			tp = p - tp - Hash2(glmod(tp, numCells / TILES));
			d = min(d, dot(tp, tp));
		}
	}
	return sqrt(d);
}

bool dither(float2 uv1, float2 uv2) {
 if(!should_dither) return false;
	return glmod(uv1.x+uv2.y,2.0/pixels) <= 1.0 / pixels;
}

float2 spherify(float2 uv) {
	float2 centered= uv *2.0-1.0;
	float z = sqrt(max(0.00001,1.0 - dot(centered.xy, centered.xy)));
	float2 sphere = centered/(z + 1.0);
	return sphere * 0.5+0.5;
}

float4 frag(v2f input) : SV_Target {
	// pixelize uv
	float2 pixelized = floor(input.uv*pixels)/pixels;
	
	// cut out a circle
	// stepping over 0.5 instead of 0.49999 makes some pixels a little buggy
	float a = step(distance(pixelized, float2(0.5,0.5)), .49999);
	
	// use dither val later to lerp between colors
	bool dith = dither(input.uv, pixelized);
	
	pixelized = rotate(pixelized, rotation);
	
	// spherify has to go after dither
	pixelized = spherify(pixelized);
	
	// use two different sized cells for some variation
	float n = Cells(pixelized - float2(time * time_speed * 2.0, 0), 10);
	n *= Cells(pixelized - float2(time * time_speed * 1.0, 0), 20);

	
	// adjust cell value to get better looking stuff
	n*= 2.;
	n = clamp(n, 0.0, 1.0);
	if (dith || !should_dither) { // here we dither
		n *= 1.3;
	}
	
	// constrain values 4 possibilities and then choose color based on those
	float interpolate = floor(n * float(n_colors - 1)) / float(n_colors - 1);
	float4 col = colors[int(interpolate * float(n_colors-1))];
	
	return float4(col.rgb, a * col.a);
}
ENDCG } } }