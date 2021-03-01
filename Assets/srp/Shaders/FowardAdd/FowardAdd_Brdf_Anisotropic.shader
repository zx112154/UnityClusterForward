Shader "ForwardAdd/Brdf_Anisotropic"
{
	Properties
	{
		_Color("Main Color", Color) = (1, 1, 1, 1)
		_MainTex("Texture", 2D) = "white" {}
		[Normal]_BumpTex("Normal", 2D) = "bump" {}
		[Specular]_SpecTex("Specular Tex", 2D) = "white"{}
		_Roughness("Roughness", Range(0, 1)) = 0.2
		_Frenel("Frenel", Color) = (1, 1, 1, 1)
		_Metallic("Metallic", Range(0, 1)) = 0.78
		_Aniso("Anisotropic", Range(0, 1)) = 0.78
	}
		SubShader
		{
			Pass
			{
				Name "ForwardAddLit"
				Tags{"LightMode" = "ForwardAddLit"}
				HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex ForwardAddLitVertex
				#pragma fragment ForwardAddLitAnisFrag
				#pragma multi_compile_instancing
				#pragma instance_options assumeuniformscaling
				#include "../../ShaderLibrary/ForwardAdd/ForwardAddLit.hlsl"
				ENDHLSL
			}

			Pass
			{
				Name "SceneInfo"
				Tags{"LightMode" = "SceneInfo"}
				HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex SceneInfoVertex
				#pragma fragment SceneInfoFrag
				#pragma multi_compile_instancing
				#pragma instance_options assumeuniformscaling
				#include "../../ShaderLibrary/ForwardAdd/SceneInfo.hlsl"
				ENDHLSL
			}
		}
}
