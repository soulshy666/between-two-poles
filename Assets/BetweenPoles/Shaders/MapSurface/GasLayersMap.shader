// 3D surface adaptation of the bundled Pixel Planets shader; see PixelPlanets-LICENSE.txt.
Shader "BetweenPoles/MapSurface/GasLayers" {
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
float pixels;
float rotation;
float cloud_cover;
float2 light_origin;
float time_speed;
float stretch;
float cloud_curve;
float light_border_1;
float light_border_2;
float bands;
bool should_dither;
int n_colors;
float4 colors[3];
float4 dark_colors[3];
float size;
int OCTAVES;
float seed;
float time;
























float rand(float2 coord) {
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

// by Leukbaars from https://www.shadertoy.com/view/4tK3zR
float circleNoise(float2 uv) {
    float uv_y = floor(uv.y);
    uv.x += uv_y*.31;
    float2 f = frac(uv);
	float h = rand(float2(floor(uv.x),floor(uv_y)));
    float m = (length(f-0.25-(h*0.5)));
    float r = h*0.25;
    return smoothstep(0.0, r, m*0.75);
}

float turbulence(float2 uv) {
	float c_noise = 0.0;
	
	
	// more iterations for more turbulence
	for (int i = 0; i < 10; i++) {
		c_noise += circleNoise((uv * size *0.3) + (float(i+1)+10.) + (float2(time * time_speed, 0.0)));
	}
	return c_noise;
}

bool dither(float2 uv_pixel, float2 uv_real) {
 if(!should_dither) return false;
	return glmod(uv_pixel.x+uv_real.y,2.0/pixels) <= 1.0 / pixels;
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
	float light_d = distance(uv, light_origin);
	
	// we use this value later to dither between colors
	bool dith = dither(uv, input.uv);
	
	// stepping over 0.5 instead of 0.49999 makes some pixels a little buggy
	float a = 1.0;
	
	// rotate planet
	
	
	// map to sphere
	uv = spherify(uv);

	// a band is just one dimensional noise
	float band = fbm(float2(0.0, uv.y*size*bands));
	
	// turbulence value is circles on top of each other
	float turb = turbulence(uv);

	// by layering multiple noise values & combining with turbulence and bands
	// we get some dynamic looking shape	
	float fbm1 = fbm(uv*size);
	float fbm2 = fbm(uv*float2(1.0, 2.0)*size+fbm1+float2(-time*time_speed,0.0)+turb);
	
	// all of this is just increasing some contrast & applying light
	fbm2 *= pow(band,2.0)*7.0;
	float light = fbm2 + light_d*1.8;
	fbm2 += pow(light_d, 1.0)-0.3;
	fbm2 = smoothstep(-0.2, 4.0-fbm2, light);
	
	// apply the dither value
	if (dith && should_dither) {
		fbm2 *= 1.1;
	}
	
	// finally add colors
	float posterized = floor(fbm2*4.0)/2.0;
	float4 col = float4(float3(0.0,0.0,0.0), 1.0);
	if (fbm2 < 0.625) {
		col = colors[int(posterized * float(n_colors - 1))];
	} else {
		col = dark_colors[int((posterized-1.0) * float(n_colors - 1))];
	}
		
	return float4(col.rgb, a * col.a);
}

ENDCG } } }