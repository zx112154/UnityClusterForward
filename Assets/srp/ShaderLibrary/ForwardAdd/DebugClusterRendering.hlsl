#ifndef DEBUG_CLUSTER_REDNERING_INCLUDED
#define DEBUG_CLUSTER_REDNERING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld;
CBUFFER_END

#define UNITY_MATRIX_M unity_ObjectToWorld

struct SPlane
{
	float3 n;
	float d;
};

struct SFrustum
{
	SPlane planes[4];
	float2 nearFar;
};

float4x4 _CameraWorldMatrix;
uint3 cb_clusterSize;
StructuredBuffer<SFrustum> ClusterAABBs;

struct SVertexInput
{
	float3 pos : POSITION;
};

struct SVertexOutput
{
	float3 clipPos : SV_POSITION;
};

uint3 computeClusterIndex(uint3 clusterIndex3D)
{
	return clusterIndex3D.x + (cb_clusterCount.x * (clusterIndex3D.y + cb_clusterCount.y * clusterIndex3D.z));
}

SVertexOutput DebugCRVertex(SVertexInput _input)
{
	SVertexOutput output;

	float3 worldPos = mul(UNITY_MATRIX_M, float4(_input.pos, 1.0)).xyz;
	uint3 screenPos = mul(unity_MatrixVP, float4(worldPos, 1.0)).xyw;
	screenPos = screenPos / cb_clusterSize;
	uint index = computeClusterIndex(screenPos);
	SFrustum frustum = ClusterAABBs[index];
}

float4 DebugCRFrag(SVertexOutput _input)
{

}

#endif