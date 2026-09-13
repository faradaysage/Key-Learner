Shader "KeyLearner/MergingLiquid"
{
    Properties { _Field("Density field",2D)="black" {} }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            TEXTURE2D(_Field);SAMPLER(sampler_Field);
            float4 _Field_TexelSize;
            float4 _Palette0,_Palette1,_Palette2,_Palette3;
            Varyings vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output;}
            half4 frag(Varyings input):SV_Target
            {
                float4 density=SAMPLE_TEXTURE2D(_Field,sampler_Field,input.uv);
                float mass=density.a;
                float3 tint=density.rgb/max(mass,.001);
                float3 rim=_Palette1.rgb;
                float nearest=dot(tint-_Palette0.rgb,tint-_Palette0.rgb);
                float distance1=dot(tint-_Palette1.rgb,tint-_Palette1.rgb);
                if(distance1<nearest){nearest=distance1;rim=_Palette2.rgb;}
                float distance2=dot(tint-_Palette2.rgb,tint-_Palette2.rgb);
                if(distance2<nearest){nearest=distance2;rim=_Palette3.rgb;}
                float distance3=dot(tint-_Palette3.rgb,tint-_Palette3.rgb);
                if(distance3<nearest)rim=_Palette0.rgb;
                float edge=smoothstep(.12,.23,mass),core=smoothstep(.48,.64,mass);
                float2 texel=_Field_TexelSize.xy;
                float dx=SAMPLE_TEXTURE2D(_Field,sampler_Field,input.uv+float2(texel.x,0)).a-SAMPLE_TEXTURE2D(_Field,sampler_Field,input.uv-float2(texel.x,0)).a;
                float dy=SAMPLE_TEXTURE2D(_Field,sampler_Field,input.uv+float2(0,texel.y)).a-SAMPLE_TEXTURE2D(_Field,sampler_Field,input.uv-float2(0,texel.y)).a;
                float3 normal=normalize(float3(-dx*12,-dy*12,1));
                float light=.65+.35*saturate(dot(normal,normalize(float3(-.6,.7,1))));
                float shine=pow(saturate(dot(normal,normalize(float3(-.3,.45,1)))),36)*.6;
                float3 color=lerp(rim,tint,core)*light+shine*core;
                float alpha=saturate(mass*.08+edge*.92);
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
