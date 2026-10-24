#ifndef ADVANCED_GRASS_GRAPH_WIND
#define ADVANCED_GRASS_GRAPH_WIND
float4 _GrassWind;
float4 _GrassGust;
float _GrassTime;
void AdvancedGrassWind_float(float3 Position, float4 UV, float4 Reserve, out float3 Deformed)
{
    float3 world=TransformObjectToWorld(Position);
    float3 absolute=GetAbsolutePositionWS(world);
    float time=_GrassTime;
    float phase=dot(absolute.xz,float2(.8,.6))*_GrassGust.z-time*_GrassWind.w+Reserve.y;
    float wave=sin(phase)*.71+sin(phase*2.17+time*.63)*.29;
    float gust=.6+.4*sin(time*_GrassGust.x*6.283+phase*.19);
    float2 direction=normalize(_GrassWind.xy+float2(-_GrassWind.y,_GrassWind.x)*sin(phase*1.7)*_GrassGust.y+.00001);
    world.xz+=direction*(wave*gust*saturate(_GrassWind.z)*Reserve.x*UV.y*UV.y);
    Deformed=TransformWorldToObject(world);
}
void AdvancedGrassWind_half(half3 Position, half4 UV, half4 Reserve, out half3 Deformed)
{
    float3 value;AdvancedGrassWind_float(Position,UV,Reserve,value);Deformed=value;
}
void AdvancedGrassSurface_float(float4 UV, float4 Vertex, float4 Texture, out float3 Color, out float Alpha, out float AO)
{
    Color=Vertex.rgb*Texture.rgb;Alpha=Texture.a;AO=lerp(.45,1,smoothstep(0,.65,UV.y));
}
void AdvancedGrassSurface_half(half4 UV, half4 Vertex, half4 Texture, out half3 Color, out half Alpha, out half AO)
{
    float3 c;float a,o;AdvancedGrassSurface_float(UV,Vertex,Texture,c,a,o);Color=c;Alpha=a;AO=o;
}
#endif
