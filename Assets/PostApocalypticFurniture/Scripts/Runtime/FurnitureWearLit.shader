Shader "PostApocalypticFurniture/Wear Lit"
{
 Properties {
  [MainTexture] _BaseMap("Base Color",2D)="white"{} [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  [Normal] _BumpMap("Normal",2D)="bump"{} _BumpScale("Normal strength",Range(0,2))=1
  _MaskMap("Metal(R) AO(G) Smooth(A)",2D)="white"{} _WearMap("Wear pattern",2D)="gray"{}
  _Metallic("Metallic",Range(0,1))=1 _Smoothness("Smoothness scale",Range(0,1))=1
  _EmissionColor("Emission",Color)=(0,0,0,0) _DamageColor("Exposed substrate",Color)=(.3,.22,.14,1)
  [HideInInspector] _Cull("Cull",Float)=2 [HideInInspector] _Cutoff("Cutoff",Float)=.5
  _FurnitureDirt("Dirt",Range(0,1))=.35 _FurnitureFade("Fade",Range(0,1))=.25
  _FurnitureDamage("Surface damage",Range(0,1))=.25 _FurnitureSeed("Seed",Float)=1
  _FurnitureWearColor("Faded tint",Color)=(.55,.49,.35,1)
 }
 SubShader {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _LIGHT_COOKIES
   #pragma multi_compile _ LIGHTMAP_ON
   #pragma multi_compile _ DIRLIGHTMAP_COMBINED
   #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
   #pragma multi_compile _ SHADOWS_SHADOWMASK
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   TEXTURE2D(_MaskMap);SAMPLER(sampler_MaskMap);TEXTURE2D(_WearMap);SAMPLER(sampler_WearMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_BaseColor,_EmissionColor,_DamageColor;
    float _BumpScale,_Metallic,_Smoothness,_Cutoff,_Cull;
   CBUFFER_END
   UNITY_INSTANCING_BUFFER_START(FurnitureWear)
    UNITY_DEFINE_INSTANCED_PROP(float, _FurnitureDirt)
    UNITY_DEFINE_INSTANCED_PROP(float, _FurnitureFade)
    UNITY_DEFINE_INSTANCED_PROP(float, _FurnitureDamage)
    UNITY_DEFINE_INSTANCED_PROP(float, _FurnitureSeed)
    UNITY_DEFINE_INSTANCED_PROP(float4, _FurnitureWearColor)
   UNITY_INSTANCING_BUFFER_END(FurnitureWear)
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 uv1:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 pos:TEXCOORD1;half3 n:TEXCOORD2;half4 t:TEXCOORD3;float3 local:TEXCOORD4;half fog:TEXCOORD5;DECLARE_LIGHTMAP_OR_SH(lightmapUV,vertexSH,6);UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   V vert(A a) {V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(a.normalOS,a.tangentOS);
    o.positionCS=p.positionCS;o.pos=p.positionWS;o.local=a.positionOS.xyz;o.n=n.normalWS;o.t=half4(n.tangentWS,a.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(p.positionCS.z);
    OUTPUT_LIGHTMAP_UV(a.uv1,unity_LightmapST,o.lightmapUV);
    #ifndef LIGHTMAP_ON
    o.vertexSH=SampleSH(o.n);
    #endif
    return o;}
   half4 frag(V i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);
    float seed=UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureSeed);
    float dirtAmount=UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureDirt);
    float fadeAmount=UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureFade);
    float damageAmount=UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureDamage);
    half3 fadedColor=UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureWearColor).rgb;
    float2 shift=float2(frac(seed*.618),frac(seed*.381));
    half4 mask=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv);half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    half3 wear=SAMPLE_TEXTURE2D(_WearMap,sampler_WearMap,i.uv*.61+shift).rgb;
    half dirt=saturate((.64-wear.r)*2.5)*dirtAmount;half fade=saturate(wear.g*.85+.12)*fadeAmount;
    half scratch=smoothstep(.82,.95,wear.b)*damageAmount;
    albedo=lerp(albedo,lerp(albedo,fadedColor,.48),fade);
    albedo*=1-dirt*.55;albedo=lerp(albedo,_DamageColor.rgb,scratch*.55);
    half3 nts=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
    half3 bitangent=cross(i.n,i.t.xyz)*i.t.w;half3 normal=NormalizeNormalPerPixel(TransformTangentToWorld(nts,half3x3(i.t.xyz,bitangent,i.n)));
    InputData input=(InputData)0;input.positionWS=i.pos;input.normalWS=normal;input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.pos);
    input.shadowCoord=TransformWorldToShadowCoord(i.pos);input.fogCoord=i.fog;input.vertexLighting=VertexLighting(i.pos,normal);input.bakedGI=SAMPLE_GI(i.lightmapUV,i.vertexSH,normal);input.shadowMask=SAMPLE_SHADOWMASK(i.lightmapUV);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.metallic=mask.r*_Metallic*(1-dirt*.6);s.specular=half3(.04,.04,.04);s.smoothness=mask.a*_Smoothness*(1-dirt*.7);s.normalTS=nts;s.occlusion=mask.g*(1-dirt*.25);s.emission=_EmissionColor.rgb;s.alpha=1;
    half4 color=UniversalFragmentPBR(input,s);color.rgb=MixFog(color.rgb,i.fog);return color;}
   ENDHLSL
  }
  Pass {
   Name "Meta" Tags {"LightMode"="Meta"} Cull Off
   HLSLPROGRAM
   #pragma vertex metaVert
   #pragma fragment metaFrag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_WearMap);SAMPLER(sampler_WearMap);TEXTURE2D(_MaskMap);SAMPLER(sampler_MaskMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST,_BaseColor,_EmissionColor,_DamageColor;
    float _BumpScale,_Metallic,_Smoothness,_Cutoff,_Cull;
   CBUFFER_END
   UNITY_INSTANCING_BUFFER_START(FurnitureWear)
    UNITY_DEFINE_INSTANCED_PROP(float, _FurnitureDirt)
    UNITY_DEFINE_INSTANCED_PROP(float, _FurnitureFade)
    UNITY_DEFINE_INSTANCED_PROP(float, _FurnitureDamage)
    UNITY_DEFINE_INSTANCED_PROP(float, _FurnitureSeed)
    UNITY_DEFINE_INSTANCED_PROP(float4, _FurnitureWearColor)
   UNITY_INSTANCING_BUFFER_END(FurnitureWear)
   struct MA {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 uv1:TEXCOORD1;float2 uv2:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct MV {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   MV metaVert(MA a) {MV o=(MV)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);o.positionCS=UnityMetaVertexPosition(a.positionOS.xyz,a.uv1,a.uv2);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);return o;}
   half4 metaFrag(MV i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);
    float seed=UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureSeed);
    half3 wear=SAMPLE_TEXTURE2D(_WearMap,sampler_WearMap,i.uv*.61+float2(frac(seed*.618),frac(seed*.381))).rgb;
    half dirt=saturate((.64-wear.r)*2.5)*UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureDirt);
    half fade=saturate(wear.g*.85+.12)*UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureFade);
    half scratch=smoothstep(.82,.95,wear.b)*UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureDamage);
    half3 a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
    a=lerp(a,lerp(a,UNITY_ACCESS_INSTANCED_PROP(FurnitureWear,_FurnitureWearColor).rgb,.48),fade);
    a=lerp(a*(1-dirt*.55),_DamageColor.rgb,scratch*.55);
    half4 mask=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv);
    half metal=mask.r*_Metallic*(1-dirt*.6);half rough=1-mask.a*_Smoothness*(1-dirt*.7);
    MetaInput m=(MetaInput)0;m.Albedo=a*(1-metal)*.96+lerp(half3(.04,.04,.04),a,metal)*rough*rough*.5;m.Emission=_EmissionColor.rgb;return MetaFragment(m);}
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
 FallBack "Universal Render Pipeline/Lit"
}
