Shader "BetweenPoles/SolarEndingBurst" {
 Properties { _Progress("Expansion", Range(0,1))=0 }
 SubShader {
  Tags { "Queue"="Overlay" "RenderType"="Transparent" }
  Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   float _Progress;
   float4 _Screen;
   fixed4 frag(v2f_img i):SV_Target {
    float2 grid = _Screen.xy / _Screen.z;
    float2 cell = floor(i.uv * grid);
    float2 p = ((cell + .5) / grid - .5) * float2(_Screen.x / _Screen.y, 1);
    float noise = frac(sin(dot(cell,float2(12.9898,78.233))) * 43758.5453);
    float radius = _Progress * length(float2(_Screen.x / _Screen.y,1)) * .61;
    float distance = length(p) + (noise - .5) * .025 * (1 - _Progress);
    float inside = step(distance,radius);
    float edge = step(radius - .055,distance);
    float3 light = lerp(float3(1,.97,.86),float3(1,.64,.18),edge);
    float sparks = step(abs(distance-radius-.035),.023)*step(.78,noise);
    return float4(lerp(light,float3(1,.93,.60),sparks),max(inside,sparks));
   }
   ENDCG
  }
 }
}
