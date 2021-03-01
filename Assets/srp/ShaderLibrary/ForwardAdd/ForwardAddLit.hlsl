#ifndef FORWARDADD_LIT_HLSL
#define FORWARDADD_LIT_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "../Lighting.hlsl"

CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld;
CBUFFER_END

CBUFFER_START(UnityPerCamera)
float3 _WorldSpaceCameraPos;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
sampler2D _MainTex;
float4 _MainTex_ST;
sampler2D _BumpTex;
float4 _BumpTex_ST;
sampler2D _SpecTex;
float4 _SpecTex_ST;
float _Roughness;
float3 _Frenel;
float _Metallic;
float4 _Color;
float _Aniso;
CBUFFER_END

#define UNITY_MATRIX_M unity_ObjectToWorld

struct SLight
{
	float3 pos;
	float3 direction;
	float4 color;
	float spotAngle;
	float range;
	uint type;
};

#define LIGHT_TYPE_SPOT			0
#define LIGHT_TYPE_DIRECTIONAL	1
#define LIGHT_TYPE_POINT		2

StructuredBuffer<SLight> g_lights;
StructuredBuffer<uint> g_lightIndexList;
StructuredBuffer<uint2> g_lightGrid;

uint3 cb_clusterSize;
uint3 cb_clusterCount;

struct SVertexInput
{
	float3 pos : POSITION;
	float2 uv : TEXCOORD0;
	float4 tangent : TANGENT;
	float3 normal : NORMAL;
};

struct SVertexOutput
{
	float4 clipPos	: SV_POSITION;
	float4 uv		: TEXCOORD0;
	float4 normal	: TEXCOORD1;
	float4 tangent	: TEXCOORD2;
	float4 bTangent	: TEXCOORD3;
	float3 worldPos : TEXCOORD4;
};

//计算分组编号
uint3 computeClusterIndex(uint3 _clusterIndex3D)
{
	return _clusterIndex3D.x + (cb_clusterCount.x * (_clusterIndex3D.y + cb_clusterCount.y * _clusterIndex3D.z));
}

SVertexOutput ForwardAddLitVertex(SVertexInput _input)
{
	SVertexOutput output;

	output.worldPos = mul(UNITY_MATRIX_M, float4(_input.pos, 1.0)).xyz;
	output.clipPos = mul(unity_MatrixVP, float4(output.worldPos, 1.0));

	output.uv.xy = _input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
	output.uv.zw = _input.uv.xy * _BumpTex_ST.xy + _BumpTex_ST.zw;

	float3x3 mTw = (float3x3)UNITY_MATRIX_M;

	float3 wNormal = normalize(mul(mTw, _input.normal)).xyz;
	float3 wTangent = normalize(mul(mTw, _input.tangent.xyz)).xyz;
	float3 wBinTangent = cross(wNormal, wTangent) * _input.tangent.w;

	output.normal = float4(wTangent.x, wBinTangent.x, wNormal.x, output.worldPos.x);
	output.tangent = float4(wTangent.y, wBinTangent.y, wNormal.y, output.worldPos.y);
	output.bTangent = float4(wTangent.z, wBinTangent.z, wNormal.z, output.worldPos.z);

	return output;
}



