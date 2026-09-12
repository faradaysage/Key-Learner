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
float2 Texel;
float3 C0,C1,C2,C3;
struct Input {float4 Position:POSITION0;float4 Color:COLOR0;float2 Tex:TEXCOORD0;};
struct Output {float4 Position:SV_POSITION;float4 Color:COLOR0;float2 Tex:TEXCOORD0;};
Output VS(Input i){Output o;o.Position=mul(i.Position,MatrixTransform);o.Color=i.Color;o.Tex=i.Tex;return o;}
float4 PS(Output i):COLOR0 {
 float4 sample=tex2D(Source,i.Tex);float d=sample.a;float3 c=sample.rgb/max(d,.001);float3 rim=C1;float best=dot(c-C0,c-C0);float dist=dot(c-C1,c-C1);if(dist<best){best=dist;rim=C2;}dist=dot(c-C2,c-C2);if(dist<best){best=dist;rim=C3;}dist=dot(c-C3,c-C3);if(dist<best)rim=C0;
 float surface=smoothstep(.12,.23,d);float core=smoothstep(.48,.64,d);float dx=tex2D(Source,i.Tex+float2(Texel.x,0)).a-tex2D(Source,i.Tex-float2(Texel.x,0)).a;float dy=tex2D(Source,i.Tex+float2(0,Texel.y)).a-tex2D(Source,i.Tex-float2(0,Texel.y)).a;
 float3 n=normalize(float3(-dx*12,-dy*12,1));float light=.65+.35*saturate(dot(n,normalize(float3(-.6,-.7,1))));float spec=pow(saturate(dot(n,normalize(float3(-.3,-.45,1)))),36)*.6;float3 color=lerp(rim,c,core)*light+spec*core;
 float alpha=saturate(d*.08+surface*.92);return float4(color*alpha,alpha);
}
technique Main {pass P0 {VertexShader=compile VS_PROFILE VS();PixelShader=compile PS_PROFILE PS();}}
