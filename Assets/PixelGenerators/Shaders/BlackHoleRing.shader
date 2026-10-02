Shader "PixelGenerators/BlackHoleRing" {
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
float disk_width;
float ring_perspective;
bool should_dither;
float4 colors[5];
int n_colors;
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
	
	// we use this value later to dither between colors
	bool dith = dither(input.uv, uv);
	
	uv = rotate(uv, rotation);
	
	// keep an undistored version of the current uvs
	float2 uv2 = uv;
	
	// compress uv along the x axis, or the accretion disk will look to stretched out
	uv.x -= 0.5;
	uv.x *= 1.3;
	uv.x += 0.5;
	
	// add a bit of movement to the accretion disk by wobbling it, completely optional and can be disabled.
	uv = rotate(uv, sin(time * time_speed * 2.0) * 0.01);
	
	// l_origin will be used to determine how to color the pixels
	float2 l_origin = float2(0.5,0.5);
	// d_width will be the final width of the accretion disk
	float d_width = disk_width;
	
	// here we distort the uvs to achieve the shape of the accretion disk
	if (uv.y < 0.5) { 
		// if we are in the top half of the image, then add to the uv.y based on how close we are to the center
		uv.y += smoothstep(distance(float2(0.5,0.5), uv), 0.5, 0.2);
		// and also the ring width has to be adjusted or it will look to stretched out
		d_width += smoothstep(distance(float2(0.5,0.5), uv), 0.5, 0.3);
		
		// another optional thing that changes the color distribution, I like it, but can be disabled.
		l_origin.y -= smoothstep(distance(float2(0.5,0.5), uv), 0.5, 0.2);
	} 
	// we don't check for exactly uv.y > 0.5 because we want a small area where the ring
	// is unaffected by stretching, the middle part that goes over the black hole.
	else if (uv.y > 0.53) {

		// same steps as before, but uv.y and light is stretched the other way, the disk width is slightly smaller here for visual effect.
		uv.y -= smoothstep(distance(float2(0.5,0.5), uv), 0.4, 0.17);
		d_width += smoothstep(distance(float2(0.5,0.5), uv), 0.5, 0.2);
		l_origin.y += smoothstep(distance(float2(0.5,0.5), uv), 0.5, 0.2);
	}
	
	// get distance to light origin based on unaltered uv's we saved earlier, some math to account for perspective
	float light_d = distance(uv2 * float2(1.0, ring_perspective), l_origin * float2(1.0, ring_perspective)) * 0.3;

	// center is used to determine ring position
	float2 uv_center = uv - float2(0.0, 0.5);

	// tilt ring
	uv_center *= float2(1.0, ring_perspective);
	float center_d = distance(uv_center,float2(0.5, 0.0));
	
	// cut out 2 circles of different sizes and only intersection of the 2.
	// this actually makes the disk
	float disk = smoothstep(0.1-d_width*2.0, 0.5-d_width, center_d);
	disk *= smoothstep(center_d-d_width, center_d, 0.4);
	
	// rotate noise in the disk
	uv_center = rotate(uv_center+float2(0, 0.5), time*time_speed*3.0);
	
	// some noise
	disk *= pow(fbm(uv_center*size), 0.5);
	
	// apply dithering
	if (dith || !should_dither) {
		disk *= 1.2;
	}
	
	// apply some colors based on final value
	float n_posterized = float(n_colors - 1);
	float posterized = floor((disk+light_d)*n_posterized);
	posterized = min(posterized, n_posterized);
	float4 col = colors[int(posterized)];
	
	// this can be toggled on to achieve a more "realistic" black hole, with red and blue shifting. This was just me messing around so can probably be more optimized and done cleaner
	//col.rgb *= 1.0 - pow(uv.x, 1.0);
	//col.gb *= 1.0 - pow(uv.x, 2.0);
	//col.b *= 3.0 - pow(uv.x, 4.0);
	//col.gb *= 2.0 - pow(uv.x, 2.0);
	//col.rgb *= pow(uv.x, 0.15);
	
	float disk_a = step(0.15, disk);
	return float4(col.rgb, disk_a * col.a);
}

ENDCG } } }