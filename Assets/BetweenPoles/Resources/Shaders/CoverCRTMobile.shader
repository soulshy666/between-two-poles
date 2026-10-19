// Explicit source texture, usable on Android/iOS without IMGUI framebuffer grabs.
Shader "Hidden/BetweenPoles/CoverCRTMobile" {
 Properties { _MainTex("Composed menu",2D)="white"{} }
 SubShader { Cull Off ZWrite Off ZTest Always
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   float4 _MainTex_TexelSize;
   float _Strength,_Glow,_Bright,_Curvature;
   float3 tap(float2 uv){return tex2D(_MainTex,uv).rgb;}
   fixed4 frag(v2f_img i):SV_Target{
    float2 c=i.uv-.5;
    float2 u=c*(1+_Curvature*dot(c,c))*(1-_Curvature*.25)+.5;
    if(any(u<0)||any(u>1))return fixed4(0,0,0,1);
    float2 px=float2(_MainTex_TexelSize.x*2,0),py=float2(0,_MainTex_TexelSize.y*2);
    float ca=_Glow*.85;
    float3 col=float3(tap(u+px*ca).r,tap(u).g,tap(u-px*ca).b);
    float3 nb=(tap(u+px*.5)+tap(u-px*.5))*.5;
    col=lerp(col,nb,.18*_Strength);
    float3 bl=(tap(u+px*1.5)+tap(u-px*1.5)+tap(u+py*1.5)+tap(u-py*1.5))*.25;
    col+=bl*bl*_Glow*.55;
    float beam=.5+.5*cos((frac(u.y*192)-.5)*6.28318);
    float lum=dot(col,float3(.3,.55,.15));
    col*=lerp(1,.42+.58*beam,(.3+.55*_Strength)*(1-lum*.35));
    float m=fmod(i.pos.x,3),ms=.12*_Strength;
    col*=m<1?float3(1,1-ms,1-ms):(m<2?float3(1-ms,1,1-ms):float3(1-ms,1-ms,1));
    float2 vv=u*(1-u);
    col*=pow(saturate(vv.x*vv.y*18),.12+.38*_Strength);
    return fixed4(col*_Bright*(1.1+.28*_Strength),1);
   }
   ENDCG
  }
 }
 Fallback Off
}