float4 ForwardAddLitFrag(SVertexOutput _input) : SV_Target
{
	float3 tex = tex2D(_MainTex, _input.uv);
	float3 spec = tex2D(_SpecTex, _input.uv);

	float3 viewDir = normalize(_WorldSpaceCameraPos - _input.worldPos);

	//Get normal in tangent space
	float3 normal = UnpackNormalmapRGorAG(tex2D(_BumpTex, _input.uv.zw));
	normal = normalize(float3(dot(_input.normal.xyz, normal), dot(_input.tangent.xyz, normal), dot(_input.bTangent.xyz, normal)));

	uint3 screenPos = _input.clipPos.xyw;
	screenPos = screenPos / cb_clusterSize;	//计算当前像素处于哪个分组

	uint index = computeClusterIndex(screenPos);

	uint2 lightGrid = g_lightGrid[index];
	float4 color = float4(0, 0, 0, 1);

	SPBRParams params;
	params.albedo = tex * _Color.xyz;
	params.normal = normal;
	params.viewDir = viewDir;
	params.roughness = _Roughness;
	params.f0 = _Frenel;
	params.metallic = _Metallic;
	params.specColor = spec;

	//lightGrid(光源序列, 光源数量)
	for (uint i = 0; i < lightGrid.y; ++i)
	{
		uint lightIndex = g_lightIndexList[lightGrid.x + i];
		SLight light = g_lights[lightIndex];
		switch (light.type)
		{
		case LIGHT_TYPE_DIRECTIONAL:
		{
			params.lightDir = -light.direction;
			params.radiance = light.color.xyz;

			color.xyz += BrdfLight(params);
		}break;
		case LIGHT_TYPE_SPOT:
		{
			float3 lightPos = light.pos;
			float radius = light.range;
			float3 tolight = normalize(lightPos - _input.worldPos);
			float3 lightDir = normalize(light.direction);
			float disToLight = distance(lightPos, _input.worldPos);
			float cosAngle = dot(-tolight, lightDir);

			float outer = 0.5 * light.spotAngle;
			float inner = 0.5 * light.spotAngle * 0.4;
			float cosOuter = cos(radians(outer));
			float cosInner = cos(radians(inner));

			float factor = (1 - saturate(disToLight / radius)) * saturate((cosAngle - cosOuter) / cosInner);

			params.lightDir = lightDir;
			params.radiance = light.color.xyz * factor;

			color.xyz += BrdfLight(params);
		}break;
		case LIGHT_TYPE_POINT:
		{
			float3 lightPos = light.pos;
			float radius = light.range;
			float3 lightDir = normalize(light.pos - _input.worldPos);

			float disToLight = distance(lightPos, _input.worldPos);
			float factor = 1 - saturate(disToLight / radius);

			params.lightDir = lightDir;
			params.radiance = light.color.xyz * factor;

			color.xyz += BrdfLight(params);
		}break;

		}
	}

	return float4(color.xyz, 1.0);
}

//各向异性材质
float4 ForwardAddLitAnisFrag(SVertexOutput _input) : SV_Target
{
	float3 tex = tex2D(_MainTex, _input.uv);
	float3 spec = tex2D(_SpecTex, _input.uv);

	float3 viewDir = normalize(_WorldSpaceCameraPos - _input.worldPos);

	//Get normal in tangent space
	float3 normal = UnpackNormalmapRGorAG(tex2D(_BumpTex, _input.uv.zw));
	normal = normalize(float3(dot(_input.normal.xyz, normal), dot(_input.tangent.xyz, normal), dot(_input.bTangent.xyz, normal)));

	uint3 screenPos = _input.clipPos.xyw;
	screenPos = screenPos / cb_clusterSize;	//计算当前像素处于哪个分组

	uint index = computeClusterIndex(screenPos);

	uint2 lightGrid = g_lightGrid[index];
	float4 color = float4(0, 0, 0, 1);

	SPBRAnisParams params;
	params.albedo = tex * _Color.xyz;
	params.normal = normal;
	params.tangent = _input.tangent.xyz;
	params.viewDir = viewDir;
	params.roughness = _Roughness;
	params.f0 = _Frenel;
	params.metallic = _Metallic;
	params.specColor = spec;
	params.kAniso = _Aniso;

	//lightGrid(光源序列, 光源数量)
	for (uint i = 0; i < lightGrid.y; ++i)
	{
		uint lightIndex = g_lightIndexList[lightGrid.x + i];
		SLight light = g_lights[lightIndex];
		switch (light.type)
		{
		case LIGHT_TYPE_DIRECTIONAL:
		{
			params.lightDir = -light.direction;
			params.radiance = light.color.xyz;

			color.xyz += BrdfCookTorrance_Anis(params);
		}break;
		case LIGHT_TYPE_SPOT:
		{
			float3 lightPos = light.pos;
			float radius = light.range;
			float3 tolight = normalize(lightPos - _input.worldPos);
			float3 lightDir = normalize(light.direction);
			float disToLight = distance(lightPos, _input.worldPos);
			float cosAngle = dot(-tolight, lightDir);

			float outer = 0.5 * light.spotAngle;
			float inner = 0.5 * light.spotAngle * 0.4;
			float cosOuter = cos(radians(outer));
			float cosInner = cos(radians(inner));

			float factor = (1 - saturate(disToLight / radius)) * saturate((cosAngle - cosOuter) / cosInner);

			params.lightDir = lightDir;
			params.radiance = light.color.xyz * factor;

			color.xyz += BrdfCookTorrance_Anis(params);
		}break;
		case LIGHT_TYPE_POINT:
		{
			float3 lightPos = light.pos;
			float radius = light.range;
			float3 lightDir = normalize(light.pos - _input.worldPos);

			float disToLight = distance(lightPos, _input.worldPos);
			float factor = 1 - saturate(disToLight / radius);

			params.lightDir = lightDir;
			params.radiance = light.color.xyz * factor;

			color.xyz += BrdfCookTorrance_Anis(params);
		}break;

		}
	}

	return float4(color.xyz, 1.0);
}

#endif