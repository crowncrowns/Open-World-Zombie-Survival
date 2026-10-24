Shader "AdvancedGrassPainter/URP Grass"
{
    Properties
    {
        _BaseMap("Optional blade texture (alpha)",2D)="white"{}
        _Cutoff("Alpha cutoff",Range(0,1))=.4
        _AO("Root ambient occlusion",Range(0,1))=.55
        _Translucency("Leaf transmission",Range(0,1))=.32
        _GrassWind("Wind direction, strength, speed",Vector)=(.82,.57,.6,1.4)
        _GrassGust("Gust, turbulence, scale",Vector)=(.27,.28,.5,0)
        _GrassTime("Wind phase seconds",Float)=0
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull Off
        Pass
        {
            Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #include "GrassCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float4 color:COLOR;float fog:TEXCOORD3;};
            Varyings Vert(Attributes a){UNITY_SETUP_INSTANCE_ID(a);Varyings o;o.world=GrassPosition(a);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;o.color=a.color;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 Frag(Varyings i, FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 albedo=GrassAlbedo(i.uv,i.color).rgb;
                half3 n=normalize(i.normal)*IS_FRONT_VFACE(front,1,-1);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                half diffuse=saturate(dot(n,light.direction))*.75+.15;
                half transmission=saturate(dot(-n,light.direction))*_Translucency;
                half ao=lerp(1-_AO,1,smoothstep(0,.65,i.uv.y));
                half3 lighting=SampleSH(n)*ao+light.color*(diffuse+transmission)*light.distanceAttenuation*light.shadowAttenuation;
                #ifdef _ADDITIONAL_LIGHTS
                uint count=GetAdditionalLightsCount();
                for(uint j=0;j<count;j++){Light l=GetAdditionalLight(j,i.world);lighting+=l.color*(saturate(dot(n,l.direction))+.08)*l.distanceAttenuation*l.shadowAttenuation;}
                #endif
                return half4(MixFog(albedo*lighting,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragDepth
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "GrassCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;float3 _LightPosition;
            struct DepthV {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            DepthV VertShadow(Attributes a){DepthV o;float3 p=GrassPosition(a);float3 n=TransformObjectToWorldNormal(a.normalOS);
            #ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 direction=normalize(_LightPosition-p);
            #else
            float3 direction=_LightDirection;
            #endif
            o.positionCS=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
            #if UNITY_REVERSED_Z
            o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
            #else
            o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE*o.positionCS.w);
            #endif
            o.uv=a.uv;o.color=a.color;return o;}
            half4 FragDepth(DepthV i):SV_Target {GrassAlbedo(i.uv,i.color);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #include "GrassCommon.hlsl"
            struct DepthV {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            DepthV VertDepth(Attributes a){DepthV o;o.positionCS=TransformWorldToHClip(GrassPosition(a));o.uv=a.uv;o.color=a.color;return o;}
            half4 FragDepth(DepthV i):SV_Target{GrassAlbedo(i.uv,i.color);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags{"LightMode"="DepthNormalsOnly"} ZWrite On
            HLSLPROGRAM
            #pragma vertex VertNormal
            #pragma fragment FragNormal
            #include "GrassCommon.hlsl"
            struct NormalV{float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;};
            NormalV VertNormal(Attributes a){NormalV o;o.positionCS=TransformWorldToHClip(GrassPosition(a));o.normal=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv;o.color=a.color;return o;}
            half4 FragNormal(NormalV i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target{GrassAlbedo(i.uv,i.color);return half4(normalize(i.normal)*IS_FRONT_VFACE(front,1,-1),0);}
            ENDHLSL
        }
    }
}
