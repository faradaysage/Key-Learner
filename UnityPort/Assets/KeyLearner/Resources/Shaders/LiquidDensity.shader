Shader "KeyLearner/LiquidDensity"
{
    SubShader
    {
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 motion:TEXCOORD1;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 motion:TEXCOORD1;float4 color:COLOR;};
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS=float4(input.positionOS.xy*2-1,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                output.positionCS.y=-output.positionCS.y;
                #endif
                output.uv=input.uv;output.motion=input.motion;output.color=input.color;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;float angle=atan2(p.y,p.x);
                float age=input.motion.x,wobble=input.motion.y;
                float shape=1+wobble*(.13*sin(angle*3+age*2)+.09*sin(angle*5-age*3));
                float mass=saturate(1-dot(p,p)/(shape*shape));mass*=mass;
                return input.color*mass;
            }
            ENDHLSL
        }
    }
}
