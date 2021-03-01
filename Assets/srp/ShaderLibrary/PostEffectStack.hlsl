#ifndef MYRP_POST_EFFECT_STACK_INCLUDED
#define MYRP_POST_EFFECT_STACK_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

TEXTURE2D(_DepthTex);
SAMPLER(sampler_DepthTex);

TEXTURE2D(_BrightTex);
SAMPLER(sampler_BrightTex);

float4 _ProjectionParams;

float4 _ZBufferParams;

float _ReinhardModifier;

static const float GaussWeight[21] =
{
	0.000272337, 0.00089296, 0.002583865, 0.00659813, 0.014869116,
	 0.029570767, 0.051898313, 0.080381679, 0.109868729, 0.132526984,
	 0.14107424,
	 0.132526984, 0.109868729, 0.080381679, 0.051898313, 0.029570767,
	0.014869116, 0.00659813, 0.002583865, 0.00089296, 0.000272337
};

struct VertexInput {
	float4 pos : POSITION;
};

struct VertexOutput {
	float4 clipPos : SV_POSITION;
	float2 uv : TEXCOORD0;
};

//模糊采样
float4 BlurSample(float2 uv, float uOffset = 0.0, float vOffset = 0.0)
{
	float2 offset = float2(uOffset * ddx(uv.x),	vOffset * ddy(uv.y));
	//ddx(uv.x) 相邻像素的UV的X的差值
	float3 result;
	for (int i = 0; i < 21; i++)
	{
		result += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset * (i - 10)).rgb * GaussWeight[i];
	}

	return float4(result, 1.0);
}

VertexOutput DefaultPassVertex(VertexInput input)
{
	VertexOutput output;
	output.clipPos = float4(input.pos.xy, 0.0, 1.0);
	output.uv = input.pos.xy * 0.5 + 0.5;
	if (_ProjectionParams.x < 0.0)
		output.uv.y = 1.0 - output.uv.y;

	return output;
}

float4 CopyPassFragment(VertexOutput input) : SV_TARGET
{
	return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
}

float4 DepthStripesPassFragment(VertexOutput input) : SV_TARGET
{
	float rawDepth = SAMPLE_DEPTH_TEXTURE(_DepthTex, sampler_DepthTex, input.uv);
	//转换得到深度到近平面的坐标
	float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
	float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

//处理天空盒
#if UNITY_REVERSED_Z
	bool hasDepth = rawDepth != 0;
#else
	bool hasDepth = rawDepth != 1;
#endif

	if (hasDepth)
		color *= pow(sin(3.14 * depth), 2.0);

	return color;
}

float4 BlurPassFragment(VertexOutput input) : SV_TARGET
{
	float4 color = BlurSample(input.uv, 1, 1);

	return float4(color.rgb, 1);
}

float4 ToneMappingPassFragment(VertexOutput input) : SV_TARGET
{
	float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
	color *= (1 + color * _ReinhardModifier) / (1 + color);
	return float4(saturate(color), 1);
}

float4 BrightPassFragment(VertexOutput input) : SV_TARGET
{
	float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
	float bright = dot(color, float3(0.2126, 0.7152, 0.0722));
	float4 final = bright > 1.0 ? float4(color, 1.0f) : float4(0, 0, 0, 1.0f);
	return final;
}

float4 TexAddPassVertex(VertexOutput input) : SV_TARGET
{
	float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
	float3 brightCol = SAMPLE_TEXTURE2D(_BrightTex, sampler_BrightTex, input.uv).rgb;
	color += brightCol;

	//tone mapping
	color *= (1 + color * _ReinhardModifier) / (1 + color);

	return float4(saturate(color), 1.0);
}


#endif //MYRP_POST_EFFECT_STACK_INCLUDED