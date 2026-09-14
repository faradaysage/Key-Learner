Shader "KeyLearner/SeabedCaustics"
{
    Properties
    {
        _Intensity("Caustic intensity", Range(0, 0.5)) = 0.19
        _MotionSpeed("Water movement", Range(0, 1)) = 0.23
        _PatternScale("Ripple scale", Float) = 0.065
    }
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
            CBUFFER_START(UnityPerMaterial)
                float _Intensity, _MotionSpeed, _PatternScale;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.color=i.color;
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            float2 hash22(float2 p)
            {
                return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);
            }
            float caustic(float2 p, float time)
            {
                p+=.24*sin(p.yx*1.2+float2(time,time*.71));
                float2 cell=floor(p), cellFraction=frac(p);
                float near1=8,near2=8;
                [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 offset=float2(x,y);
                    float2 seed=hash22(cell+offset);
                    float2 featurePosition=.5+.32*sin(6.283185*seed+time*.48);
                    float2 delta=offset+featurePosition-cellFraction;
                    float squaredDistance=dot(delta,delta);
                    if(squaredDistance<near1){near2=near1;near1=squaredDistance;}else near2=min(near2,squaredDistance);
                }
                return 1-smoothstep(.005,.07,near2-near1);
            }
            half4 frag(Varyings i):SV_Target
            {
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half facing=smoothstep(.05,.8,saturate(dot(normalize(i.normalWS),sun.direction)));
                half3 baseColor=i.color.rgb*(.58+.42*facing*sun.shadowAttenuation)*sun.color;
                float time=_Time.y*_MotionSpeed;
                float pattern=caustic(i.positionWS.xz*_PatternScale,time);
                float distanceFade=1-smoothstep(160,470,distance(i.positionWS,_WorldSpaceCameraPos));
                baseColor+=half3(.45,.90,.88)*pattern*_Intensity*distanceFade*saturate(i.normalWS.y)*sun.shadowAttenuation;
                return half4(MixFog(baseColor,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
