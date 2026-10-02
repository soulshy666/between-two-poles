Shader "PixelGenerators/StarBlobs" {
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
float4 colors[1];
float time_speed;
float time;
float rotation;
float seed;
float circle_amount;
float circle_size;
float size;
int OCTAVES;


















float rand(float2 co){
	co = glmod(co, float2(1.0,1.0)*round(size));
    return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 15.5453 * seed);
}


float2 rotate(float2 vec, float angle) {
	vec -=float2(0.5,0.5);
	vec = mul(vec, float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)));
	vec += float2(0.5,0.5);
	return vec;
}

float circle(float2 uv) {
	float invert = 1.0 / circle_amount;
	
	if (glmod(uv.y, invert*2.0) < invert) {
		uv.x += invert*0.5;
	}
	float2 rand_co = floor(uv*circle_amount)/circle_amount;
	uv = glmod(uv, invert)*circle_amount;
	
	float r = rand(rand_co);
	r = clamp(r, invert, 1.0 - invert);
	float circle = distance(uv, float2(r,r));
	return smoothstep(circle, circle+0.5, invert * circle_size * rand(rand_co*1.5));
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
	float scl = 0.5;

	for(int i = 0; i < OCTAVES ; i++){
		value += noise(coord) * scl;
		coord *= 2.0;
		scl *= 0.5;
	}
	return value;
}

float2 spherify(float2 uv) {
	float2 centered= uv *2.0-1.0;
	float z = sqrt(max(0.00001,1.0 - dot(centered.xy, centered.xy)));
	float2 sphere = centered/(z + 1.0);
	return sphere * 0.5+0.5;
}

float4 frag(v2f input) : SV_Target {
	float2 pixelized = floor(input.uv*pixels)/pixels;

	float2 uv = rotate(pixelized, rotation);

	// angle from centered uv's
	float angle = atan2(uv.x - 0.5, uv.y - 0.5);
	float d = distance(pixelized, float2(0.5,0.5));
	
	
	float c = 0.0;
	for(int i = 0; i < 15; i++) {
		float r = rand(float2(float(i),float(i)));
		float2 circleUV = float2(d, angle);
		c += circle(circleUV*size -time * time_speed - (1.0/max(d,0.0001)) * 0.1 + r);
	}
	
	c *= 0.37 - d;
	c = step(0.07, c - d);
	
	return float4(colors[0].rgb, c * colors[0].a);
}
ENDCG } } }