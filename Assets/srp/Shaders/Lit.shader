Shader "MyPipeline/Lit"
{

	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_MainTex("Albedo & Alpha", 2D) = "white" {}
		[KeywordEnum(Off, On, Shadows)] _Clipping("Alpha Clipping", Float) = 0
		_Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
		_Metallic("Metallic", Range(0, 1)) = 0
		_Smoothness("Smoothness", Range(0, 1)) = 0.5
		[Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
		[Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("Receive Shadows", Float) = 1
		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha("Premultiply Alpha", Float) = 0
	}

		SubShader
		{
			Pass
			{
				Blend[_SrcBlend][_DstBlend]
				Cull[_Cull]
				ZWrite[_ZWrite]
				HLSLPROGRAM
				#include "../ShaderLibrary/Lit.hlsl"
				#pragma target 3.5
				#pragma multi_compile_instancing
				#pragma shader_feature _CLIPPING_ON
				#pragma shader_feature _RECEIVE_SHADOWS
				#pragma multi_compile _ _CASCADED_SHADOWS_HARD _CASCADED_SHADOWS_SOFT
				#pragma multi_compile _ _SHADOWS_HARD
				#pragma multi_compile _ _SHADOWS_SOFT
				#pragma multi_compile _ LIGHTMAP_ON
				#pragma vertex LitPassVertex
				#pragma fragment LitPassFragment
				ENDHLSL
			}
			Pass
			{
				Tags{
					"LightMode" = "ShadowCaster"
				}
				Cull[_Cull]
				HLSLPROGRAM
				#include "../ShaderLibrary/ShadowCaster.hlsl"
				#pragma target 3.5
				#pragma multi_compile_instancing
				#pragma shader_feature _CLIPPING_OFF
				#pragma vertex ShadowCasterPassVertex
				#pragma fragment ShadowCasterPassFragment
				ENDHLSL
			}
			
			Pass
			{
				Tags{
						"LightMode" = "DepthOnly"
				}

				ColorMask 0
				Cull[_Cull]
				ZWrite On

				HLSLPROGRAM
				#pragma target 3.5
				#pragma multi_compile_instancing

				#pragma shader_feature _CLIPPING_ON		
				#pragma multi_compile _ LOD_FADE_CROSSFADE

				#pragma vertex DepthOnlyPassVertex
				#pragma fragment DepthOnlyPassFragment

				#include "../ShaderLibrary/DepthOnly.hlsl"

				ENDHLSL
			}
		}

			CustomEditor "LitShaderGUI"
}
