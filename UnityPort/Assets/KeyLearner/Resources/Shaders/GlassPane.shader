Shader "KeyLearner/GlassPane"
{
    Properties { _Tint("Glass tint",Color)=(.25,.66,.88,.065) _Shine("Broad soft reflection",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            float4 _Tint;float _Shine;
            Varyings vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output;}
            half4 frag(Varyings input):SV_Target
            {
                float diagonal=abs(frac((input.uv.x+input.uv.y)*1.35)-.5);
                float reflection=(1-smoothstep(.015,.12,diagonal))*_Shine;
                return half4(lerp(_Tint.rgb,float3(.76,.9,1),reflection*.45),_Tint.a+reflection*.075);
            }
            ENDHLSL
        }
    }
}
