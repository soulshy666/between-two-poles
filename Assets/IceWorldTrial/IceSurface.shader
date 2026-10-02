Shader "BetweenPoles/IceTrialSurface" {
 Properties{_Color("Ice tint",Color)=(.67,.86,.94,1) _Grid("Grid",Float)=1}
 SubShader{Tags{"RenderType"="Opaque"} CGPROGRAM
 #pragma surface surf Lambert
 #include "UnityCG.cginc"
 fixed4 _Color;float _Grid;struct Input{float3 worldPos;float3 worldNormal;};
 void surf(Input i,inout SurfaceOutput o){float3 p=i.worldPos;float top=smoothstep(.3,.8,i.worldNormal.y);float2 g=abs(frac((p.xz+.75)/1.5)-.5);float seam=smoothstep(.485,.497,max(g.x,g.y))*_Grid;
 float n=frac(sin(dot(floor(p.xz*28),float2(12.9898,78.233)))*43758.5453);
 float3 ice=lerp(float3(.17,.47,.61),_Color.rgb,top);ice*=1-seam*.13*top;ice+=(n-.5)*.022;
 o.Albedo=ice;o.Emission=ice*.16;o.Alpha=1;
 }ENDCG } FallBack "Diffuse" }
