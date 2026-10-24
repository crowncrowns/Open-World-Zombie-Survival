Shader "ZombieSurvival/Dumpster Surface"
{
 Properties
 {
  _BaseMap("Base colour",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  [Normal]_BumpMap("Baked tangent normal",2D)="bump"{}
  _MicroNormal("Micro surface normal",2D)="bump"{}
  _MaskMap("Metal / AO / unused / smoothness",2D)="white"{}
  _BakedAO("Baked occlusion",2D)="white"{}
  _GrimeMasks("Sediment / leaks / fading",2D)="gray"{}
  _DirtAmount("Dirt",Range(0,1))=.65
  _VariantSeed("Mask offset",Float)=0
  _FadeAmount("Faded plastic",Range(0,1))=.2
  _HeightOffset("Local height offset",Float)=0
  _Cutoff("Cutoff",Float)=.5
 }
 SubShader
 {
  Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit"
   Tags{"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   TEXTURE2D(_MicroNormal);SAMPLER(sampler_MicroNormal);
   TEXTURE2D(_MaskMap);SAMPLER(sampler_MaskMap);
   TEXTURE2D(_BakedAO);SAMPLER(sampler_BakedAO);
   TEXTURE2D(_GrimeMasks);SAMPLER(sampler_GrimeMasks);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;half4 _BaseColor;float _DirtAmount,_VariantSeed,_FadeAmount,_Cutoff,_HeightOffset;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;float height:TEXCOORD4;half fog:TEXCOORD5;float2 grimeUV:TEXCOORD6;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   Varyings vert(Attributes a)
   {
    Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(a.normalOS,a.tangentOS);
    o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;o.tangentWS=half4(n.tangentWS,a.tangentOS.w*GetOddNegativeScale());o.uv=a.uv;o.height=a.positionOS.y+_HeightOffset;o.grimeUV=float2(abs(a.normalOS.x)>abs(a.normalOS.z)?a.positionOS.z:a.positionOS.x,o.height);o.fog=ComputeFogFactor(p.positionCS.z);return o;
   }
   half4 frag(Varyings i):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);
    float2 uv=i.uv;half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_BaseColor;
    half4 mask=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,uv);
    float2 guv=i.grimeUV*.75+float2(frac(_VariantSeed*.173),frac(_VariantSeed*.317));
    half3 grime=SAMPLE_TEXTURE2D(_GrimeMasks,sampler_GrimeMasks,guv).rgb;
    half bottom=1-smoothstep(.30,.85,i.height);
    half sediment=saturate(_DirtAmount*(bottom*(.25+grime.r*1.2)+grime.g*.55));
    base.rgb=lerp(base.rgb,half3(.095,.078,.048)*(1+grime.r*.5),sediment*.78);
    base.rgb=lerp(base.rgb,base.rgb*1.3+half3(.016,.014,.004),_FadeAmount*grime.b);
    half3 normal=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv));
    half3 micro=UnpackNormal(SAMPLE_TEXTURE2D(_MicroNormal,sampler_MicroNormal,uv*2));normal=normalize(half3(normal.xy+micro.xy*.25,normal.z));
    half3 bitangent=cross(i.normalWS,i.tangentWS.xyz)*i.tangentWS.w;
    InputData input=(InputData)0;input.positionWS=i.positionWS;input.normalWS=normalize(TransformTangentToWorld(normal,half3x3(i.tangentWS.xyz,bitangent,i.normalWS)));input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);input.bakedGI=SampleSH(input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);input.shadowMask=half4(1,1,1,1);
    SurfaceData s=(SurfaceData)0;s.albedo=base.rgb;s.alpha=1;s.metallic=mask.r;s.specular=half3(.04,.04,.04);s.smoothness=lerp(mask.a,.13,sediment);s.normalTS=normal;s.occlusion=mask.g*SAMPLE_TEXTURE2D(_BakedAO,sampler_BakedAO,uv).r;
    half4 col=UniversalFragmentPBR(input,s);col.rgb=MixFog(col.rgb,i.fog);return col;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
 FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
