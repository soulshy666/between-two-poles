Shader "PixelGenerators/SpaceNebulae" {
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
float size;
int OCTAVES;
float seed;
float pixels;
sampler2D colorscheme;
float4 background_color;
bool should_tile;
bool reduce_background;
bool pixel_art;
bool clear_edges;
float edge_clear_margin;
float2 uv_correct;
















float rand(float2 coord, float tilesize) {
	if (should_tile) {
		coord = glmod(coord, tilesize * uv_correct);
	}

	return frac(sin(dot(coord.xy ,float2(12.9898,78.233))) * (15.5453 + seed));
}

float noise(float2 coord, float tilesize){
	float2 i = floor(coord);
	float2 f = frac(coord);
		
	float a = rand(i, tilesize);
	float b = rand(i + float2(1.0, 0.0), tilesize);
	float c = rand(i + float2(0.0, 1.0), tilesize);
	float d = rand(i + float2(1.0, 1.0), tilesize);

	float2 cubic = f * f * (3.0 - 2.0 * f);

	return lerp(a, b, cubic.x) + (c - a) * cubic.y * (1.0 - cubic.x) + (d - b) * cubic.x * cubic.y;
}

float fbm(float2 coord, float tilesize){
	float value = 0.0;
	float scale = 0.5;

	for(int i = 0; i < OCTAVES ; i++){
		value += noise(coord, tilesize ) * scale;
		coord *= 2.0; tilesize *= 2.0;
		scale *= 0.5;
	}
	return value;
}

bool dither(float2 uv1, float2 uv2) {
 if(!should_dither) return false;
	return glmod(uv1.y+uv2.x,2.0/pixels) <= 1.0 / pixels;
}

float circleNoise(float2 uv, float tilesize) {
	if (should_tile) {
		uv = glmod(uv, tilesize * uv_correct);
	}
	
    float uv_y = floor(uv.y);
    uv.x += uv_y*.31;
    float2 f = frac(uv);
	float h = rand(float2(floor(uv.x),floor(uv_y)), tilesize);
    float m = (length(f-0.25-(h*0.5)));
    float r = h*0.25;
    return smoothstep(0.0, r, m*0.75);
}

float2 rotate(float2 vec, float angle) {
	vec -=float2(0.5,0.5);
	vec = mul(vec, float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)));
	vec += float2(0.5,0.5);
	return vec;
}

float cloud_alpha(float2 uv, float tilesize) {
	float c_noise = 0.0;
	
	// more iterations for more turbulence
	int iters = 2;
	for (int i = 0; i < iters; i++) {
		c_noise += circleNoise(uv * 0.5 + (float(i+1)) + float2(-0.3, 0.0), ceil(tilesize * 0.5));
	}
	float cloudValue = fbm(uv+c_noise, tilesize);
	
	return cloudValue;
}

float edge_mask(float2 uv) {
	if (!clear_edges) {
		return 1.0;
	}
	float edge_dist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
	return smoothstep(edge_clear_margin, edge_clear_margin + 0.03, edge_dist);
}

float4 frag(v2f input) : SV_Target {
 float2 originalUV = input.uv;
 input.uv += motion * clockTime;
 input.uv += warpAmount * sin(input.uv.yx*6.28318+clockTime*.12);
	// pixelizing and dithering
	float2 uv = input.uv;
	if (pixel_art) {
		uv = floor(input.uv * pixels) / pixels;
	}
	
	// distance from center
	float d =  distance(uv, float2(0.5,0.5)) * 0.4;
	
	uv *= uv_correct;
	bool dith = false;
	if (pixel_art) {
		dith = dither(uv, input.uv);
	}
	
	// noise for the inside of the nebulae
	float n = cloud_alpha(uv * size, size);
	float n2 = fbm(uv * size + float2(1, 1), size);
	float n_lerp = n2 * n;
	float n_dust = cloud_alpha(uv * size, size);
	float n_dust_lerp = n_dust * n_lerp;

	// apply dithering
	if (dith) {
		n_dust_lerp *= 0.95;
		n_lerp *= 0.95;
		d*= 0.98;
	}

	// slightly offset alpha values to create thin bands around the nebulae
	float a = step(n2, 0.1 + d);
	float a2 = step(n2, 0.115 + d);
	if (should_tile) {
		a = step(n2, 0.3);
		a2 = step(n2, 0.315);
	}

	// choose colors
	if (reduce_background) {
		n_dust_lerp = pow(n_dust_lerp, 1.2) * 0.7;
	}
	float col_value = 0.0;
	if (a2 > a) {
		col_value = n_dust_lerp * 35.0 / 7.0;
		if (pixel_art) {
			col_value = floor(n_dust_lerp * 35.0) / 7.0;
		}
	} else {
		col_value = n_dust_lerp * 14.0 / 7.0;
		if (pixel_art) {
			col_value = floor(n_dust_lerp * 14.0) / 7.0;
		}
	}
	
	// apply colors
	float3 col = tex2D(colorscheme, float2(col_value, 0.0)).rgb;
	if (col_value < 0.1) {
		col = background_color.rgb;
	}

	return float4(col, a2 * edge_mask(originalUV));
}

ENDCG } } }