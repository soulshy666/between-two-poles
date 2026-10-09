Shader "BetweenPoles/VerdantPlanetSurface" {
 SubShader { Tags {"RenderType"="Opaque" "Queue"="Geometry-100"} ZWrite Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 struct v2f {float4 position:SV_POSITION;float3 local:TEXCOORD0;float3 normal:TEXCOORD1;float3 world:TEXCOORD2;};
 v2f vert(appdata_base v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xyz;o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
 float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
 float noise(float3 p){
  float3 k=floor(p),f=frac(p);f=f*f*(3-2*f);
  float z0=lerp(lerp(hash(k),hash(k+float3(1,0,0)),f.x),lerp(hash(k+float3(0,1,0)),hash(k+float3(1,1,0)),f.x),f.y);
  float z1=lerp(lerp(hash(k+float3(0,0,1)),hash(k+float3(1,0,1)),f.x),lerp(hash(k+float3(0,1,1)),hash(k+float3(1,1,1)),f.x),f.y);
  return lerp(z0,z1,f.z);
 }
 fixed4 frag(v2f i):SV_Target {
  // Sample in 3D: continents remain attached to the sphere, with no polar UV pinching.
  float3 p=(floor(normalize(i.local)*48)+.5)/48;
  // Broken, elongated foliage patches, with a small fixed pixel palette.
  float3 warp=float3(noise(p*5+21),noise(p*5+47),noise(p*5+83))-.5;
  float3 terrain=p*float3(7,10,7)+warp*1.2;
  float n=noise(terrain+17)*.76+noise(terrain*2.1+7)*.24;
  float3 col=n>.65?float3(.76,.85,.35):n>.58?float3(.59,.73,.27):n>.52?float3(.36,.56,.22):n>.45?float3(.19,.39,.22):n>.37?float3(.09,.28,.20):float3(.055,.19,.17);
  float illumination=dot(normalize(i.normal),normalize(float3(-.6,.65,-.55)));
  float shade=illumination>.65?1:illumination>.25?.86:illumination>-.1?.65:.43;
  col*=shade;
  // Palette values are authored in sRGB; avoid washed-out greens in linear projects.
  #ifndef UNITY_COLORSPACE_GAMMA
  col=GammaToLinearSpace(col);
  #endif
  return float4(col,1);
 }
 ENDCG }
 }
}
