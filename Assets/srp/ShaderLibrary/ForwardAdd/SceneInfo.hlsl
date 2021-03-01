#ifndef SCENEINFO_INCLUDED
#define SCENEINFO_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
float4 _ProjectionParams;
float4 _ZBufferParams;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_WorldToCamera;
float4x4 unity_CameraToWorld;

sampler2D _MainTex;
float4 _MainTex_ST;
sampler2D _BumpTex;
float4 _BumpTex_ST;
CBUFFER_END

struct SVertexInput
{
	float3 pos		:	POSITION;
	float3 normal	:	NORMAL;
	float4 tangent	:	TANGENT;
	float2 uv		:	TEXCOORD0;
};

struct SVertexOutput
{
	float4 clipPos  : SV_POSITION;
	float4 uv		: TEXCOORD0;
	float4 vPos		: TEXCOORD1;
	float4 normal	: TEXCOORD2;
	float4 tangent	: TEXCOORD3;
	float4 bTangent	: TEXCOORD4;
};

struct SFragmentOutput
{
	float4 gBuffer0 : SV_Target0;
};

SVertexOutput SceneInfoVertex(SVertexInput _input)
{
	SVertexOutput output;

	output.uv.xy = _input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
	output.uv.zw = _input.uv.xy * _BumpTex_ST.xy + _BumpTex_ST.zw;
	float4 worldPos = mul(unity_ObjectToWorld, float4(_input.pos, 1.0));

	output.vPos = mul(unity_WorldToCamera, worldPos);
	output.clipPos = mul(unity_MatrixVP, worldPos);

	float3x3 mTw = (float3x3)unity_ObjectToWorld;

	float3 wNormal = normalize(mul(mTw, _input.normal.xyz));
	float3 wTangent = normalize(mul(mTw, _input.tangent.xyz));
	float3 wBinTangent = cross(wNormal, wTangent) * _input.tangent.w;

	output.normal = float4(wTangent.x, wBinTangent.x, wNormal.x, worldPos.x);
	output.tangent = float4(wTangent.y, wBinTangent.y, wNormal.y, worldPos.y);
	output.bTangent = float4(wTangent.z, wBinTangent.z, wNormal.z, worldPos.z);

	return output;
}

SFragmentOutput SceneInfoFrag(SVertexOutput _input) : SV_Target
{
	SFragmentOutput sceneInfo;

	float3 normal = UnpackNormalmapRGorAG(tex2D(_BumpTex, _input.uv.zw));
	normal = normalize(float3(dot(_input.normal.xyz, normal), dot(_input.tangent.xyz, normal), dot(_input.bTangent.xyz, normal)));

	sceneInfo.gBuffer0 = normalize(mul(float4(normal, 1.0), unity_CameraToWorld));
	sceneInfo.gBuffer0.z = -sceneInfo.gBuffer0.z;
	sceneInfo.gBuffer0.xyz = PackNormalMaxComponent(sceneInfo.gBuffer0.xyz);
	return sceneInfo;
}

#endif