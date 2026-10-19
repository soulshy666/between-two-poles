Shader "Hidden/BetweenPoles/CoverCompose" {
 Properties { _MainTex("Artwork",2D)="white"{} }
 SubShader { Cull Off ZWrite Off ZTest Always
  Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   struct Input { float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   struct Output { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(Output i):SV_Target{return tex2D(_MainTex,i.uv)*i.color;}
   ENDCG
  }
 }
 Fallback Off
}
