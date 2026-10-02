Shader "BetweenPoles/FrozenPlanetTrial" {
 Properties{_Color("Tint",Color)=(1,1,1,1)}
 SubShader{Tags{"RenderType"="Opaque"} CGPROGRAM
 #pragma surface surf Lambert
 #include "UnityCG.cginc"
 struct Input{float3 worldPos;};
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.54);}
 float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);}
 void surf(Input i,inout SurfaceOutput o){float3 p=floor(mul(unity_WorldToObject,float4(i.worldPos,1)).xyz*10)/10;float n=noise(p*.3)*.65+noise(p*.7)*.25+noise(p*1.6)*.1;float band=floor(n*8)/8;
 float3 sea=lerp(float3(.11,.32,.43),float3(.31,.60,.68),band);float snow=step(.60,n);float coast=step(.56,n);float3 c=lerp(sea,float3(.46,.74,.80),coast);c=lerp(c,float3(.76,.87,.92),snow);o.Albedo=c;o.Emission=c*.09;o.Alpha=1;}
 ENDCG} FallBack "Diffuse" }
