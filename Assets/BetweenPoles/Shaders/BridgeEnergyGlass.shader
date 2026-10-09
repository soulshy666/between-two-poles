Shader "BetweenPoles/BridgeEnergyGlass" {
 Properties {
  _Color("Glass color",Color)=(.12,.58,1,.30)
  _Smoothness("Smoothness",Range(0,1))=.92
  _EdgeGlow("Edge glow",Range(0,1))=.28
 }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True"}
  Cull Back ZWrite Off
  CGPROGRAM
  #pragma surface surf Standard alpha:fade vertex:bend
  #pragma target 3.0
  #include "UnityCG.cginc"

  fixed4 _Color;
  half _Smoothness,_EdgeGlow;
  struct Input {float3 worldPos;float3 worldNormal;float3 viewDir;};

  float4 _IceFocus,_ExplicitIsland,_BridgeStart,_BridgeEnd,_BridgeIslandA,_BridgeIslandB;
  float _IceRadius,_IslandDisplayScale,_BridgeEnabled;
  float3 _IceDiskUp,_IceDiskRight,_IceDiskCenter,_BridgeOffsetA,_BridgeOffsetB;float _IceDiskRadius;

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
  float3 bridgeWarp(float3 p,float4 anchor){
   float3 relative=anchor.xyz-_IceFocus.xyz+referencePoint();float d=length(relative.xz),angle=d/_IceRadius;
   float localAngle=angle*(1-.10*upperWeight(anchor.xyz-_IceFocus.xyz));float3 axis=d>.001?float3(relative.z,0,-relative.x)/d:float3(0,0,1);
   return _IceFocus.xyz+float3(0,-_IceRadius,0)+originalFrame(turn(float3(0,_IceRadius,0),axis,sin(angle),cos(angle))+turn(p-anchor.xyz,axis,sin(localAngle),cos(localAngle)))+islandOffset(anchor.xyz);
  }
  float3 bridgeNormal(float3 n,float4 anchor){
   float3 relative=anchor.xyz-_IceFocus.xyz+referencePoint();float d=length(relative.xz),a=d/_IceRadius*(1-.10*upperWeight(anchor.xyz-_IceFocus.xyz));
   float3 axis=d>.001?float3(relative.z,0,-relative.x)/d:float3(0,0,1);return originalFrame(turn(n,axis,sin(a),cos(a)));
  }
  void bend(inout appdata_full v){
   float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;

 if(_IslandRigidLayout>.5 || _RecoilFlying>.5){
  v.normal=mul((float3x3)unity_WorldToObject,displayNormal(p,UnityObjectToWorldNormal(v.normal)));
  v.vertex=mul(unity_WorldToObject,float4(displayPoint(p),1));
  return;
 }
   if(_BridgeEnabled>.5){
    float3 direction=normalize(_BridgeEnd.xyz-_BridgeStart.xyz),side=cross(direction,float3(0,1,0));
    float t=dot(p-_BridgeStart.xyz,direction)/max(length(_BridgeEnd.xyz-_BridgeStart.xyz),.001);
    float3 a=bridgeWarp(_BridgeStart.xyz,_BridgeIslandA),b=bridgeWarp(_BridgeEnd.xyz,_BridgeIslandB);
    if(abs(direction.x)>.5){a.z=b.z=(a.z+b.z)*.5;}else{a.x=b.x=(a.x+b.x)*.5;}
    float3 forward=normalize(b-a);
    float3 up=float3(0,1,0);
    float3 across=normalize(cross(forward,up));up=normalize(cross(across,forward));
    float3 wp=lerp(a,b,t)+across*dot(p-_BridgeStart.xyz,side)+up*p.y;
    float3 n=UnityObjectToWorldNormal(v.normal),wn=forward*dot(n,direction)+across*dot(n,side)+up*n.y;
    v.vertex=mul(unity_WorldToObject,float4(displayPoint(wp),1));v.normal=mul((float3x3)unity_WorldToObject,wn);return;
   }
   float4 anchor=_ExplicitIsland.w>.5?_ExplicitIsland:float4(_IceFocus.xyz,0);
   v.vertex=mul(unity_WorldToObject,float4(displayPoint(bridgeWarp(p,anchor)),1));
   v.normal=mul((float3x3)unity_WorldToObject,bridgeNormal(UnityObjectToWorldNormal(v.normal),anchor));
  }
  void surf(Input i,inout SurfaceOutputStandard o){
   if(_IceDiskRadius>0 && _RecoilFlying<.5){float3 relative=i.worldPos-_IceDiskCenter;float2 disk=float2(dot(relative,_IceDiskRight),dot(relative,_IceDiskUp));clip(_IceDiskRadius*_IceDiskRadius-dot(disk,disk));}
   half fresnel=pow(1-saturate(dot(normalize(i.viewDir),normalize(i.worldNormal))),3);
   o.Albedo=_Color.rgb*.48;o.Emission=_Color.rgb*(.16+fresnel*_EdgeGlow);
   o.Metallic=.08;o.Smoothness=_Smoothness;o.Alpha=saturate(_Color.a+fresnel*.22);
  }
  ENDCG
 }
 FallBack Off
}
