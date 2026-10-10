Shader "BetweenPoles/ChapterIslandSurface" {
 Properties{_Theme("Chapter theme",Float)=1 _Color("Tint",Color)=(1,1,1,1) _Grid("Grid",Float)=0 _Snow("Snow",Float)=0 _IceArt("Ice art shading",Float)=0 _Painted("Painted ice",Float)=0 _Cracks("Crack strength",Range(0,1))=.3}
 SubShader{Tags{"RenderType"="Opaque" "DisableBatching"="True"} CGPROGRAM
 #pragma surface surf Lambert vertex:bend addshadow
 #include "UnityCG.cginc"
 fixed4 _Color;float _Grid,_Snow,_IceArt,_Painted,_Cracks,_Theme;struct Input{float3 worldPos;float3 worldNormal;float2 logicalXZ;float4 iceTint;float2 iceSurface;};
 float4 _IceFocus,_IslandAnchors[16],_ExplicitIsland;int _IslandCount;float _IceRadius,_IslandFlatten;float3 _IslandViewOffset;float _IceDiskRadius;float3 _IceDiskCenter,_IceDiskRight,_IceDiskUp;float4x4 _IceRotation;float _IslandDisplayScale;
 float _IslandWindowClipEnabled;float4 _IslandWindowBounds;
 // Connected gameplay uses one common frame; never stretch a bridge between warped shores.
 float _IslandRigidLayout,_RecoilFlying; float3 _IceCurveFocus,_RecoilDisplayOffset;
 // One continuous arc for terrain, bridges and actors: no separate shore offsets.
 float _IslandFlatEnabled;float4 _IslandFlatBounds;
 float _IslandPlanar;
  float3 surfaceFrame(float3 p,out float3 axis,out float a){
   if(_IslandPlanar>.5){axis=float3(0,0,1);a=0;return _IceFocus.xyz+(p-_IceFocus.xyz)*max(1,_IslandDisplayScale);}
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
 UNITY_INITIALIZE_OUTPUT(Input,o);o.iceTint=v.color;
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

 float iceHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float iceNoise(float2 p){float2 k=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(iceHash(k),iceHash(k+float2(1,0)),f.x),lerp(iceHash(k+float2(0,1)),iceHash(k+1),f.x),f.y);}
 float iceField(float2 p){return iceNoise(p)*.65+iceNoise(p*2.03+7)*.25+iceNoise(p*4.1)*.1;}
 float cracks(float2 p){float2 id=floor(p),f=frac(p);float nearest=9,second=9;for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){float2 cell=float2(x,y);float2 seed=float2(iceHash(id+cell),iceHash(id+cell+41));float d=length(cell+seed-f);if(d<nearest){second=nearest;nearest=d;}else second=min(second,d);}return 1-smoothstep(.014,.05,second-nearest);}
 void surf(Input i,inout SurfaceOutput o){
 if(_IslandWindowClipEnabled>.5){float2 edge=min(i.logicalXZ-_IslandWindowBounds.xy,_IslandWindowBounds.zw-i.logicalXZ);clip(min(edge.x,edge.y));}
 if(_IceDiskRadius>0 && _RecoilFlying<.5){float3 relative=i.worldPos-_IceDiskCenter;float2 disk=float2(dot(relative,_IceDiskRight),dot(relative,_IceDiskUp));clip(_IceDiskRadius*_IceDiskRadius-dot(disk,disk));}
 float3 col=_Color.rgb;float2 g=abs(frac((i.logicalXZ+.75)/1.5)-.5);float seam=smoothstep(.452,.486,max(g.x,g.y))*_Grid;
 if(_Painted>.5){
  // Stable world-aligned pixel pigments: no scrolling texture or shimmer under the player.
  float2 p=floor(i.logicalXZ*18)/18;float broad=iceNoise(floor(i.logicalXZ*3.2)/3.2+float2(13,6));
  float field=iceField(p*.30+float2(13,6))*.28+broad*.72;float band=floor(saturate(field)*5)/4;
  float3 ice=lerp(float3(.25,.50,.66),float3(.72,.89,.95),saturate(band));
  float snow=smoothstep(.56,.72,field);ice=lerp(ice,float3(.90,.97,1),floor(snow*3)/3);
  float crack=0;
  ice=lerp(ice,float3(.13,.36,.54),crack*_Cracks);
  float crystal=step(.91,iceNoise(floor(i.logicalXZ*7.0)/7.0+19));ice+=crystal*float3(.05,.16,.22);
  float dust=step(.965,iceHash(floor(p*18)))*.018;ice+=dust;
  float top=smoothstep(.45,.93,i.iceSurface.x);
  float strata=floor(saturate(-i.iceSurface.y/.85)*4)/4;
  float3 side=lerp(float3(.25,.50,.66),float3(.08,.18,.34),strata);
  side*=.90+.14*step(.48,iceNoise(float2(p.x*3+p.y*2,floor(i.iceSurface.y*18))));
  if(_Theme<1.5){
   ice=lerp(float3(.18,.34,.16),float3(.57,.70,.30),band);
   ice=lerp(ice,float3(.70,.76,.43),step(.68,field)*.55);
   side=lerp(float3(.35,.32,.19),float3(.14,.21,.16),strata);
  }else if(_Theme<2.5){
   ice=lerp(float3(.48,.31,.20),float3(.86,.72,.46),band);
   ice=lerp(ice,float3(.94,.85,.62),step(.66,field)*.6);
   side=lerp(float3(.54,.37,.24),float3(.25,.21,.24),strata);
  }else{
   ice=lerp(float3(.16,.14,.20),float3(.39,.30,.34),band);
   float glow=crack*.5;ice=lerp(ice,float3(.98,.36,.07),glow);
   side=lerp(float3(.30,.17,.20),float3(.10,.10,.16),strata);
  }
  float3 gridColor=_Theme<1.5?float3(.10,.22,.12):(_Theme<2.5?float3(.30,.20,.12):float3(.07,.05,.09));
  col=lerp(side,ice,top);col=lerp(col,gridColor,seam*.80*top);
  o.Albedo=col*.88;o.Emission=col*.12+crystal*float3(.015,.05,.08);o.Alpha=1;return;
 }
 float up=saturate(i.worldNormal.y);col=lerp(col,lerp(float3(.20,.49,.62),col,smoothstep(.3,.85,up)),_Snow);o.Albedo=col*(1-seam*.38);o.Emission=col*.12*(1-seam*.38);o.Alpha=1;
 }
 ENDCG} FallBack "Diffuse" }
