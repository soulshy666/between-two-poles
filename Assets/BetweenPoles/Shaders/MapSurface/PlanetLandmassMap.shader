// 3D surface adaptation of the bundled Pixel Planets shader; see PixelPlanets-LICENSE.txt.
Shader "BetweenPoles/MapSurface/PlanetLandmass" {
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#include "UnityCG.cginc"
struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float3 objectP:TEXCOORD1; };
v2f vert(appdata_base v) { v2f o; o.vertex=float4(v.texcoord.xy*2-1,0,1); o.uv=float2(v.texcoord.x,1-v.texcoord.y); o.objectP=v.vertex.xyz; return o; }
float glmod(float a,float b){return a-b*floor(a/b);}
float2 glmod(float2 a,float2 b){return a-b*floor(a/b);}
float2 glmod(float2 a,float b){return a-b*floor(a/b);}
float clockTime; float2 motion; float warpAmount;
bool should_dither;
float pixels;
float rotation;
float2 light_origin;
float time_speed;
float dither_size;
float light_border_1;
float light_border_2;
float land_cutoff;
float4 colors[4];
float size;
int OCTAVES;
float seed;
float time;




















float rand(float2 coord) {
	// land has to be tiled (or the contintents on this planet have to be changing very fast)
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

static float2 surfaceUV;
float2 spherify(float2 uv) { return surfaceUV; }

float2 rotate(float2 coord, float angle){
	coord -= 0.5;
	coord = mul(coord, float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)));
	return coord + 0.5;
}

float4 frag(v2f input) : SV_Target {
surfaceUV=floor(input.uv*float2(2,1)*max(pixels,1))/max(pixels,1);
 input.uv=float2(.34,.38)+ (input.uv-.5)*.18;


	// pixelize uv
	float2 uv = floor(input.uv*pixels)/pixels;
	
	float d_light = distance(uv , light_origin);
	// cut out a circle
	float d_circle = distance(uv, float2(0.5,0.5));
	// stepping over 0.5 instead of 0.49999 makes some pixels a little buggy
	float a = 1.0;
	
	// give planet a tilt
	
	
	// map to sphere
	uv = spherify(uv);
	
	// some scrolling noise for landmasses
	float2 base_fbm_uv = (uv)*size+float2(time*time_speed,0.0);
	
	// use multiple fbm's at different places so we can determine what color land gets
	float fbm1 = fbm(base_fbm_uv);
	float fbm2 = fbm(base_fbm_uv - light_origin*fbm1);
	float fbm3 = fbm(base_fbm_uv - light_origin*1.5*fbm1);
	float fbm4 = fbm(base_fbm_uv - light_origin*2.0*fbm1);
	
	// lots of magic numbers here
	// you can mess with them, it changes the color distribution
	if (d_light < light_border_1) {
		fbm4 *= 0.9;
	}
	if (d_light > light_border_1) {
		fbm2 *= 1.05;
		fbm3 *= 1.05;
		fbm4 *= 1.05;
	} 
	if (d_light > light_border_2) {
		fbm2 *= 1.3;
		fbm3 *= 1.4;
		fbm4 *= 1.8;
	} 
	
	// increase contrast on d_light
	d_light = pow(d_light, 2.0)*0.1;
	float4 col = colors[3];
	// assign colors based on if there is noise to the top-left of noise
	// and also based on how far noise is from light
	if (fbm4 + d_light < fbm1) {
		col = colors[2];
	}
	if (fbm3 + d_light < fbm1) {
		col = colors[1];
	}
	if (fbm2 + d_light < fbm1) {
		col = colors[0];
	}
	
	return float4(col.rgb, step(land_cutoff, fbm1) * a * col.a);
}

ENDCG } } }