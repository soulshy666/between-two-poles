Shader "BetweenPoles/PixelPlanetoid" {
Properties { _Shell("Spherical body",Range(0,1))=0 _Seed("Geology seed",Float)=0 _MainTex("Grid coordinates",2D)="white"{} }
SubShader { Tags {"RenderType"="Opaque"}
CGPROGRAM
#pragma surface surf Lambert
float _Shell,_Seed;
struct Input {float3 worldPos;float3 worldNormal;float3 viewDir;float2 uv_MainTex;};
float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);}
float fbm(float3 p){return noise(p)*.58+noise(p*2.05)*.28+noise(p*4.2)*.14;}
void surf(Input IN,inout SurfaceOutput o){
 float3 p=floor(IN.worldPos*15)/15+float3(_Seed,0,7);
 float warp=fbm(p*.48);
 float land=fbm(p*.85+warp*2.6);
 float bands=floor(saturate((land-.25)*1.8)*5)/5;
 float veins=step(.50,land)*step(land,.523);
 float fleck=step(.78,noise(p*3.2));
 float3 dark=float3(.035,.035,.12),blue=float3(.105,.16,.34),pale=float3(.24,.34,.48);
 float3 crust=lerp(dark,blue,saturate(bands*1.7));
 crust=lerp(crust,pale,step(.6,bands)*.7);
 crust+=veins*float3(.045,.095,.13)+fleck*.025;
 float up=saturate(IN.worldNormal.y);
 float topMask=(1-_Shell)*smoothstep(.55,.92,up);
 float3 surface=lerp(float3(.23,.25,.34),float3(.32,.34,.43),bands);
 surface+=veins*.022;
 float2 grid=abs(IN.uv_MainTex-.5);
 float seam=step(.49,max(grid.x,grid.y));
 surface*=1-seam*.22;
 float depthShade=lerp(.40,1,saturate((IN.worldPos.y+4.8)/4.8));
 o.Albedo=lerp(crust*depthShade,surface,topMask);
 float rim=pow(1-saturate(dot(normalize(IN.viewDir),float3(0,0,1))),3);
 // A restrained blue limb and contour strata emphasize the sphere, not glowing props.
 o.Emission=crust*.035+float3(.055,.13,.24)*rim*_Shell*.42;
 o.Alpha=1;
}
ENDCG
}Fallback "Diffuse" }
