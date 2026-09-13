Shader "KeyLearner/WetPaint"
{
    Properties { _Tint("Paint color",Color)=(1,.2,.3,1) _Seed("Spatter variation",Float)=30 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10"}
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            float4 _Tint;float _Seed;
            Varyings vert(Attributes input){Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS.xyz);output.uv=input.uv;return output;}
            half4 frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;float angle=atan2(p.y,p.x),radius=length(p);
                float edge=.48+.06*sin(angle*5+_Seed)+.055*sin(angle*9-_Seed)+.25*pow(saturate(sin(angle*13+_Seed)),18);
                float mask=1-smoothstep(edge-.012,edge,radius);
                float grain=sin(p.x*95+sin(p.y*47))*sin(p.y*61)*.008;
                float wet=saturate(1-radius/max(.01,edge));
                float highlight=pow(saturate(1-length((p+float2(.15,-.18))*2.8)),5)*.55;
                float rim=(1-smoothstep(.005,.055,abs(radius-edge+.035)))*.13;
                float3 color=_Tint.rgb*(.78+wet*.22+grain)+highlight+rim;
                float alpha=mask*_Tint.a;
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
