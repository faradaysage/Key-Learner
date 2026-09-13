Shader "KeyLearner/Terrain"
{
    Properties { _BaseColor("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 p:POSITION; float3 n:NORMAL; half4 color:COLOR; };
            struct V { float4 p:SV_POSITION; float3 n:TEXCOORD0; float3 w:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; };
            V vert(A a)
            {
                V o; o.w=TransformObjectToWorld(a.p.xyz); o.p=TransformWorldToHClip(o.w);
                o.n=TransformObjectToWorldNormal(a.n); o.color=a.color; o.fog=ComputeFogFactor(o.p.z); return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }
            half4 frag(V i):SV_Target
            {
                half3 normal=normalize(i.n);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
                half diffuse=smoothstep(.12,.72,saturate(dot(normal,sun.direction)));
                // Surface detail follows the original domain terrain exactly. Steep faces expose
                // layered stone; low slopes retain meadow colors and high peaks retain snow.
                float broad=noise(i.w.xz*.045), grain=noise(i.w.xz*.32+i.w.y*.09);
                half strata=.025*sin(i.w.y*.42+sin(i.w.z*.035)*2+sin(i.w.x*.09));
                half3 stone=lerp(half3(.19,.24,.25),half3(.39,.43,.42),broad)+strata+(grain-.5)*.035;
                half cliff=smoothstep(.055,.28,1-abs(normal.y))*smoothstep(20,65,i.w.y);
                half snow=smoothstep(135,185,i.w.y)*smoothstep(.65,.93,normal.y);
                half3 surface=lerp(i.color.rgb*(.94+broad*.08+(grain-.5)*.035),stone,cliff);
                surface=lerp(surface,half3(.77,.85,.87),snow);
                half3 color=surface*(.53+.47*diffuse*sun.shadowAttenuation)*sun.color;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
