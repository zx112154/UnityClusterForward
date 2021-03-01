#ifndef MYRP_DEPTH_ONLY_INCLUDED
#define MYRP_DEPTH_ONLY_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
#include "Lighting.hlsl"

#define MAX_VISIBLE_LIGHTS 16

CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld, unity_WorldToObject;

float4 unity_LightData;
float4 unity_LightIndices[2];

float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];
float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS];
float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];

float4 unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax;
float4 unity_SpecCube0_ProbePosition, unity_SpecCube0_HDR;
float4 unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax;
float4 unity_SpecCube1_ProbePosition, unity_SpecCube1_HDR;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float _Cutoff;
CBUFFER_END

//œ‡ª˙Œª÷√
CBUFFER_START(UnityPerCamera)
float3 _WorldSpaceCameraPos;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);


#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

UNITY_INSTANCING_BUFFER_START(PerInstance)
UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(PerInstance)

struct VertexInput
{
	float4 pos : POSITION;
	float2 uv : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
	float4 clipPos : SV_POSITION;
	float2 uv : TEXCOORD3;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

VertexOutput DepthOnlyPassVertex(VertexInput input)
{
	VertexOutput output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	
	float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
	output.clipPos = mul(unity_MatrixVP, worldPos);
	output.uv = TRANSFORM_TEX(input.uv, _MainTex);

	return output;
}

float4 DepthOnlyPassFragment(VertexOutput input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	float4 albedoAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
	albedoAlpha *= UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color);

#if defined(_CLIPPING_ON)
	clip(albedoAlpha.a - _Cutoff);
#endif

	return 0;
}

#endif //MYRP_DEPTH_ONLY_INCLUDED