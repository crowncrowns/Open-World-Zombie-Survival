Shader "MoscowTrees/Leaves Cutout Builtin"
{
    Properties {
        _MainTex ("Leaves", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.45
    }
    SubShader {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows
        #pragma target 3.0
        #pragma multi_compile_instancing
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input { float2 uv_MainTex; };
        void surf(Input IN, inout SurfaceOutputStandard o) {
            fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=c.rgb; o.Alpha=c.a; o.Metallic=0; o.Smoothness=0.08;
        }
        ENDCG
    }
    Fallback "Transparent/Cutout/Diffuse"
}
