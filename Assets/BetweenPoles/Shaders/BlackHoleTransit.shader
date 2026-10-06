Shader "BetweenPoles/BlackHoleTransit" {
 Properties { _MainTex("Captured view",2D)="black"{} _Progress("Suction progress",Range(0,1))=0 _Center("Black hole center",Vector)=(.5,.5,0,0) _Aspect("Aspect",Float)=1.777 }
 SubShader { Tags {"Queue"="Overlay" "RenderType"="Transparent"} Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float _Progress,_Aspect,_FlipY;float4 _Center;
   struct input{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct output{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   output vert(input v){output o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   float3 readFrame(float2 uv){uv=1-abs(frac(uv*.5)*2-1);uv.y=lerp(uv.y,1-uv.y,_FlipY);return tex2D(_MainTex,uv).rgb;}
   float2 rot(float2 q,float a){float s=sin(a),c=cos(a);return float2(c*q.x-s*q.y,s*q.x+c*q.y);}
   fixed4 frag(output i):SV_Target{
    // The transition coroutine already eases progress. Every displacement must be zero at p=0.
    float p=saturate(_Progress);
    float2 q=i.uv-_Center.xy;q.x*=_Aspect;float r=length(q);
    // Start with a subtle local turn, then draw a wider region into one vortex.
    float reach=lerp(.18,1.1,p);
    float nearCore=1-smoothstep(.02,reach,r);
    float pull=p*p*(.25+1.45*nearCore);
    float angle=p*(.12+3.2*p)*nearCore*nearCore;
    float2 source=rot(q,angle);
    source*=1+pull*(.35+1.9*nearCore);
    float2 uv=_Center.xy+source/float2(_Aspect,1);
    float3 scene=0;
    // Radial streaking makes the suction read as motion instead of a simple scale.
    for(int k=0;k<8;k++){
      float t=k/7.0;float2 streak=lerp(uv,_Center.xy,t*p*p*.54*nearCore);scene+=readFrame(streak)/8;
    }
    float blackRadius=p*p*.68;
    float blackCore=1-smoothstep(blackRadius,blackRadius+.12,r);
    scene*=1-blackCore*p;
    return fixed4(scene,1);
   }
  ENDCG }
 } FallBack Off
}
