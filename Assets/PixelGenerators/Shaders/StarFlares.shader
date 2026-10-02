Shader "PixelGenerators/StarFlares" {
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
float4 colors[2];
float time_speed;
float time;
float rotation;
bool should_dither;
float storm_width;
float storm_dither_width;
float scale;
float seed;
float circle_amount;
float circle_scale;
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
	return smoothstep(circle, circle+0.5, invert * circle_scale * rand(rand_co*1.5));
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
	
	// use dither val later to interpolate between alpha
	bool dith = dither(input.uv, pixelized);
	
	pixelized = rotate(pixelized, rotation);
	
	// counter rotation against rotation caused by the way uv's are made later
	float2 uv = pixelized;//rotate(pixelized, -time  * time_speed);
	
	// angle from centered uv's
	float angle = atan2(uv.x - 0.5, uv.y - 0.5) * 0.4;
	// distance from center
	float d = distance(pixelized, float2(0.5,0.5));
	
	// we make uv circular here to have eternally outward moving stuff
	float2 circleUV = float2(d, angle);
	
	// two types of noise values
	float n = fbm(circleUV*size -time * time_speed);
	float nc = circle(circleUV*scale -time * time_speed + n);
	
	nc *= 1.5;
	float n2 = fbm(circleUV*size -time + float2(100, 100));
	nc -= n2 * 0.1;
	
	// our alpha, default 0
	float a = 0.0;
	if (1.0 - d > nc) {
		// now we generate very thin strips of positive alpha if our noise has certain values and is close enough to center
		if (nc > storm_width - storm_dither_width + d && (dith || !should_dither)) {
			a = 1.0;
		} else if (nc > storm_width + d) { // could use an or statement instead, but this looks more clear to me
			a = 1.0;
		}
	}
	
	// use our two noise values to assign colors
	float interpolate = floor(n2 + nc);
	float4 col = colors[int(interpolate)];
	
	// final step to not have everything appear from the center
	a *= step(n2 * 0.25, d);
	return float4(col.rgb, a * col.a);
}
ENDCG } } }