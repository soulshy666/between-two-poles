Shader "PixelGenerators/Clouds" {
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
float rotation;
float cloud_cover;
float2 light_origin;
float time_speed;
float stretch;
float cloud_curve;
float light_border_1;
float light_border_2;
float4 colors[4];
float size;
int OCTAVES;
float seed;
float time;





















float rand(float2 coord) {
	coord = glmod(coord, float2(1.0,1.0)*round(size));
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

float cloud_alpha(float2 uv) {
	float c_noise = 0.0;
	
	// more iterations for more turbulence
	for (int i = 0; i < 9; i++) {
		c_noise += circleNoise((uv * size * 0.3) + (float(i+1)+10.) + (float2(time*time_speed, 0.0)));
	}
	float cloudValue = fbm(uv*size+c_noise + float2(time*time_speed, 0.0));
	
	return cloudValue;//step(a_cutoff, fbm);
}

bool dither(float2 uv_pixel, float2 uv_real) {
 if(!should_dither) return false;
	return glmod(uv_pixel.x+uv_real.y,2.0/pixels) <= 1.0 / pixels;
}

float2 spherify(float2 uv) {
	float2 centered= uv *2.0-1.0;
	float z = sqrt(max(0.00001,1.0 - dot(centered.xy, centered.xy)));
	float2 sphere = centered/(z + 1.0);
	return sphere * 0.5+0.5;
}

float2 rotate(float2 coord, float angle){
	coord -= 0.5;
	coord = mul(coord, float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)));
	return coord + 0.5;
}

float4 frag(v2f input) : SV_Target {
	// pixelize uv
	float2 uv = floor(input.uv*pixels)/pixels;
	
	// distance to light source
	float d_light = distance(uv , light_origin);
	
	
	// stepping over 0.5 instead of 0.49999 makes some pixels a little buggy
	float a = step(length(uv-float2(0.5,0.5)), 0.49999);
	// cut out a circle
	float d_to_center = distance(uv, float2(0.5,0.5));
	
	uv = rotate(uv, rotation);
	
	// map to sphere
	uv = spherify(uv);
	// slightly make uv go down on the right, and up in the left
	uv.y += smoothstep(0.0, cloud_curve, abs(uv.x-0.4));
	
	
	float c = cloud_alpha(uv*float2(1.0, stretch));
	
	// assign some colors based on cloud depth & distance from light
	float4 col = colors[0];
	if (c < cloud_cover + 0.03) {
		col = colors[1];
	}
	if (d_light + c*0.2 > light_border_1) {
		col = colors[2];

	}
	if (d_light + c*0.2 > light_border_2) {
		col = colors[3];
	}
	
	c *= step(d_to_center, 0.5);
	return float4(col.rgb, step(cloud_cover, c) * a * col.a);
}
ENDCG } } }