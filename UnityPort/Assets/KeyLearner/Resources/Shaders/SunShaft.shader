Shader "KeyLearner/SunShaft"
{
    Properties { _BaseColor("Light tint",Color)=(.7,.9,1,.035) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A {float4 position:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float eye:TEXCOORD1;float4 screen:TEXCOORD2;half fog:TEXCOORD3;};
            V vert(A i)
            {
                V o;float3 world=TransformObjectToWorld(i.position.xyz);o.position=TransformWorldToHClip(world);
                o.uv=i.uv;o.eye=-TransformWorldToView(world).z;o.screen=ComputeScreenPos(o.position);o.fog=ComputeFogFactor(o.position.z);return o;
            }
            half4 frag(V i):SV_Target
            {
                float edge=pow(saturate(1-abs(i.uv.x*2-1)),2.5);
                float ends=smoothstep(0,.12,i.uv.y)*(1-smoothstep(.72,1,i.uv.y));
                float depth=LinearEyeDepth(SampleSceneDepth(i.screen.xy/i.screen.w),_ZBufferParams);
                float contact=saturate((depth-i.eye)/5);
                float distanceFade=1-smoothstep(110,310,i.eye);
                float shimmer=.88+.12*sin(_Time.y*.31+i.uv.y*8);
                return half4(_BaseColor.rgb,_BaseColor.a*edge*ends*contact*distanceFade*shimmer);
            }
            ENDHLSL
        }
    }
}
