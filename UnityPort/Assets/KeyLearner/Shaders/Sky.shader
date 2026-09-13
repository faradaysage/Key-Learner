Shader "KeyLearner/Sky" {
 SubShader { Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
 struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 direction:TEXCOORD0;};
 V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.direction=a.p.xyz;return o;}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);float a=frac(sin(dot(i,float2(127.1,311.7)))*43758.5453);float b=frac(sin(dot(i+float2(1,0),float2(127.1,311.7)))*43758.5453);float c=frac(sin(dot(i+float2(0,1),float2(127.1,311.7)))*43758.5453);float d=frac(sin(dot(i+1,float2(127.1,311.7)))*43758.5453);return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);}
 half4 frag(V i):SV_Target {float3 d=normalize(i.direction);float horizon=saturate(d.y);half3 color=lerp(half3(.67,.84,.93),half3(.18,.46,.78),pow(horizon,.5));float2 p=d.xz/max(.08,d.y)*2.5+_Time.y*.003;float n=noise(p)*.6+noise(p*2.05)*.28+noise(p*4.2)*.12;float clouds=smoothstep(.55,.70,n)*smoothstep(.03,.23,d.y);color=lerp(color,half3(1,.98,.92),clouds*.25);float sun=pow(saturate(dot(d,normalize(float3(-.4,.6,-.6)))),400);color+=sun*.5;return half4(SRGBToLinear(color),1);}
 ENDHLSL
 } } }

