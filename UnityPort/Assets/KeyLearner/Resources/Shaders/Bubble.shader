Shader "KeyLearner/Bubble"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; half4 c:COLOR; half fog:TEXCOORD1; };
            V vert(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.uv=i.uv; o.c=i.c; o.fog=ComputeFogFactor(o.p.z); return o; }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1; float r=length(p);
                float rim=exp(-pow((r-.82)*18,2))*.58;
                float glint=exp(-dot(p-float2(-.32,.43),p-float2(-.32,.43))*70);
                float alpha=(rim+glint*.7+saturate(1-r)*.025)*i.c.a;
                return half4(MixFog(i.c.rgb+glint*.2,i.fog),alpha);
            }
            ENDHLSL
        }
    }
}
