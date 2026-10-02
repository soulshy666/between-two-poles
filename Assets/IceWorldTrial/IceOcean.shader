Shader "BetweenPoles/IceTrialOcean" {
 Properties { _Clock("Time",Float)=0 }
 SubShader {Tags {"RenderType"="Opaque"} Pass {Cull Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Clock;struct v {float4 p:SV_POSITION;float3 w:TEXCOORD0;};
 v vert(appdata_base a){v o;o.p=UnityObjectToClipPos(a.vertex);o.w=mul(unity_ObjectToWorld,a.vertex).xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.54);}
 float4 frag(v i):SV_Target{
 float2 p=floor(i.w.xz*22)/22;float t=_Clock*.18;
 float wave=sin(p.y*5+sin(p.x*.65+t)*1.4+t)+sin(p.y*10-p.x*.35-t)*.25;
 float ripple=smoothstep(.92,1.18,wave)*.055;
 float far=smoothstep(-5,10,i.w.z);
 float3 col=lerp(float3(.028,.12,.20),float3(.24,.43,.54),far);
 col+=ripple*float3(.5,.9,1);col+=step(.994,hash(floor(p*float2(1.5,6))+floor(t)))*.07;
 return float4(col,1);
 }ENDCG
 }} }
