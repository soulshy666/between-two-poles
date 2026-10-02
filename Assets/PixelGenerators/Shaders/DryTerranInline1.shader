Shader "PixelGenerators/DryTerranInline1" {
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
float rotation;
float2 light_origin;
float light_distance1;
float light_distance2;
float time_speed;
float dither_size;
float4 colors[5];
int n_colors;
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

bool dither(float2 uv1, float2 uv2) {
 if(!should_dither) return false;
	return glmod(uv1.x+uv2.y,2.0/pixels) <= 1.0 / pixels;
}

float2 rotate(float2 coord, float angle){
	coord -= 0.5;
	coord = mul(coord, float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)));
	return coord + 0.5;
}

float2 spherify(float2 uv) {
	float2 centered= uv *2.0-1.0;
	float z = sqrt(max(0.00001,1.0 - dot(centered.xy, centered.xy)));
	float2 sphere = centered/(z + 1.0);
	return sphere * 0.5+0.5;
}


float4 frag(v2f input) : SV_Target {
	//pixelize uv
	float2 uv = floor(input.uv*pixels)/pixels;
	bool dith = dither(uv, input.uv);
	
	// cut out a circle
	float d_circle = distance(uv, float2(0.5,0.5));
	float a = step(d_circle, 0.49999);
	
	uv = spherify(uv);
	
	// check distance distance to light
	float d_light = distance(uv , light_origin);
	
	uv = rotate(uv, rotation);
	
	// noise
	float f = fbm(uv*size+float2(time*time_speed, 0.0));
	
	// remap light
	d_light = smoothstep(-0.3, 1.2, d_light);
	
	if (d_light < light_distance1) {
		d_light *= 0.9;
	}
	if (d_light < light_distance2) {
		d_light *= 0.9;
	}
	
	
	float c = d_light*pow(f,0.8)*3.5; // change the magic nums here for different light strengths
	
	// apply dithering
	if (dith || !should_dither) {
		c += 0.02;
		c *= 1.05;
	}
	
	// now we can assign colors based on distance to light origin
	float posterize = floor(c*4.0)/4.0;
	posterize = min(posterize, 1.0);
	float4 col = colors[int(posterize * float(n_colors-1))];
	
	return float4(col.rgb, a * col.a);
}

ENDCG } } }