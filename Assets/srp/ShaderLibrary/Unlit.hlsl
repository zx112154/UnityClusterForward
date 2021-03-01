#ifndef MYRP_UNLIT_INCLUDED
#define MYRP_UNLIT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld;
CBUFFER_END

#define UNITY_MATRIX_M unity_ObjectToWorld
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

float4 _Color;

struct VertexInput
{
	float4 pos : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
	float4 clipPos : SV_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};


VertexOutput UnlitPassVertex(VertexInput input)
{
	VertexOutput output;
	float4 wPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
	output.clipPos = mul(unity_MatrixVP, wPos);
	return output;
}

float4 UnlitPassFragment(VertexOutput input) : SV_Target
{
	return _Color;
}

#endif //MYRP_UNLIT_INCLUDED