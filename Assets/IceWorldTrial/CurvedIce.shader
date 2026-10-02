Shader "BetweenPoles/CurvedIceTrial" {
 Properties{_Color("Tint",Color)=(1,1,1,1) _Grid("Grid",Float)=0 _Snow("Snow",Float)=0}
 SubShader{Tags{"RenderType"="Opaque" "DisableBatching"="True"} CGPROGRAM
 #pragma surface surf Lambert vertex:bend addshadow
 #include "UnityCG.cginc"
 fixed4 _Color;float _Grid,_Snow;struct Input{float3 worldPos;float3 worldNormal;float2 logicalXZ;};
 float4 _IceFocus,_IslandAnchors[16],_ExplicitIsland;int _IslandCount;float _IceRadius,_IslandFlatten;float3 _IslandViewOffset;float _IceDiskRadius;float3 _IceDiskCenter,_IceDiskRight,_IceDiskUp;float4x4 _IceRotation;
 float3 turn(float3 q,float3 axis,float si,float co){return q*co+cross(axis,q)*si+axis*dot(axis,q)*(1-co);}
 float _BridgeEnabled;float4 _BridgeStart,_BridgeEnd,_BridgeIslandA,_BridgeIslandB;
 float3 _BridgeOffsetA,_BridgeOffsetB;
 float3 bridgeWarp(float3 p,float4 anchor,float3 offset){float d=length(anchor.xz);float angle=d/_IceRadius;float3 axis=d>.001?float3(anchor.z,0,-anchor.x)/d:float3(0,0,1);float localAngle=angle*(1-anchor.w);return float3(_IceFocus.x,-_IceRadius,_IceFocus.z)+mul((float3x3)_IceRotation,turn(float3(0,_IceRadius,0),axis,sin(angle),cos(angle))+turn(p-anchor.xyz,axis,sin(localAngle),cos(localAngle)))+offset;}
 float3 bridgeNormal(float3 n,float4 anchor){float d=length(anchor.xz);float a=d/_IceRadius*(1-anchor.w);float3 axis=d>.001?float3(anchor.z,0,-anchor.x)/d:float3(0,0,1);return mul((float3x3)_IceRotation,turn(n,axis,sin(a),cos(a)));}
 void bend(inout appdata_full v,out Input o){
 UNITY_INITIALIZE_OUTPUT(Input,o);
 float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;o.logicalXZ=p.xz;float3 anchor=float3(-3.75,0,0);float best=1e9;
 if(_BridgeEnabled>.5){
  // A single straight span between the two shoreline edges, not a curved blend.
  float3 direction=normalize(_BridgeEnd.xyz-_BridgeStart.xyz);float3 side=cross(direction,float3(0,1,0));
  float lengthLogical=max(length(_BridgeEnd.xyz-_BridgeStart.xyz),.001);
  float t=dot(p-_BridgeStart.xyz,direction)/lengthLogical;
  float3 a=bridgeWarp(_BridgeStart.xyz,_BridgeIslandA,_BridgeOffsetA),b=bridgeWarp(_BridgeEnd.xyz,_BridgeIslandB,_BridgeOffsetB);
  float3 forward=normalize(b-a);
  float3 up=normalize(bridgeNormal(float3(0,1,0),_BridgeIslandA)+bridgeNormal(float3(0,1,0),_BridgeIslandB));
  float3 across=normalize(cross(forward,up));up=normalize(cross(across,forward));
  float3 wp=lerp(a,b,t)+across*dot(p-_BridgeStart.xyz,side)+up*p.y;
  float3 n=UnityObjectToWorldNormal(v.normal);float3 wn=forward*dot(n,direction)+across*dot(n,side)+up*n.y;
  v.vertex=mul(unity_WorldToObject,float4(wp,1));v.normal=mul((float3x3)unity_WorldToObject,wn);return;
 }
 for(int k=0;k<_IslandCount;k++){float d=dot(p.xz-_IslandAnchors[k].xz,p.xz-_IslandAnchors[k].xz);if(d<best){best=d;anchor=_IslandAnchors[k].xyz;}}
 if(_ExplicitIsland.w>.5)anchor=_ExplicitIsland.xyz;float distance=length(anchor.xz);float angle=distance/_IceRadius;float3 axis=distance>.001?float3(anchor.z,0,-anchor.x)/distance:float3(0,0,1);
 float3 normal=turn(float3(0,1,0),axis,sin(angle),cos(angle));
 float localAngle=angle*(1-_IslandFlatten);float3 q=turn(p-anchor,axis,sin(localAngle),cos(localAngle));
 p=float3(_IceFocus.x,-_IceRadius,_IceFocus.z)+mul((float3x3)_IceRotation,normal*_IceRadius+q)+_IslandViewOffset;
 v.vertex=mul(unity_WorldToObject,float4(p,1));float3 n=turn(UnityObjectToWorldNormal(v.normal),axis,sin(localAngle),cos(localAngle));n=mul((float3x3)_IceRotation,n);v.normal=mul((float3x3)unity_WorldToObject,n);
 }

 void surf(Input i,inout SurfaceOutput o){if(_IceDiskRadius>0){float3 relative=i.worldPos-_IceDiskCenter;float2 disk=float2(dot(relative,_IceDiskRight),dot(relative,_IceDiskUp));clip(_IceDiskRadius*_IceDiskRadius-dot(disk,disk));}float3 col=_Color.rgb;float2 g=abs(frac((i.logicalXZ+.75)/1.5)-.5);float seam=smoothstep(.470,.490,max(g.x,g.y))*_Grid;float up=saturate(i.worldNormal.y);col=lerp(col,lerp(float3(.20,.49,.62),col,smoothstep(.3,.85,up)),_Snow);o.Albedo=col*(1-seam*.38);o.Emission=col*.12*(1-seam*.38);o.Alpha=1;}
 ENDCG} FallBack "Diffuse" }

