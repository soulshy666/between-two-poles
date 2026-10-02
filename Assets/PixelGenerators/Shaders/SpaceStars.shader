Shader "PixelGenerators/SpaceStars" {
Properties { _MainTex("星点图集",2D)="white"{} }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex, colorscheme;
float clockTime, density, twinkle, seed, pixels, clear_edges, edge_clear_margin;
float2 uv_correct,starGrid;
float starFrames,starScale;
struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
v2f vert(appdata_base v){ v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o; }
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7))+seed)*43758.5453);}
float4 frag(v2f i):SV_Target {
 float2 uv=floor(i.uv*pixels)/pixels;
 float2 grid=uv*starGrid; float2 cell=floor(grid);float h=hash(cell);
 if(h>density) return 0;
 float2 offset=float2(hash(cell+17),hash(cell+39));
 float2 q=(frac(grid)-(.25+offset*.5))*starScale+.5;
 if(any(q<0)||any(q>1)) return 0;
 float frame=floor(hash(cell+61)*starFrames);
 float4 tex=tex2D(_MainTex,float2((q.x+frame)/starFrames,q.y));
 float phase=clockTime*(4.4+hash(cell+92)*3.0)+h*123;
 float alpha=lerp(1,.2+.8*pow(.5+.5*sin(phase),3),twinkle);
 float3 col=tex2D(colorscheme,float2(.55+tex.r*.45,0)).rgb;
 float edge=min(min(i.uv.x,1-i.uv.x),min(i.uv.y,1-i.uv.y));
 return float4(col,tex.a*alpha*(clear_edges>.5?smoothstep(edge_clear_margin,edge_clear_margin+.03,edge):1));
}
ENDCG } } }
