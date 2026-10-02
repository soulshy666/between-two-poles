Shader "PixelGenerators/Ring" {
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
float2 light_origin;
float time_speed;
float light_border_1;
float light_border_2;
float ring_width;
float ring_perspective;
float scale_rel_to_planet;
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
	
	float light_d = distance(uv, light_origin);
	uv = rotate(uv, rotation);
	
	// center is used to determine ring position
	float2 uv_center = uv - float2(0.0, 0.5);
	
	// tilt ring
	uv_center *= float2(1.0, ring_perspective);
	float center_d = distance(uv_center,float2(0.5, 0.0));
	
	
	// cut out 2 circles of different sizes and only intersection of the 2.
	float ring = smoothstep(0.5-ring_width*2.0, 0.5-ring_width, center_d);
	ring *= smoothstep(center_d-ring_width, center_d, 0.4);
	
	// pretend like the ring goes behind the planet by removing it if it's in the upper half.
	if (uv.y < 0.5) {
		ring *= step(1.0/scale_rel_to_planet, distance(uv,float2(0.5,0.5)));
	}
	
	// rotate material in the ring
	uv_center = rotate(uv_center+float2(0, 0.5), time*time_speed);
	// some noise
	ring *= fbm(uv_center*size);
	
	// apply some colors based on final value
	float posterized = floor((ring+pow(light_d, 2.0)*2.0)*4.0)/4.0;
	posterized = min(posterized, 2.0);
	float4 col;
	if (posterized <= 1.0) {
		col = colors[int(posterized * float(n_colors - 1))];
	} else {
		col = dark_colors[int((posterized-1.0) * float(n_colors - 1))];
	}
	
	float ring_a = step(0.28, ring);
	return float4(col.rgb, ring_a * col.a);
}

ENDCG } } }