Shader "BetweenPoles/BlackHolePortal" {
 Properties{_Color("Tint",Color)=(1,1,1,1) _Grid("Grid",Float)=0 _Snow("Snow",Float)=0 _IceArt("Ice art shading",Float)=0 _Painted("Painted ice",Float)=0 _Cracks("Crack strength",Range(0,1))=.3}
 SubShader{Tags{"RenderType"="Transparent" "Queue"="Transparent" "DisableBatching"="True"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 fixed4 _Color;float _Grid,_Snow,_IceArt,_Painted,_Cracks;struct Input{float2 portalUV;float3 worldPos;float3 worldNormal;float2 logicalXZ;float4 iceTint;float2 iceSurface;};
 float4 _IceFocus,_IslandAnchors[16],_ExplicitIsland;int _IslandCount;float _IceRadius,_IslandFlatten;float3 _IslandViewOffset;float _IceDiskRadius;float3 _IceDiskCenter,_IceDiskRight,_IceDiskUp;float4x4 _IceRotation;float _IslandDisplayScale;
 // Connected gameplay uses one common frame; never stretch a bridge between warped shores.
 float _IslandRigidLayout,_RecoilFlying; float3 _IceCurveFocus,_RecoilDisplayOffset;
 // One continuous arc for terrain, bridges and actors: no separate shore offsets.
 float _IslandFlatEnabled;float4 _IslandFlatBounds;
 float3 surfaceFrame(float3 p,out float3 axis,out float a){
  float3 basePoint=_IceCurveFocus;
  if(_IslandFlatEnabled>.5)basePoint=float3(clamp(p.x,_IslandFlatBounds.x,_IslandFlatBounds.z),_IceCurveFocus.y,clamp(p.z,_IslandFlatBounds.y,_IslandFlatBounds.w));
  float3 q=(p-basePoint)*max(1,_IslandDisplayScale);
  float d=length(q.xz);float radius=max(12,_IceRadius*1.65);
  a=min(d/radius,1.15);axis=d>.0001?float3(q.z,0,-q.x)/d:float3(0,0,1);
  float extra=max(0,d-radius*1.15);
  float radial=radius*sin(a)+extra*cos(a);
  float drop=radius*(cos(a)-1)-extra*sin(a);
  float3 up=float3(-axis.z*sin(a),cos(a),axis.x*sin(a));
  return _IceFocus.xyz+(basePoint-_IceFocus.xyz)*max(1,_IslandDisplayScale)+float3(d>.0001?q.x/d*radial:0,drop,d>.0001?q.z/d*radial:0)+up*q.y;
 }
 float3 displayPoint(float3 p){
  if(_IslandRigidLayout<.5 && _RecoilFlying<.5)return _IceFocus.xyz+(p-_IceFocus.xyz)*max(1,_IslandDisplayScale);
  float3 axis;float a;float3 curved=surfaceFrame(p,axis,a);
  return lerp(curved,_IceFocus.xyz+(p-_IceFocus.xyz)*max(1,_IslandDisplayScale)+_RecoilDisplayOffset,step(.5,_RecoilFlying));
 }
 float3 displayNormal(float3 p,float3 n){
  float3 axis;float a;surfaceFrame(p,axis,a);
  return lerp(n*cos(a)+cross(axis,n)*sin(a)+axis*dot(axis,n)*(1-cos(a)),n,step(.5,_RecoilFlying));
 }
 float3 turn(float3 q,float3 axis,float si,float co){return q*co+cross(axis,q)*si+axis*dot(axis,q)*(1-co);}
 float _BridgeEnabled;float4 _BridgeStart,_BridgeEnd,_BridgeIslandA,_BridgeIslandB;
 float3 _BridgeOffsetA,_BridgeOffsetB;
 // Same spherical rotation as CurvedIceTrial, rebased to the original composition for every island.
 float3 referencePoint(){return float3(0,0,0);}
 float3 originalFrame(float3 v){return v;}
 float upperWeight(float3 delta){return smoothstep(0,1,saturate((delta.z-abs(delta.x))/6));}
 float _IslandEdgeLayout;
 float3 islandOffset(float3 anchor){
  float3 delta=anchor-_IceFocus.xyz;
  float3 offset=_IceDiskUp*(.45*upperWeight(delta)+.25*upperWeight(-delta));
  float d=length(delta.xz);
  if(_IslandEdgeLayout>.001 && _IceDiskRadius>0 && d>.001){
   float angle=d/_IceRadius;
   float3 center=float3(delta.x/d*sin(angle)*_IceRadius,(cos(angle)-1)*_IceRadius,delta.z/d*sin(angle)*_IceRadius)+offset;
   float2 screen=float2(dot(center,_IceDiskRight),dot(center,_IceDiskUp));
   float extent=length(screen);
   float push=max(0,_IceDiskRadius*.96/max(1,_IslandDisplayScale)-extent)*smoothstep(0,6,d)*_IslandEdgeLayout;
   offset+=(_IceDiskRight*screen.x+_IceDiskUp*screen.y)/max(.001,extent)*push;
  }
  return offset;
 }
 float3 bridgeWarp(float3 p,float4 anchor,float3 offset){float3 relative=anchor.xyz-_IceFocus.xyz+referencePoint();float d=length(relative.xz);float angle=d/_IceRadius;float localAngle=angle*(1-.10*upperWeight(anchor.xyz-_IceFocus.xyz));float3 axis=d>.001?float3(relative.z,0,-relative.x)/d:float3(0,0,1);return _IceFocus.xyz+float3(0,-_IceRadius,0)+originalFrame(turn(float3(0,_IceRadius,0),axis,sin(angle),cos(angle))+turn(p-anchor.xyz,axis,sin(localAngle),cos(localAngle)))+islandOffset(anchor.xyz);}
 float3 bridgeNormal(float3 n,float4 anchor){float3 relative=anchor.xyz-_IceFocus.xyz+referencePoint();float d=length(relative.xz);float a=d/_IceRadius*(1-.10*upperWeight(anchor.xyz-_IceFocus.xyz));float3 axis=d>.001?float3(relative.z,0,-relative.x)/d:float3(0,0,1);return originalFrame(turn(n,axis,sin(a),cos(a)));}
 void bend(inout appdata_full v,out Input o){
 UNITY_INITIALIZE_OUTPUT(Input,o);o.iceTint=v.color;o.portalUV=v.texcoord.xy;
 float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;o.logicalXZ=p.xz;o.iceSurface=float2(v.normal.y,p.y);

 if(_IslandRigidLayout>.5 || _RecoilFlying>.5){
  v.normal=mul((float3x3)unity_WorldToObject,displayNormal(p,UnityObjectToWorldNormal(v.normal)));
  v.vertex=mul(unity_WorldToObject,float4(displayPoint(p),1));
  return;
 }
 if(_BridgeEnabled>.5){
  // A single straight span between the two shoreline edges, not a curved blend.
  float3 direction=normalize(_BridgeEnd.xyz-_BridgeStart.xyz);float3 side=cross(direction,float3(0,1,0));
  float lengthLogical=max(length(_BridgeEnd.xyz-_BridgeStart.xyz),.001);
  float t=dot(p-_BridgeStart.xyz,direction)/lengthLogical;
  float3 a=bridgeWarp(_BridgeStart.xyz,_BridgeIslandA,_BridgeOffsetA),b=bridgeWarp(_BridgeEnd.xyz,_BridgeIslandB,_BridgeOffsetB);
  if(abs(direction.x)>.5){a.z=b.z=(a.z+b.z)*.5;}else{a.x=b.x=(a.x+b.x)*.5;}
  float3 forward=normalize(b-a);
  float3 up=float3(0,1,0);
  float3 across=normalize(cross(forward,up));up=normalize(cross(across,forward));
  float3 wp=lerp(a,b,t)+across*dot(p-_BridgeStart.xyz,side)+up*p.y;
  float3 n=UnityObjectToWorldNormal(v.normal);float3 wn=forward*dot(n,direction)+across*dot(n,side)+up*n.y;
  v.vertex=mul(unity_WorldToObject,float4(displayPoint(wp),1));v.normal=mul((float3x3)unity_WorldToObject,wn);return;
 }
 float3 anchor=_ExplicitIsland.w>.5?_ExplicitIsland.xyz:_IceFocus.xyz;
 float3 result=bridgeWarp(p,float4(anchor,0),float3(0,0,0));
 v.vertex=mul(unity_WorldToObject,float4(displayPoint(result),1));
 float3 normal=bridgeNormal(UnityObjectToWorldNormal(v.normal),float4(anchor,0));
 v.normal=mul((float3x3)unity_WorldToObject,normal);
 }


 struct v2f {float4 pos:SV_POSITION;float2 portalUV:TEXCOORD0;};
 v2f vert(appdata_full v){Input data;bend(v,data);v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.portalUV=v.texcoord.xy;return o;}
 fixed4 frag(v2f i):SV_Target {
 float2 p=(floor(i.portalUV*48)+.5)/48*2-1;
 float r=length(p);clip(.98-r);
 float angle=atan2(p.y,p.x);float arms=sin(angle*3+r*19-_Time.y*1.6);
 float dust=frac(sin(dot(floor(p*48),float2(12.9898,78.233)))*43758.5453);
 float glow=pow(saturate(arms*.5+.5),4)*smoothstep(.16,.3,r)*(1-smoothstep(.65,.98,r));
 float rim=exp(-abs(r-.25)*32);
 float3 col=lerp(float3(.025,.009,.055),float3(.37,.12,.62),floor(glow*5)/4);
 col+=float3(.30,.17,.43)*rim+step(.97,dust)*glow*.4;
 col=lerp(float3(.009,.003,.02),col,smoothstep(.17,.24,r));
 return fixed4(col,1-smoothstep(.82,.98,r));
 }
 ENDCG} } FallBack Off }
