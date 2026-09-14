Shader "KeyLearner/Pearl" {
Properties { _BaseColor("Tint",Color)=(.91,.95,1,1) }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
struct A {float4 position:POSITION;float3 normal:NORMAL;};
struct V {float4 position:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;};
V vert(A i){V o;o.world=TransformObjectToWorld(i.position.xyz);o.position=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normal);return o;}
half4 frag(V i):SV_Target {
float3 n=normalize(i.normal),v=normalize(_WorldSpaceCameraPos-i.world);
float rim=pow(1-saturate(dot(n,v)),2.2);
float band=dot(n,normalize(float3(.4,.7,-.5)))*2.6+rim*4;
float3 nacre=.7+.22*cos(float3(0,2.1,4.2)+band*3.14);
float glint=pow(saturate(dot(reflect(-normalize(float3(-.4,.7,-.8)),n),v)),40);
return half4(lerp(_BaseColor.rgb,nacre,.65)+glint*.8+rim*.2,.48+rim*.45);
}
ENDHLSL
}}}
