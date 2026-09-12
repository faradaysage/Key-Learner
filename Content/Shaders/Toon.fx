#ifdef OPENGL
#define SV_POSITION POSITION
#define VS_PROFILE vs_3_0
#define PS_PROFILE ps_3_0
#else
#define VS_PROFILE vs_4_0_level_9_1
#define PS_PROFILE ps_4_0_level_9_1
#endif
float4x4 WorldViewProjection;
float4x4 NormalMatrix;
float3 Tint;
float Alpha;
float OutlineWidth;
struct Input {float4 Position:POSITION0;float3 Normal:NORMAL0;float2 UV:TEXCOORD0;};
struct Output {float4 Position:SV_POSITION;float3 Normal:TEXCOORD0;};
Output VS(Input i){Output o;o.Position=mul(i.Position,WorldViewProjection);o.Normal=mul(float4(i.Normal,0),NormalMatrix).xyz;return o;}
Output OutlineVS(Input i){i.Position.xy*=1+OutlineWidth*.025; i.Position.z*=1.04;return VS(i);}
float4 ToonPS(Output i):COLOR0 {
 float3 n=normalize(i.Normal);float light=dot(n,normalize(float3(-.45,-.65,1)));
 float band=light<.05?.36:light<.55?.68:1;
 float highlight=step(.985,dot(n,normalize(float3(-.2,-.3,1))))*.12;
 return float4(saturate(Tint*band+highlight)*Alpha,Alpha);
}
float4 OutlinePS(Output i):COLOR0{return float4(float3(.025,.035,.055)*Alpha,Alpha);}
technique Outline {pass P0 {VertexShader=compile VS_PROFILE OutlineVS();PixelShader=compile PS_PROFILE OutlinePS();}}
technique Toon {pass P0 {VertexShader=compile VS_PROFILE VS();PixelShader=compile PS_PROFILE ToonPS();}}
