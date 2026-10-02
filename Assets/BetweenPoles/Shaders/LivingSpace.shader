Shader "BetweenPoles/LivingSpace" {
Properties { _Clock("Time",Float)=0 }
SubShader { Tags {"Queue"="Background" "RenderType"="Opaque"} Cull Off ZWrite Off
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.0
#include "UnityCG.cginc"
float _Clock;
struct v2f {float4 pos:SV_POSITION;float4 screen:TEXCOORD0;};
v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.screen=ComputeScreenPos(o.pos);return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
float fbm(float2 p){return noise(p)*.55+noise(p*2.1)*.27+noise(p*4.2)*.12+noise(p*8.4)*.06;}
float3 planet(float2 uv,float2 center,float r,float3 tint,float3 bg){
 float2 p=(uv-center)*float2(1.7778,1)/r;float rr=dot(p,p);
 if(rr>1)return bg;
 float3 n=float3(p,sqrt(1-rr));float light=saturate(dot(n,normalize(float3(-.75,.6,.3))));
 float bands=floor(fbm(p*6+float2(_Clock*.007,0))*5)/5;
 float rim=pow(1-n.z,3)*saturate(dot(n.xy,normalize(float2(-1,1))));
 return tint*(.12+floor(light*5)/5*(.45+bands*.8))+tint*rim*.8;
}
float3 asteroid(float2 uv,float2 center,float size,float seed,float3 bg){
 float2 p=(uv-center)*float2(1.7778,1)/size;
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
 return c;
}
fixed4 frag(v2f i):SV_Target {
 float2 uv=i.screen.xy/i.screen.w;uv=floor(uv*float2(683,384))/float2(683,384);
 float t=_Clock*.012;float2 p=uv*float2(5.8,3.7);
 float2 warp=float2(fbm(p+float2(t,0)),fbm(p+float2(7,-t)));
 float cloud=fbm(p*1.7+warp*3+float2(t*.3,-t*.4));
 float band=exp(-pow((uv.y-(.20+uv.x*.63+sin(uv.x*7+t)*.13))/.22,2));
 float v=floor(saturate((cloud-.28)*2.3)*7)/7*band;
 float vein=pow(saturate(1-abs(cloud-.54)*19),3)*band;
 float hue=fbm(p*.7+4);
 float3 col=float3(.018,.022,.065)+lerp(float3(.04,.19,.37),float3(.27,.035,.36),hue)*v;
 col+=lerp(float3(.045,.20,.30),float3(.18,.045,.22),hue)*vein*.55;
 float wisps=fbm(p*2.8+warp*4-float2(t*.16,t*.3));
 float ribbon=pow(saturate(1-abs(wisps-.53)*13),3);
 float pocket=saturate(noise(p*.85+11)-.32);
 col+=float3(.035,.11,.19)*floor(ribbon*5)/5*pocket;
 col+=float3(.095,.025,.13)*floor(saturate((wisps-.43)*3)*5)/5*(1-band)*.6;
 float2 star=floor(uv*float2(380,214));float h=hash(star);
 if(h>.9993)col+=float3(.20,.30,.43)*(.5+.3*sin(_Clock*.9+h*170));
 // Sparse, blue-white pixel crosses. Each star has its own twinkle phase.
 float2 starCell=floor(uv*float2(28,16));float sh=hash(starCell+47);
 float2 starCenter=(starCell+.25+float2(hash(starCell+9),hash(starCell+21))*.5)/float2(28,16);
 float2 sd=abs((uv-starCenter)*float2(683,384));
 if(sh>.972){
  float tw=.12+.88*pow(.5+.5*sin(_Clock*(3.8+sh*1.8)+sh*237),2);
  float cross=max(step(sd.x,.65)*saturate(1-sd.y/3.8),step(sd.y,.65)*saturate(1-sd.x/3.0));
  float halo=saturate(1-length(sd)/3)*.12;
  col+=float3(.35,.68,1)*(cross+halo)*tw;
 }
 // The far half of the ring is occluded by the planet; the near half crosses it.
 float2 rp=(uv-float2(.83,.885))*float2(1.7778,1);
 float2 rq=float2(rp.x*.94+rp.y*.342,-rp.x*.342+rp.y*.94);
 float ringR=length(rq/float2(.105,.024));
 float ring=step(.68,ringR)*step(ringR,1);
 float3 ringColor=lerp(float3(.16,.15,.30),float3(.35,.30,.48),step(.84,ringR));
 col=lerp(col,ringColor,ring*.8);
 col=planet(uv,float2(.83,.885),.050,float3(.40,.19,.49),col);
 col=lerp(col,ringColor,ring*step(rq.y,0)*.85);
 // Several depths and sizes: leave most of the sky empty to preserve scale.
 for(int k=0;k<11;k++){
  float seed=k*13.17+3;
  float2 center=float2(.08+hash(float2(seed,2))*.88,hash(float2(seed,9)));
  center+=float2(sin(_Clock*.19+seed)*.012,cos(_Clock*.26+seed)*.015);
  float size=lerp(.009,.035,hash(float2(seed,5)));
  col=asteroid(uv,center,size,seed,col);
 }
 col=asteroid(uv,float2(.18,.63)+float2(sin(_Clock*.20)*.012,cos(_Clock*.28)*.017),.035,17,col);
 col=asteroid(uv,float2(.65,.075)+float2(sin(_Clock*.22)*.008,sin(_Clock*.30)*.014),.028,31,col);
 col=asteroid(uv,float2(.95,.89)+float2(sin(_Clock*.18+4)*.009,cos(_Clock*.26)*.012),.022,44,col);
 col=asteroid(uv,float2(.22,.16)+float2(sin(_Clock*.17+7)*.013,cos(_Clock*.24+2)*.018),.039,53,col);
 // Brief meteor passages, with a long quiet interval between them.
 for(int m=0;m<2;m++){
  float phase=frac((_Clock+m*9)/19);
  if(phase<.105){
   float travel=phase/.105;
   float2 head=float2(.46+m*.28,.96)-float2(.28,.18)*travel;
   float2 delta=(uv-head)*float2(1.7778,1);
   float2 direction=normalize(float2(.28*1.7778,.18));
   float along=dot(delta,direction),across=abs(dot(delta,float2(-direction.y,direction.x)));
   float tail=step(0,along)*saturate(1-along/.12)*step(across,.0018);
   float glint=saturate(1-length(delta)/.005);
   col+=float3(.32,.46,.65)*(tail+glint)*sin(travel*3.14159);
  }
 }
 // A lightweight, authored 28-second collision vignette in the left sky.
 // 0-3.6s approach, then collision and debris, followed by a quiet interval.
 float eventTime=fmod(_Clock,28);
 float2 impact=float2(.19,.27);
 if(eventTime<3.6){
  float f=eventTime/3.6;
  float2 head=lerp(float2(-.06,.64),impact,f);
  float2 target=lerp(float2(.28,.19),impact,f);
  float2 delta=(uv-head)*float2(1.7778,1);
  float2 trailDir=normalize(float2(-.25*1.7778,.37));
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
  col=lerp(col,fire,saturate(density*2));
  float filament=step(.51,turbulence)*step(turbulence,.56)*density;
  col+=float3(.16,.10,.035)*filament;
  col=asteroid(uv,target,.040,79,col);
  col=asteroid(uv,head,.048,67,col);
  col+=float3(.48,.16,.025)*saturate(1-abs(length(delta)-.039)/.007);
 }else if(eventTime<5.8){
  float age=eventTime-3.6;
  float2 d=(uv-impact)*float2(1.7778,1);
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
  col=lerp(col,blast,cloudMask*cloudFade);
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
   float3 debris=asteroid(uv,impact+offset,.019-b*.003,82+b,col);
   col=lerp(col,debris,saturate((2.2-age)*2));
  }
 }
 return fixed4(col,1);
}
ENDCG } } }
