#ifdef OPENGL
#define SV_POSITION POSITION
#define VS_PROFILE vs_3_0
#define PS_PROFILE ps_3_0
#else
#define VS_PROFILE vs_4_0_level_9_1
#define PS_PROFILE ps_4_0_level_9_1
#endif
float4x4 MatrixTransform;
float Seed;
struct Input{float4 Position:POSITION0;float4 Color:COLOR0;float2 Tex:TEXCOORD0;};
struct Output{float4 Position:SV_POSITION;float4 Color:COLOR0;float2 Tex:TEXCOORD0;};
Output VS(Input i){Output o;o.Position=mul(i.Position,MatrixTransform);o.Color=i.Color;o.Tex=i.Tex;return o;}
float4 PS(Output i):COLOR0 {
 float2 p=i.Tex*2-1;float a=atan2(p.y,p.x);float r=length(p);float edge=.48+.06*sin(a*5+Seed)+.055*sin(a*9-Seed)+.25*pow(saturate(sin(a*13+Seed)),18);
 float mask=1-smoothstep(edge-.012,edge,r);float noise=sin(p.x*95+sin(p.y*47))*sin(p.y*61)*.008;float wet=saturate(1-r/max(.01,edge));float highlight=pow(saturate(1-length((p+float2(.15,.18))*2.8)),5)*.55;float rim=smoothstep(.055,.005,abs(r-edge+.035))*.13;
 float3 color=i.Color.rgb*(.78+wet*.22+noise)+highlight+rim;float alpha=mask*i.Color.a;return float4(color*alpha,alpha);
}
technique Main{pass P0{VertexShader=compile VS_PROFILE VS();PixelShader=compile PS_PROFILE PS();}}
