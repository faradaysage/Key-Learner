Shader "KeyLearner/Water" {
 Properties {_BaseColor("Water",Color)=(.05,.55,.73,.7)}
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"} Pass {
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial) half4 _BaseColor; float4 _Ripple; CBUFFER_END
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;};
 V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.fog=ComputeFogFactor(o.p.z);return o;}
 half4 frag(V i):SV_Target {float wave=sin(i.w.x*.18+sin(i.w.z*.053)*1.5+_Time.y*.7)*sin(i.w.z*.13+sin(i.w.x*.037)+_Time.y*.5);float shimmer=pow(saturate(wave*.7+sin(dot(i.w.xz,float2(.073,.061))-_Time.y*.3)*.3),12);float ripple=sin(distance(i.w.xz,_Ripple.xy)*.85-_Time.y*4)*exp(-distance(i.w.xz,_Ripple.xy)*.045)*_Ripple.z;float grazing=pow(1-saturate(abs(normalize(_WorldSpaceCameraPos-i.w).y)),3);half3 c=lerp(_BaseColor.rgb,half3(.34,.59,.72),grazing*.45)+shimmer*.18+ripple*.1;return half4(MixFog(c,ComputeFogFactor(TransformWorldToHClip(i.w).z)),lerp(_BaseColor.a,1,grazing));}
 ENDHLSL
 } } }
