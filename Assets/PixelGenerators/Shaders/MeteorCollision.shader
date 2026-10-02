Shader "PixelGenerators/MeteorCollision" {
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend One OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#include "UnityCG.cginc"
float _Clock, _Aspect, _EventPeriod, _CollisionScale; float2 _Pixels, _CollisionCenter;
float _CollisionAngle, _HideEvent;
struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
float fbm(float2 p){return noise(p)*.55+noise(p*2.1)*.27+noise(p*4.2)*.12+noise(p*8.4)*.06;}
float3 asteroid(float2 uv,float2 center,float size,float seed,float3 bg,inout float alpha){
 float2 p=(uv-center)*float2(_Aspect,1)/size;
 float turn=_Clock*.055+seed;float cs=cos(turn),sn=sin(turn);
 p=float2(cs*p.x-sn*p.y,sn*p.x+cs*p.y);
 float a=atan2(p.y,p.x),r=length(p);
 float edge=.81+.09*sin(floor((a+3.14159)*1.7)*2.4+seed);
 if(r>edge)return bg;
 float facet=floor((p.x*.7+p.y*.9+noise(floor(p*3)+seed)*.5)*5)/5;
 float pits=noise(floor(p*6)+seed);
 float3 c=lerp(float3(.065,.057,.11),float3(.21,.22,.32),saturate(.45+facet*.55));
 c*=pits<.25?.65:1;
 if(r>edge-.075)c+=float3(.10,.09,.17)*saturate(-p.x+p.y);
 alpha=1; return c;
}
float4 frag(v2f i):SV_Target {
 if(_HideEvent>.5)return 0;
 float2 uv=floor(i.uv*_Pixels)/_Pixels;
 float2 relative=(uv-_CollisionCenter)*float2(_Aspect,1);float cs=cos(_CollisionAngle),sn=sin(_CollisionAngle);
 relative=float2(cs*relative.x+sn*relative.y,-sn*relative.x+cs*relative.y);
 uv=relative/float2(_Aspect,1)/max(.2,_CollisionScale)+float2(.19,.27);
 float3 col=0;float alpha=0;
 float eventTime=fmod(max(0,_Clock),max(8,_EventPeriod));
 float2 impact=float2(.19,.27);
 if(eventTime<3.6){
  float f=eventTime/3.6;
  float2 head=lerp(float2(-.06,.64),impact,f);
  float2 target=lerp(float2(.28,.19),impact,f);
  float2 delta=(uv-head)*float2(_Aspect,1);
  float2 trailDir=normalize(float2(-.25*_Aspect,.37));
  float along=dot(delta,trailDir),side=dot(delta,float2(-trailDir.y,trailDir.x));
  float progress=saturate(along/.38);
  float bend=(sin(along*32-_Clock*4)*.018+sin(along*61-_Clock*6)*.007)*smoothstep(0,.15,along);
  float2 flameUV=float2(along*18-_Clock*2.5,(side-bend)*25);
  float turbulence=fbm(flameUV+float2(fbm(flameUV+3),fbm(flameUV-5))*2);
  float width=(.047+.012*sin(along*37-_Clock*3))*pow(saturate(1-progress),.65);
  float profile=saturate(1-abs(side-bend)/max(.002,width));
  float density=saturate(profile*1.4+(turbulence-.5)*1.3-.18)*step(0,along)*(1-progress)*saturate(2-abs(side-bend)/max(.002,width));
  float heat=floor(saturate(density*1.35-progress*.17)*8)/8;
  float3 fire=lerp(float3(.17,.055,.23),float3(.67,.09,.035),smoothstep(0,.28,heat));
  fire=lerp(fire,float3(1,.37,.055),smoothstep(.25,.60,heat));
  fire=lerp(fire,float3(1,.86,.39),smoothstep(.60,.92,heat));
  col=lerp(col,fire,saturate(density*2)); alpha=max(alpha,saturate(density*2));
  float filament=step(.51,turbulence)*step(turbulence,.56)*density;
  col+=float3(.16,.10,.035)*filament;
  col=asteroid(uv,target,.040,79,col,alpha);
  col=asteroid(uv,head,.048,67,col,alpha);
  col+=float3(.48,.16,.025)*saturate(1-abs(length(delta)-.039)/.007);
 }else if(eventTime<5.8){
  float age=eventTime-3.6;
  float2 d=(uv-impact)*float2(_Aspect,1);
  float flash=saturate(1-age/.22)*saturate(1-length(d)/.09);
  col+=float3(1,.72,.27)*flash;
  float radius=.025+.11*(1-exp(-age*3.6));
  float angleCloud=atan2(d.y,d.x);
  float billow=fbm(d*23+float2(age*.7,-age*1.3));
  float edge=radius*(.83+.27*noise(float2(cos(angleCloud),sin(angleCloud))*3+age*.35));
  float cloudMask=saturate((edge-length(d))/.027+(billow-.5)*.85);
  float cloudFade=saturate((2.2-age)/1.35);
  float heat=floor(saturate(cloudMask*.45+billow*.7-age*.32)*8)/8;
  float3 blast=lerp(float3(.14,.065,.19),float3(.61,.10,.035),smoothstep(.05,.35,heat));
  blast=lerp(blast,float3(1,.46,.09),smoothstep(.3,.65,heat));
  blast=lerp(blast,float3(1,.91,.57),smoothstep(.65,.95,heat));
  col=lerp(col,blast,cloudMask*cloudFade); alpha=max(alpha,cloudMask*cloudFade);
  float shockRadius=.035+age*.20;
  float shock=saturate(1-abs(length(d)-shockRadius)/.006)*saturate(1-age/.85);
  shock*=.35+.65*noise(d*70+age);
  col+=float3(.47,.27,.15)*shock;
  for(int s=0;s<18;s++){
   float angle=s*2.39996;
   float2 direction=float2(cos(angle),sin(angle));
   float speed=.06+hash(float2(s,19))*.12;
   float2 spark=direction*age*speed;
   spark.y-=age*age*.012;
   float2 dist=d-spark;
   float streak=length(float2(dot(dist,direction)*.4,dot(dist,float2(-direction.y,direction.x))));
   float life=1.0+hash(float2(s,23))*.9;
   float intensity=saturate(1-streak/.004)*saturate(1-age/life);
   col+=lerp(float3(1,.16,.035),float3(1,.81,.32),saturate(1-age/life))*intensity;
  }
  for(int b=0;b<3;b++){
   float2 offset=float2(cos(b*2.1+.4),sin(b*2.1+.4))*age*.075;
   float debrisAlpha=0; float3 debris=asteroid(uv,impact+offset,.019-b*.003,82+b,col,debrisAlpha); alpha=max(alpha,debrisAlpha*saturate((2.2-age)*2));
   col=lerp(col,debris,saturate((2.2-age)*2));
  }
 }
alpha=max(alpha,saturate(max(col.r,max(col.g,col.b))));
 return float4(col,alpha);
}
ENDCG } } }
