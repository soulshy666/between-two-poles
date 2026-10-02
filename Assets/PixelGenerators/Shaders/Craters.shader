Shader "PixelGenerators/Craters" {
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
float light_border;
float4 colors[2];
float size;
float seed;
float time;













float rand(float2 coord) {
	coord = glmod(coord, float2(1.0,1.0)*round(size));
	return frac(sin(dot(coord.xy ,float2(12.9898,78.233))) * 15.5453 * seed);
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
		c *= circleNoise((uv * size) + (float(i+1)+10.) + float2(time*time_speed,0.0));
	}
	return 1.0 - c;
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
	//pixelize uv
	float2 uv = floor(input.uv*pixels)/pixels;
	
	// check distance from center & distance to light
	float d_circle = distance(uv, float2(0.5,0.5));
	float d_light = distance(uv , light_origin);
	// cut out a circle
	// stepping over 0.5 instead of 0.49999 makes some pixels a little buggy
	float a = step(d_circle, 0.49999);
	
	uv = rotate(uv, rotation);
	uv = spherify(uv);
		
	float c1 = crater(uv );
	float c2 = crater(uv +(light_origin-0.5)*0.03);
	float4 col = colors[0];
	
	a *= step(0.5, c1);
	if (c2<c1-(0.5-d_light)*2.0) {
		col = colors[1];
	}
	if (d_light > light_border) {
		col = colors[1];
	} 

	// cut out a circle
	a*= step(d_circle, 0.5);
	return float4(col.rgb, a * col.a);
}

ENDCG } } }