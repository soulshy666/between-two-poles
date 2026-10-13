sampler2D _DiscoveryMask;
float4 _DiscoveryBounds;
float _DiscoveryEnabled;
float DiscoveryHidden(float2 logicalXZ){
 // Discovery masking is enabled only while viewing the overview map.
 if(_DiscoveryEnabled<.5)return 0;
 float2 uv=(logicalXZ-_DiscoveryBounds.xy)/max(_DiscoveryBounds.zw,.001);
 if(any(uv<0)||any(uv>1))return 0;
 return step(.5,tex2D(_DiscoveryMask,uv).r);
}
