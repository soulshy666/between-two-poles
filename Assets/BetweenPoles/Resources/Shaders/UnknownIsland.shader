Shader "Hidden/BetweenPoles/UnknownIsland" {
 SubShader {
  Tags { "RenderType"="Opaque" "DisableBatching"="True" }
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _IceFocus; float _IslandDisplayScale;
   float4 vert(appdata_base v):SV_POSITION {
    float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
    p=_IceFocus.xyz+(p-_IceFocus.xyz)*max(1,_IslandDisplayScale);
    return mul(UNITY_MATRIX_VP,float4(p,1));
   }
   fixed4 frag():SV_Target { return fixed4(.008,.01,.016,1); }
   ENDCG
  }
 }
}
