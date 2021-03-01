#ifndef MYRP_DEFERREDSHADING_INCLUDED
#define MYRP_DEFERREDSHADING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld, unity_WorldToObject;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

UNITY_DEFINE_INSTANCED_PROP(float4, _Color);
UNITY_DEFINE_INSTANCED_PROP(float, _Metallic);
UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness);

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject

struct VertexInput
{
	float4 pos : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
	float4 pos : SV_POSITION;
	float4 mPos : TEXCOORD0;
	float3 normal : TEXCOORD1;
	float2 uv : TEXCOORD2;

	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct PixelOutput {
	float4 mPos : COLOR0;
	float4 normal : COLOR1;
	float4 color : COLOR2;
	float4 light : COLOR3;
};

VertexOutput DeferredShadingPassVertex(VertexInput input)
{
	VertexOutput output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);

	float3 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0)).xyz;
	float3 worldNormal = normalize(mul(input.normal, (float3x3)UNITY_MATRIX_I_M));
	output.pos = mul(unity_MatrixVP,  float4(worldPos, 1.0));

	output.mPos =  float4(worldPos, 1.0);
	output.normal = worldNormal;
	output.uv = TRANSFORM_TEX(input.uv, _MainTex);
	return output;
}

PixelOutput DeferredShadingPassFragment(VertexOutput input)
{
	UNITY_SETUP_INSTANCE_ID(input);

	PixelOutput o;
	o.mPos = input.mPos;
	o.normal = float4((input.normal + 1) * 0.5,1.0);
	o.color = _Color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
	o.light = float4(_Metallic, _Smoothness, 0, 0);
	return o;
}

#endif //MYRP_DEFERREDSHADING_INCLUDED