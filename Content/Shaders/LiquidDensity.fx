#ifdef OPENGL
#define SV_POSITION POSITION
#define VS_PROFILE vs_3_0
#define PS_PROFILE ps_3_0
#else
#define VS_PROFILE vs_4_0_level_9_1
#define PS_PROFILE ps_4_0_level_9_1
#endif
float4x4 MatrixTransform;
float Time;
float Wobble;
struct Input {float4 Position:POSITION0;float4 Color:COLOR0;float2 Tex:TEXCOORD0;};
struct Output {float4 Position:SV_POSITION;float4 Color:COLOR0;float2 Tex:TEXCOORD0;};
Output VS(Input i){Output o;o.Position=mul(i.Position,MatrixTransform);o.Color=i.Color;o.Tex=i.Tex;return o;}
float4 PS(Output i):COLOR0 {float2 p=i.Tex*2-1;float angle=atan2(p.y,p.x);float shape=1+Wobble*(.13*sin(angle*3+Time*2)+.09*sin(angle*5-Time*3));float d=saturate(1-dot(p,p)/(shape*shape));d=d*d;return float4(i.Color.rgb*d,i.Color.a*d);}
technique Main {pass P0 {VertexShader=compile VS_PROFILE VS();PixelShader=compile PS_PROFILE PS();}}
