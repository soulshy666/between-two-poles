// Adapted from the user-owned 灰烬王冠.html WebGL CRT fragment shader.
Shader "BetweenPoles/CoverCRT" {
 Properties { _MainTex("Unused",2D)="white"{} }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  GrabPass { }
  Pass {
   Blend Off
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   sampler2D _GrabTexture;
   float _Strength, _Glow, _Bright, _Curvature;
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 grab:TEXCOORD1; };
   v2f vert(appdata_img v){
    v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;
    o.grab=ComputeGrabScreenPos(o.pos);return o;
   }
   float3 tap(float2 uv){return tex2D(_GrabTexture,uv).rgb;}
   fixed4 frag(v2f i):SV_Target {
    float2 c=i.uv-.5;
    float2 u=c*(1+_Curvature*dot(c,c))*(1-_Curvature*.25)+.5;
    float2 grab=i.grab.xy/i.grab.w;
    // Derivatives retain the correct grab orientation on D3D/OpenGL and Scene view.
    float2 dx=ddx(grab)/ddx(i.uv.x),dy=ddy(grab)/ddy(i.uv.y);
    float2 at=grab+dx*(u.x-i.uv.x)+dy*(u.y-i.uv.y);
    float2 px=dx/688.0,py=dy/384.0;
    if(any(u<0)||any(u>1))return fixed4(0,0,0,1);
    float ca=_Glow*.85;
    float3 col=float3(tap(at+px*ca).r,tap(at).g,tap(at-px*ca).b);
    float3 nb=(tap(at+px*.5)+tap(at-px*.5))*.5;
    col=lerp(col,nb,.18*_Strength);
    float3 bl=(tap(at+px*1.5)+tap(at-px*1.5)+tap(at+py*1.5)+tap(at-py*1.5))*.25;
    col+=bl*bl*_Glow*.55;
    // 192 phosphor rows remain readable at typical 720p/1080p game sizes.
    float fy=frac(u.y*192);
    float beam=.5+.5*cos((fy-.5)*6.28318);
    float lum=dot(col,float3(.3,.55,.15));
    col*=lerp(1,.42+.58*beam,(.3+.55*_Strength)*(1-lum*.35));
    float m=fmod(i.pos.x,3),ms=.12*_Strength;
    col*=m<1?float3(1,1-ms,1-ms):(m<2?float3(1-ms,1,1-ms):float3(1-ms,1-ms,1));
    float2 vv=u*(1-u);
    col*=pow(saturate(vv.x*vv.y*18),.12+.38*_Strength);
    col*=_Bright*(1.1+.28*_Strength);
    return fixed4(col,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
