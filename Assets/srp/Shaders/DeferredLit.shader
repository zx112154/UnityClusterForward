Shader "MyPipeline/DeferredLit"
{

	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_MainTex("Albedo & Alpha", 2D) = "white" {}
		_Metallic("Metallic", Range(0, 1)) = 0
		_Smoothness("Smoothness", Range(0, 1)) = 0.5
	}

	SubShader
	{
		Pass
		{
			Tags{
				"LightMode" = "DeferredShadingData"
			}
			HLSLPROGRAM
			#include "../ShaderLibrary/DeferredShading.hlsl"
			#pragma target 3.5
			#pragma multi_compile_instancing
			#pragma vertex DeferredShadingPassVertex
			#pragma fragment DeferredShadingPassFragment
			ENDHLSL
		}

		Pass
		{
			Blend srcAlpha OneMinusSrcAlpha
			HLSLPROGRAM
			#include "../ShaderLibrary/DeferredLit.hlsl"
			#pragma target 3.5
			#pragma multi_compile_instancing
			#pragma vertex DeferredLitPassVertex
			#pragma fragment DeferredLitPassFragment
			ENDHLSL
		}

	}
}
