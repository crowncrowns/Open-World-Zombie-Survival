#ifndef ADVANCED_GRASS_COMMON
#define ADVANCED_GRASS_COMMON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _GrassWind, _GrassGust;
float _Cutoff, _AO, _Translucency, _GrassTime;
CBUFFER_END
struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float2 uv:TEXCOORD0; float2 wind:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
float3 GrassPosition(Attributes a)
{
    float3 p=TransformObjectToWorld(a.positionOS.xyz);
    float phase=dot(p.xz,float2(.8,.6))*_GrassGust.z-_GrassTime*_GrassWind.w+a.wind.y;
    float wave=sin(phase)*.71+sin(phase*2.17+_GrassTime*.63)*.29;
    float gust=.6+.4*sin(_GrassTime*_GrassGust.x*6.283+phase*.19);
    float2 direction=normalize(_GrassWind.xy+float2(-_GrassWind.y,_GrassWind.x)*sin(phase*1.7)*_GrassGust.y+.00001);
    p.xz+=direction*(wave*gust*saturate(_GrassWind.z)*a.wind.x*a.uv.y*a.uv.y);
    return p;
}
float4 GrassAlbedo(float2 uv,float4 color)
{
    float4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,TRANSFORM_TEX(uv,_BaseMap));
    clip(tex.a-_Cutoff); return tex*color;
}
#endif
