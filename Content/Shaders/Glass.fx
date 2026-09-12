#ifdef OPENGL
#define SV_POSITION POSITION
#define VS_PROFILE vs_3_0
#define PS_PROFILE ps_3_0
#else
#define VS_PROFILE vs_4_0_level_9_1
#define PS_PROFILE ps_4_0_level_9_1
#endif
float4x4 MatrixTransform;
texture Texture;
sampler Source=sampler_state{Texture=<Texture>;MinFilter=Linear;MagFilter=Linear;AddressU=Clamp;AddressV=Clamp;};
float2 Tilt;
float Opacity;
struct Input {float4 Position:POSITION0;float4 Color:COLOR0;float2 Tex:TEXCOORD0;};
struct Output {float4 Position:SV_POSITION;float4 Color:COLOR0;float2 Tex:TEXCOORD0;};
Output VS(Input i){Output o;o.Position=mul(i.Position,MatrixTransform);o.Color=i.Color;o.Tex=i.Tex;return o;}
float4 PS(Output i):COLOR0 {float2 uv=i.Tex+Tilt*.013;float3 scene=tex2D(Source,uv).rgb;float reflection=pow(saturate(1-abs(i.Tex.x+i.Tex.y*.7+Tilt.x*.4-.75)*3),18);float fresnel=saturate(dot(Tilt,Tilt)*.25);float3 c=scene*.97+float3(.06,.10,.13)*(fresnel+.12)+reflection*(.06+fresnel*.2);return float4(c*Opacity,Opacity);}
technique Main {pass P0 {VertexShader=compile VS_PROFILE VS();PixelShader=compile PS_PROFILE PS();}}
