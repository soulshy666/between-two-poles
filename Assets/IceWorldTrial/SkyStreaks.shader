Shader "BetweenPoles/PixelSkyStreaks" {
 SubShader {Tags {"Queue"="Transparent+5" "RenderType"="Transparent"} Cull Off ZWrite Off Blend One One
 Pass {CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 float4 _Heads[3],_Styles[3],_Planet,_Resolution;float _Aspect,_Clock;
 struct v2f {float4 position:SV_POSITION;float4 screen:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.screen=ComputeScreenPos(o.position);return o;}
 float4 frag(v2f i):SV_Target {
  float2 uv=i.screen.xy/i.screen.w;uv=floor(uv*max(_Resolution.xy,1))/max(_Resolution.xy,1);float2 p=uv*float2(_Aspect,1);
  if(_Planet.z>0&&distance(p,_Planet.xy)<_Planet.z+.01)return 0;
  float3 color=0;
  for(int j=0;j<3;j++){
   float4 style=_Styles[j];float2 d=p-_Heads[j].xy,dir=_Heads[j].zw;
   float back=-dot(d,dir),side=dot(d,float2(-dir.y,dir.x));float progress=saturate(back/style.x);
   float bend=style.w*sin(progress*8-_Clock*2)*.007*progress;
   float width=style.y*(1+progress*2.5);
   float mask=step(0,back)*step(back,style.x)*pow(1-progress,1.6);
   float core=saturate(1-abs(side-bend)/width)*mask;
   float haze=saturate(1-abs(side-bend)/(width*2.8))*mask*.23*style.w;
   float3 tail=lerp(float3(.45,.64,1),lerp(float3(.36,.8,1),float3(.35,.16,.65),progress),style.w);
   float nucleus=saturate(1-length(d)/max(style.y*1.6,.0038));
   color+=(tail*(core+haze)+float3(.8,.95,1)*nucleus)*style.z;
  }
  return float4(color,0);
 }
 ENDCG}
 }
}
