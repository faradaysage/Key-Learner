Shader "KeyLearner/Dust" {
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"} Pass {
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A{float4 p:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;half fog:TEXCOORD1;};
 V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv;o.color=a.color;o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 frag(V i):SV_Target {float2 p=i.uv*2-1;float d=length(p);float edge=.06*sin(p.x*14+sin(p.y*9));float a=pow(saturate(1-d+edge),1.5);return half4(MixFog(i.color.rgb,i.fog),i.color.a*a);}
 ENDHLSL
 } } }
