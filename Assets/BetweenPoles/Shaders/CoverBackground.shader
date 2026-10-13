Shader "BetweenPoles/CoverBackground" {
Properties{_MainTex("Background",2D)="white"{} _MapTex("Solar mask",2D)="black"{}}
SubShader{Cull Off ZWrite Off ZTest Always Pass{CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex,_MapTex;float _CoverTime;float4 _Parallax;
fixed4 frag(v2f_img i):SV_Target{
 float2 uv=i.uv+float2(_Parallax.x,-_Parallax.y);
 float3 color=tex2D(_MainTex,uv).rgb;
 float mask=tex2D(_MapTex,uv).r;
 float2 p=float2(uv.x,1-uv.y)*float2(1376,768);
 float2 delta=p-float2(-188.4,2496.2);
 float radius=length(delta)-2281.6;
 float phase=frac(_CoverTime*.03),other=frac(_CoverTime*.03+.5);
 float weight=1-abs(phase*2-1);
 float2 shift=float2(.007, .003);
 float2 a=uv+shift*(phase-.5),b=uv+shift*(other-.5);
 float wa=weight*tex2D(_MapTex,a).r,wb=(1-weight)*tex2D(_MapTex,b).r;
 float3 surface=(tex2D(_MainTex,a).rgb*wa+tex2D(_MainTex,b).rgb*wb)/max(.001,wa+wb);
 color=lerp(color,surface,mask*(1-smoothstep(-70,-12,radius)));
 float pulse=.86+.1*sin(_CoverTime*1.1)+.04*sin(_CoverTime*3.3+1);
 color+=float3(1,.46,.12)*exp(-abs(radius)/35)*.08*pulse;
 float leftShade=.28*(1-smoothstep(0,.45,uv.x));
 float radialShade=.42*(1-smoothstep(0,.7,length((float2(uv.x,1-uv.y)-float2(.08,.62))/float2(.6,.75))));
 color*=(1-leftShade)*(1-radialShade);
 return fixed4(color,1);
}
ENDCG}}}
