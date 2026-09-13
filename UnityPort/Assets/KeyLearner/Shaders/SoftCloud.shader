Shader "KeyLearner/SoftCloud"
{
    Properties { _BaseColor("Tint",Color)=(1,1,1,1) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 position:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR;};
            struct V {float4 position:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; half fog:TEXCOORD1;};
            V vert(A i) {V o;o.position=TransformObjectToHClip(i.position.xyz);o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(o.position.z);return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float radius=dot(p,p);
                float edge=1-smoothstep(.48,1.0,radius);
                float density=pow(saturate(1-radius),.42);
                half shade=lerp(.79,1.0,saturate(.55+p.y*.5));
                half3 color=MixFog(i.color.rgb*shade,i.fog);
                return half4(color,i.color.a*edge*density);
            }
            ENDHLSL
        }
    }
}
