Shader "Hidden/BetweenPoles/CrashColorReveal" {
 Properties { _MainTex("Scene",2D)="white"{} _Progress("Reveal",Range(0,1))=0 _Center("Impact center",Vector)=(.5,.5,0,0) _Age("Wave age",Float)=-1 }
 SubShader { Cull Off ZWrite Off ZTest Always
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize,_Center;float _Progress,_Age,_Birth,_Hold,_Expand;
   float hash(float n){return frac(sin(n*127.1+31.7)*43758.5453);}
   fixed4 frag(v2f_img i):SV_Target {
    fixed4 scene=tex2D(_MainTex,i.uv);
    float gray=saturate((dot(scene.rgb,float3(.2126,.7152,.0722))-.5)*1.06+.5);
    if(_Age<0)return fixed4(gray,gray,gray,scene.a);
    float aspect=_MainTex_TexelSize.z/_MainTex_TexelSize.w;
    float2 scale=float2(aspect,1),delta=(i.uv-_Center.xy)*scale;
    float furthest=length(max(abs(_Center.xy),abs(1-_Center.xy))*scale)+.025;
    float progress=smoothstep(0,1,_Progress);
    float smallRadius=.075;
    float radius=_Age<_Birth?smallRadius*smoothstep(0,_Birth,_Age):lerp(smallRadius,furthest,progress);
    float distance=length(delta),pixel=1/_MainTex_TexelSize.w;
    float aa=pixel*.7;
    float colorMask=_Progress>=1?1:1-smoothstep(radius-aa,radius+aa,distance);
    float3 color=lerp(gray.xxx,scene.rgb,colorMask);
    float ringFade=1-smoothstep(.93,1,_Progress);
    float outline=(1-smoothstep(pixel*3.0,pixel*4.0,abs(distance-radius)))*ringFade;
    color=lerp(color,float3(.055,.075,.085),outline*.75);
    float white=(1-smoothstep(pixel*1.5,pixel*2.2,abs(distance-radius)))*ringFade;
    color=lerp(color,float3(.97,.99,1),white);
    float ripple=(1-smoothstep(pixel*.5,pixel*1.2,abs(distance-(radius-.035))))*.28*ringFade*smoothstep(0,.15,_Progress);
    color=lerp(color,float3(.75,.91,1),ripple);
    // Sparse deterministic sparkles born along the moving front; no scrolling noise overlay.
    float sectors=72,theta=atan2(delta.y,delta.x),sector=floor((theta+UNITY_PI)/(2*UNITY_PI)*sectors);
    float sparks=0;
    [unroll] for(int k=-1;k<=1;k++){
     float id=fmod(sector+k+sectors,sectors),seed=hash(id+7);
     float birth=_Birth+_Hold+seed*(_Expand-.35);
     float age=_Age-birth,life=1.05;
     float bornProgress=saturate((birth-_Birth-_Hold)/_Expand);
     float r0=lerp(smallRadius,furthest,smoothstep(0,1,bornProgress));
     float r=r0+age*(.06+.07*hash(id+20));
     float angle=(id+.25+.5*hash(id+90))/sectors*2*UNITY_PI-UNITY_PI;
     float2 at=float2(cos(angle),sin(angle))*r;
     float size=pixel*(1.1+1.2*hash(id+43));
     float2 q=abs(delta-at);
     float diamond=1-smoothstep(size*.6,size*1.3,q.x+q.y);
     float fade=smoothstep(0,.08,age)*(1-smoothstep(.45,life,age));
     sparks=max(sparks,diamond*fade*step(0,age)*step(age,life));
    }
    color=lerp(color,float3(1,.99,.91),sparks*.95);
    return fixed4(color,scene.a);
   }
   ENDCG }
 }
 Fallback Off
}
