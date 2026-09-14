Shader "KeyLearner/FlameAtlas"
{
    Properties { _MainTex("Flame atlas",2D)="black" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" }
        Pass
        {
            Blend One One
            ZWrite Off
            ColorMask RGB
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            Varyings vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;output.color=input.color;return output;}
            half4 frag(Varyings input):SV_Target
            {
                half3 source=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).rgb;
                // The original atlas has a bright rim and a faint filled interior.
                // Compress that contrast while preserving its authored silhouette
                // and exact-black additive background, rather than drawing an outline.
                float density=saturate((max(source.r,max(source.g,source.b))-.008)/.992);
                float body=pow(density,.35);
                float core=body*pow(saturate((1-input.uv.y)*1.4),2);
                half3 color=input.color.rgb*(body*.52+density*.12);
                color+=input.color.aaa*core*.12;
                return half4(color,0);
            }
            ENDHLSL
        }
    }
}
