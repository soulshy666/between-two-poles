Shader "PixelGenerators/Asteroids" {
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
float time_speed;
float4 colors[3];
float size;
int octaves;
float seed;
bool should_dither;













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

	for(int i = 0; i < octaves ; i++){
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

// by Leukbaars from https://www.shadertoy.com/view/4tK3zR
float circleNoise(float2 uv) {
    float uv_y = floor(uv.y);
    uv.x += uv_y*.31;
    float2 f = frac(uv);
	float h = rand(float2(floor(uv.x),floor(uv_y)));
    float m = (length(f-0.25-(h*0.5)));
    float r = h*0.25;
    return m = smoothstep(r-.10*r,r,m);
}

float crater(float2 uv) {
	float c = 1.0;
	for (int i = 0; i < 2; i++) {
		c *= circleNoise((uv * size) + (float(i+1)+10.));
	}
	return 1.0 - c;
}

float4 frag(v2f input) : SV_Target {
	//pixelize uv
	float2 uv = floor(input.uv*pixels)/pixels;
	
	// we use this val later to interpolate between shades
	bool dith = dither(uv, input.uv);
	
	// distance from center
	float d = distance(uv, float2(0.5,0.5));
	
	// optional rotation, do this after the dither or the dither will look very messed up
	uv = rotate(uv, rotation);
	
	// two noise values with one slightly offset according to light source, to create shadows later
	float n = fbm(uv * size);
	float n2 = fbm(uv * size + (rotate(light_origin, rotation)-0.5) * 0.5);
	
	// step noise values to determine where the edge of the asteroid is
	// step cutoff value depends on distance from center
	float n_step = step(0.2, n - d);
	float n2_step = step(0.2, n2 - d);
	
	// with this val we can determine where the shadows should be
	float noise_rel = (n2_step + n2) - (n_step + n);
	
	// two crater values, again one extra for the shadows
	float c1 = crater(uv );
	float c2 = crater(uv + (light_origin-0.5)*0.03);

	// now we just assign colors depending on noise values and crater values
	// base
	float4 col = colors[1];
	
	// noise
	if (noise_rel < -0.06 || (noise_rel < -0.04 && (dith || !should_dither))) {
		col = colors[0];
	}
	if (noise_rel > 0.05 || (noise_rel > 0.03 && (dith || !should_dither))) {
		col = colors[2];
	}
	
	// crater
	if (c1 > 0.4)  {
		col = colors[1];
	}
	if (c2<c1) {
		col = colors[2];
	}
	
	return float4(col.rgb, n_step * col.a);
}

ENDCG } } }