Shader "BetweenPoles/BlackHoleTransit" {
 Properties { _MainTex("Captured view",2D)="black"{} _Progress("Progress",Range(0,1))=0 _Center("Center",Vector)=(.5,.5,0,0) _Aspect("Aspect",Float)=1.777 }
 SubShader { Tags {"Queue"="Overlay" "RenderType"="Transparent"} Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float _Progress,_Aspect,_FlipY;float4 _Center;
 struct input {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 struct output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
 output vert(input v){output o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
 float3 readFrame(float2 uv){uv=1-abs(frac(uv*.5)*2-1);uv.y=lerp(uv.y,1-uv.y,_FlipY);return tex2D(_MainTex,uv).rgb;}
 // Scene-color refraction driven by twirled low-frequency noise.
 float hash21(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise2(float2 p){float2 k=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash21(k),hash21(k+float2(1,0)),f.x),lerp(hash21(k+float2(0,1)),hash21(k+1),f.x),f.y);}
 float2 rotate2(float2 q,float angle){float s=sin(angle),c=cos(angle);return float2(c*q.x-s*q.y,s*q.x+c*q.y);}
 fixed4 frag(output i):SV_Target {
  float p=_Progress;float strength=smoothstep(0,.95,p);
  float2 pivot=lerp(_Center.xy,float2(.5,.5),smoothstep(0,.8,p));
  float2 q=i.uv-pivot;q.x*=_Aspect;float radius=length(q);
  float reach=lerp(.12,1.65,strength);
  float field=1-smoothstep(reach*.30,reach,radius);
  // Twist the noise domain, not the entire image into a uniform spiral.
  float2 domain=rotate2(q,field*strength*3.2+p*1.2)*3.4*exp(p*.25);
  float2 flow=float2(noise2(domain+float2(p*.8,-p*.5)),noise2(domain+float2(17.3-p*.6,8.2+p*.7)))*2-1;
  float2 radial=q/max(radius,.001);float2 tangent=float2(-radial.y,radial.x);
  float core=smoothstep(0,.12,radius);
  float lens=field*strength;
  float2 bent=q*(1+lens*1.5);
  bent+=core*lens*(flow*.28+radial*flow.x*.15+tangent*flow.y*.16);
  float2 center=_Center.xy;
  float2 sampleUV=center+bent/float2(_Aspect,1);
  float3 scene=0;
  // Warp and radial blur only: preserve the existing scene colors.
  for(int k=0;k<7;k++){float2 uv=lerp(sampleUV,center,(k/6.0)*p*.20);scene+=readFrame(uv)/7;}
  return fixed4(scene,i.color.a);
 }
 ENDCG } } FallBack Off
}
