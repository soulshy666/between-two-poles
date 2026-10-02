Shader "BetweenPoles/PlanetRock" {
Properties { _Color("Tint",Color)=(.5,.5,.6,1) _MainTex("Cell coordinates",2D)="white"{} }
SubShader { Tags {"RenderType"="Opaque"}
CGPROGRAM
#pragma surface surf Lambert
fixed4 _Color;
struct Input {float4 color:COLOR;float3 worldPos;float2 uv_MainTex;};
float h(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(h(i),h(i+float2(1,0)),f.x),lerp(h(i+float2(0,1)),h(i+1),f.x),f.y);}
void surf(Input IN,inout SurfaceOutput o){
 float grain=floor(h(floor(IN.worldPos.xz*19))*4)/4;
 float sediment=floor(noise(IN.worldPos.xz*.85+17)*5)/5;
 float3 stone=lerp(float3(.72,.78,.89),float3(1.09,1.03,.95),sediment);
 float2 grid=abs(IN.uv_MainTex-.5);
 float seam=step(.49,max(grid.x,grid.y))*step(-.3,IN.worldPos.y);
 float2 rockPixel=floor(IN.worldPos.xz*16);
 float fleck=step(.985,h(floor(rockPixel/2)))*step(.35,h(rockPixel));
 float hairline=step(.496,noise(IN.worldPos.xz*2.3+8))*step(noise(IN.worldPos.xz*2.3+8),.505)*step(.64,noise(IN.worldPos.xz*.7+3));
 float detail=1-fleck*.065-hairline*.04;
 o.Albedo=_Color.rgb*IN.color.rgb*stone*(.92+grain*.12)*(1-seam*.18)*detail;o.Alpha=1;
}
ENDCG
}Fallback "Diffuse" }
