Shader "PixelGenerators/SpacePlanet" {
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
float2 light_origin;
bool pixel_art;












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

bool dither(float2 uv1, float2 uv2) {
 if(!should_dither) return false;
	return glmod(uv1.y+uv2.x,2.0/pixels) <= 1.0 / pixels;
}

float circleNoise(float2 uv) {
    float uv_y = floor(uv.y);
    uv.x += uv_y*.31;
    float2 f = frac(uv);
	float h = rand(float2(floor(uv.x),floor(uv_y)));
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

float2 spherify(float2 uv) {
	float2 centered = uv *2.0-1.0;
	float z = sqrt(max(0.00001,1.0 - dot(centered.xy, centered.xy)));
//	float z = pow(1.0 - dot(centered.xy, centered.xy), 0.5);
	float2 sphere = centered/(z + 1.0);
	
	return sphere * 0.5+0.5;
}

float cloud_alpha(float2 uv) {
	float c_noise = 0.0;
	
	// more iterations for more turbulence
	int iters = 2;
	for (int i = 0; i < iters; i++) {
		float relative = (float(i)/float(iters));
		float2 c_uv = rotate(uv, relative * 6.28);
		c_noise += circleNoise((uv * 0.3) + (float(i+1)+10.));
	}
	float cloudValue = fbm(uv+c_noise);
	
	return cloudValue;
}


float4 frag(v2f input) : SV_Target {
	/// pixelzing and dithering
	float2 uv = input.uv;
	bool dith = false;
	if (pixel_art) {
		uv = floor(input.uv * pixels) / pixels;
		dith = dither(input.uv, uv);
	}
	
	// distance from center, to create a circle
	float d_to_center = distance(uv , float2(0.5,0.5));
	uv = spherify(uv);
	
	// distance from light source, to create shading
	float d_to_light = distance(uv, light_origin);
	// bit of contrast
	d_to_light += pow(d_to_center * 3.0, 4.0) * 0.05;
	
	// noise for the planet
	float n = fbm(uv * size);
	float n2 = fbm(uv * size + n*3.0);
	float lerped = n2 * d_to_light;
	
	// optionally create some contrast with this
//	lerped = pow(lerped, 1.0);

	// apply dithering
	if (dith) {
		lerped *= 0.95;
	}
	
	// choose and apply colors
	float col_val = lerped * 15.0 / 7.0;
	if (pixel_art) {
		col_val = floor(lerped * 15.0) / 7.0;
	}
	float3 col = tex2D(colorscheme, float2(col_val, 0.0)).rgb;
	
	// apply alpha
	float a = smoothstep(0.505, 0.485, d_to_center);
	return float4(col, a);
}
ENDCG } } }