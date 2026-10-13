Shader "Hidden/BetweenPoles/BlackHoleMapLighting" {
 Properties { _MainTex("Scene",2D)="white"{} }
 SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 sampler2D _MainTex;
 float4 _Islands[64],_Portals[64];
 int _IslandLightCount,_PortalLightCount;
 float _Strength,_Aspect;
 fixed4 frag(v2f_img i):SV_Target {
     float4 scene=tex2D(_MainTex,i.uv);
     float light=0,glow=0;
     for(int n=0;n<_IslandLightCount;n++){
         float2 d=abs((i.uv-_Islands[n].xy)/_Islands[n].zw);
         // Rounded rectangular pools follow each island's projected footprint.
         float distance=pow(pow(d.x,4)+pow(d.y,4),.25);
         light=max(light,1-smoothstep(.68,1.15,distance));
     }
     for(int p=0;p<_PortalLightCount;p++){
         float2 d=i.uv-_Portals[p].xy;d.y/=_Aspect;
         float r=length(d)/_Portals[p].z;
         glow+=exp(-r*r*2.8)*.22;
     }
     float3 dimmed=scene.rgb*lerp(float3(.34,.36,.46),float3(1,1,1),light);
     float3 color=dimmed+float3(.56,.24,.9)*min(glow,.35);
     return float4(lerp(scene.rgb,color,_Strength),scene.a);
 }
 ENDCG }
 }
}
