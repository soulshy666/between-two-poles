Shader "BetweenPoles/PaintedIceTrial" {
 Properties{_Color("Tint",Color)=(1,1,1,1) _Grid("Grid",Float)=0 _Snow("Snow",Float)=0 _IceArt("Ice art shading",Float)=0 _Painted("Painted ice",Float)=0 _Cracks("Crack strength",Range(0,1))=.3}
 SubShader{Tags{"RenderType"="Opaque" "DisableBatching"="True"} CGPROGRAM
 #pragma surface surf Lambert vertex:bend addshadow
 #include "UnityCG.cginc"
 fixed4 _Color;float _Grid,_Snow,_IceArt,_Painted,_Cracks;struct Input{float3 worldPos;float3 worldNormal;float2 logicalXZ;float4 iceTint;float2 iceSurface;};
 float4 _IceFocus,_IslandAnchors[16],_ExplicitIsland;int _IslandCount;float _IceRadius,_IslandFlatten;float3 _IslandViewOffset;float _IceDiskRadius;float3 _IceDiskCenter,_IceDiskRight,_IceDiskUp;float4x4 _IceRotation;float _IslandDisplayScale;
 float3 displayPoint(float3 p){return _IceFocus.xyz+(p-_IceFocus.xyz)*max(1,_IslandDisplayScale);}
 float3 turn(float3 q,float3 axis,float si,float co){return q*co+cross(axis,q)*si+axis*dot(axis,q)*(1-co);}
 float _BridgeEnabled;float4 _BridgeStart,_BridgeEnd,_BridgeIslandA,_BridgeIslandB;
 float3 _BridgeOffsetA,_BridgeOffsetB;
 // Same spherical rotation as CurvedIceTrial, rebased to the original composition for every island.
 float3 referencePoint(){return float3(0,0,0);}
 float3 originalFrame(float3 v){return v;}
 float upperWeight(float3 delta){return smoothstep(0,1,saturate((delta.z-abs(delta.x))/6));}
 float3 islandOffset(float3 anchor){float3 delta=anchor-_IceFocus.xyz;return _IceDiskUp*(.45*upperWeight(delta)+.25*upperWeight(-delta));}
 float3 bridgeWarp(float3 p,float4 anchor,float3 offset){float3 relative=anchor.xyz-_IceFocus.xyz+referencePoint();float d=length(relative.xz);float angle=d/_IceRadius;float localAngle=angle*(1-.10*upperWeight(anchor.xyz-_IceFocus.xyz));float3 axis=d>.001?float3(relative.z,0,-relative.x)/d:float3(0,0,1);return _IceFocus.xyz+float3(0,-_IceRadius,0)+originalFrame(turn(float3(0,_IceRadius,0),axis,sin(angle),cos(angle))+turn(p-anchor.xyz,axis,sin(localAngle),cos(localAngle)))+islandOffset(anchor.xyz);}
 float3 bridgeNormal(float3 n,float4 anchor){float3 relative=anchor.xyz-_IceFocus.xyz+referencePoint();float d=length(relative.xz);float a=d/_IceRadius*(1-.10*upperWeight(anchor.xyz-_IceFocus.xyz));float3 axis=d>.001?float3(relative.z,0,-relative.x)/d:float3(0,0,1);return originalFrame(turn(n,axis,sin(a),cos(a)));}
 void bend(inout appdata_full v,out Input o){
 UNITY_INITIALIZE_OUTPUT(Input,o);o.iceTint=v.color;
 float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;o.logicalXZ=p.xz;o.iceSurface=float2(v.normal.y,p.y);
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
 if(_IceDiskRadius>0){float3 relative=i.worldPos-_IceDiskCenter;float2 disk=float2(dot(relative,_IceDiskRight),dot(relative,_IceDiskUp));clip(_IceDiskRadius*_IceDiskRadius-dot(disk,disk));}
 float3 col=_Color.rgb;float2 g=abs(frac((i.logicalXZ+.75)/1.5)-.5);float seam=smoothstep(.470,.490,max(g.x,g.y))*_Grid;
 if(_Painted>.5){
  // Stable world-aligned pixel pigments: no scrolling texture or shimmer under the player.
  float2 p=floor(i.logicalXZ*18)/18;float field=iceField(p*.42+float2(13,6));float band=floor(saturate(field)*5)/4;
  float3 ice=lerp(float3(.35,.60,.72),float3(.77,.91,.94),saturate(band));
  float snow=smoothstep(.57,.66,field);ice=lerp(ice,float3(.86,.94,.95),floor(snow*3)/3);
  float crack=cracks(p*.48)*smoothstep(.42,.62,iceNoise(p*.3+31));
  ice=lerp(ice,float3(.23,.46,.59),crack*_Cracks);
  float dust=step(.95,iceHash(floor(p*18)))*.025;ice+=dust;
  float top=smoothstep(.45,.93,i.iceSurface.x);
  float strata=floor(saturate(-i.iceSurface.y/.85)*4)/4;
  float3 side=lerp(float3(.39,.64,.75),float3(.20,.31,.47),strata);
  side*=.94+.1*step(.48,iceNoise(float2(p.x*3+p.y*2,floor(i.iceSurface.y*18))));
  col=lerp(side,ice,top);col*=1-seam*.25*top;
  o.Albedo=col*.8;o.Emission=col*.23;o.Alpha=1;return;
 }
 float up=saturate(i.worldNormal.y);col=lerp(col,lerp(float3(.20,.49,.62),col,smoothstep(.3,.85,up)),_Snow);o.Albedo=col*(1-seam*.38);o.Emission=col*.12*(1-seam*.38);o.Alpha=1;
 }
 ENDCG} FallBack "Diffuse" }
