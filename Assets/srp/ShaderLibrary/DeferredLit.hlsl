#ifndef MYRP_DEFERREDLIT_INCLUDED
#define MYRP_DEFERREDLIT_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Lighting.hlsl"

#define MAX_VISIBLE_LIGHTS 8

CBUFFER_START(UnityPerFrame)
float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
float4x4 unity_ObjectToWorld;

int _VisibleLightCount;

float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
float4 _VisibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];
float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS];
float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
CBUFFER_END

CBUFFER_START(UnityPerCamera)
float4 _WorldSpaceCameraPos;
CBUFFER_END

TEXTURE2D(_SrceenTex);
SAMPLER(sampler_SrceenTex);

TEXTURE2D(_PosTex);
SAMPLER(sampler_PosTex);

TEXTURE2D(_NormalTex);
SAMPLER(sampler_NormalTex);

TEXTURE2D(_AlbedoSpecTex);
SAMPLER(sampler_AlbedoSpecTex);

TEXTURE2D(_LightParamTex);
SAMPLER(sampler_LightParamTex);

#define UNITY_MATRIX_M unity_ObjectToWorld

struct VertexInput
{
	float4 pos : POSITION;
};

struct VertexOutput
{
	float4 clipPos : SV_POSITION;
	float2 uv : TEXCOORD0;
};

VertexOutput DeferredLitPassVertex(VertexInput input)
{
	VertexOutput output;
	output.clipPos = float4(input.pos.xy, 0.0, 1.0);
	output.uv = input.pos.xy * 0.5 + 0.5;
	return output;
}

float3 GenericLight(int index, LitSurface s)
{
	float3 lightColor = _VisibleLightColors[index].rgb;
	float4 lightDirectionOrPosition = _VisibleLightDirectionsOrPositions[index];
	float4 lightAttenuation = _VisibleLightAttenuations[index];
	float3 spotDirection = _VisibleLightSpotDirections[index].xyz;

	float3 lightVector = lightDirectionOrPosition.xyz - s.position * lightDirectionOrPosition.w;
	float3 lightDir = normalize(lightVector);
	float3 color = LightSurface(s, lightDir);

	float rangeFade = dot(lightVector, lightVector) * lightAttenuation.x;
	rangeFade = saturate(1.0 - rangeFade * rangeFade);
	rangeFade *= rangeFade;

	//float spotFade = dot(spotDirection, lightDir);
	//spotFade = saturate(spotFade *lightAttenuation.z + lightAttenuation.w);
	//spotFade *= spotFade;

	float distanceSqr = max(dot(lightVector, lightVector), 0.00001);
	color *= rangeFade / distanceSqr;

	return color * lightColor;
}

float4 DeferredLitPassFragment(VertexOutput input) : SV_TARGET
{
	float3 pos = SAMPLE_TEXTURE2D(_PosTex, sampler_PosTex, input.uv).xyz;
	float3 normal = SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, input.uv).xyz;
	float4 color = SAMPLE_TEXTURE2D(_AlbedoSpecTex, sampler_AlbedoSpecTex, input.uv);
	float4 lParam = SAMPLE_TEXTURE2D(_LightParamTex, sampler_LightParamTex, input.uv);

	normal = normalize(normal * 2 - 1);

	float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - pos);

	LitSurface surface = GetLitSurface(normal, pos, viewDir, color.rgb, lParam.r, lParam.g, true);

	float3 lightColor;

	for (int i = 0; i < min(_VisibleLightCount, MAX_VISIBLE_LIGHTS); i++)
	{
		lightColor += GenericLight(i, surface);
	}

	return float4(lightColor, color.a);
}

#endif //MYRP_DEFERREDLIT_INCLUDED