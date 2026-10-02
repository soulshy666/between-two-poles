Shader "PixelGenerators/Galaxy" {
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
float time_speed;
float dither_size;
bool should_dither;
float4 colors[7];
int n_colors;
float size;
int OCTAVES;
float seed;
float time;
float tilt;
float n_layers;
float layer_height;
float zoom;
float swirl;






















float rand(float2 coord) {
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

float2 rotate(float2 coord, float angle){
	coord -= 0.5;
	coord = mul(coord, float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)));
	return coord + 0.5;
}

bool dither(float2 uv1, float2 uv2) {
 if(!should_dither) return false;
	return glmod(uv1.x+uv2.y,2.0/pixels) <= 1.0 / pixels;
}

float4 frag(v2f input) : SV_Target {
	float2 uv = input.uv;
	uv = floor(uv * pixels) / pixels;
	bool dith = dither(uv, input.uv);
	
	// I added a little zooming functionality so I dont have to mess with other values to get correct sizing.
	uv *= zoom;
	uv -= (zoom - 1.0) / 2.0;
	
	// overall rotation of galaxy
	uv = rotate(uv, rotation);
	float2 uv2 = uv; // save a copy of untranslated uv for later

	// this uv is used to determine where the "layers" will be
	uv.y *= tilt;
	uv.y -= (tilt - 1.0) / 2.0;

	float d_to_center = distance(uv, float2(0.5, 0.5));
	// swirl uv around the center, the further from the center the more rotated.
	float rot = swirl * pow(d_to_center, 0.4);
	float2 rotated_uv = rotate(uv, rot + time * time_speed);

	// fbm will decide where the layers are
	float f1 = fbm(rotated_uv * size);
	// quantize to a few different values, so layers don't blur through each other
	f1 = floor(f1 * n_layers) / n_layers;

	// use the unaltered second uv for the actual galaxy
	// tilt so it looks like it's an angle.
	uv2.y *= tilt;
	uv2.y -= (tilt - 1.0) / 2.0 + f1 * layer_height;

	// now do the same stuff as before, but for the actual galaxy image, not the layers
	float d_to_center2 = distance(uv2, float2(0.5, 0.5));
    float rot2 = swirl * pow(d_to_center2, 0.4);
	float2 rotated_uv2 = rotate(uv2, rot2 + time * time_speed);
	// I offset the second fbm by some amount so the don't all use the same noise, try it wihout and the layers are very obvious
	float f2 = fbm(rotated_uv2 * size + float2(f1,f1) * 10.0);

	// alpha
	float a = step(f2 + d_to_center2, 0.7);
	
	// some final steps to choose a nice color
	f2 *= 2.3;
	if(should_dither && dith) { // dithering
		f2 *= 0.94;
	}

	f2 = floor(f2 * (float(n_colors)));
	f2 = min(f2, float(n_colors));
	float4 col = colors[int(f2)];
	
	return float4(col.rgb, a * col.a);
}
ENDCG } } }