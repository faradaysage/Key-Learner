Shader "KeyLearner/RoarShockwave" {
 Properties { _Progress("Progress",Float)=0 _Strength("Strength",Float)=0 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
 Pass { Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
 CBUFFER_START(UnityPerMaterial) float _Progress; float _Strength; CBUFFER_END
 struct A { float4 p:POSITION; }; struct V { float4 p:SV_POSITION; };
 V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);return o;}
 half4 frag(V i):SV_Target {
  float2 uv=GetNormalizedScreenSpaceUV(i.p);float2 center=uv-.5;center.x*=_ScreenParams.x/_ScreenParams.y;
  float radius=length(center), ring=exp(-pow((radius-_Progress*.95)/.10,2));
  float amount=sin((radius-_Progress*.95)*55)*ring*_Strength*(1-_Progress);
  float2 direction=normalize(center+float2(.0001,.0001));direction.x/=_ScreenParams.x/_ScreenParams.y;
  half3 color=SampleSceneColor(saturate(uv+direction*amount*.022));
  return half4(color,ring*saturate(_Strength*1.3)*(1-_Progress));
 }
 ENDHLSL
 } } }
