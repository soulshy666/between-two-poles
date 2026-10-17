Shader "Hidden/BetweenPoles/CoverTitleSignal" {
Properties {_MainTex("Title",2D)="white"{}}
SubShader {Cull Off ZWrite Off ZTest Always Pass {
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;
float _SignalTime;
fixed4 sampleTitle(float2 uv){
 if(any(uv<0)||any(uv>1))return 0;
 return tex2D(_MainTex,uv);
}
fixed4 frag(v2f_img i):SV_Target{
 float t=_SignalTime,phase=fmod(t,3.1);
 float pulse=phase<.48?sin(phase/.48*3.14159265):0;
 float row=floor(i.uv.y*44),tick=floor(t*18);
 float wave=sin(row*1.73+tick*2.1);
 float amount=pulse*step(.5,abs(wave));
 float stretch=1+amount*wave*.065;
 float2 uv=i.uv;
 uv.x=(uv.x-.5)/stretch+.5-amount*wave*.015;
 fixed4 base=sampleTitle(uv);
 // Colour only the upper pixel strokes of the two poles; retain the ivory wordmark.
 float3 pole=uv.x<.26?float3(.96,.28,.25):float3(.20,.68,.85);
 float accent=step(.73,uv.y)*(1-step(.49,uv.x));
 base.rgb=lerp(base.rgb,pole,accent*.88);
 float separation=.0015+amount*.004;
 float red=sampleTitle(uv+float2(separation,0)).a;
 float blue=sampleTitle(uv-float2(separation,0)).a;
 float echo=max(red,blue)*.4*(1-base.a);
 float alpha=base.a+echo;
 float3 rgb=base.rgb*base.a+float3(red,.6*blue,blue)*.4*(1-base.a);
 return float4(rgb/max(alpha,.0001),alpha);
}
ENDCG
}}}
