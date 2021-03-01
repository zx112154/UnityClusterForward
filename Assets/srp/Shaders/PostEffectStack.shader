Shader "Hidden/My Pipeline/PostEffectStack"
{
	SubShader
	{
		Cull Off
		ZTest Always
		ZWrite Off

		HLSLINCLUDE
		#include "../ShaderLibrary/PostEffectStack.hlsl"	
		ENDHLSL

		Pass
		{
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment CopyPassFragment
			ENDHLSL
		}

		Pass
		{
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment BlurPassFragment
			ENDHLSL
		}

		Pass
		{
			//2 DepthStripes
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment DepthStripesPassFragment
			ENDHLSL
		}

		Pass{	//3 ToneMapping
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment ToneMappingPassFragment
			ENDHLSL
		}

		Pass{	//4 Extract Bright
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment BrightPassFragment
			ENDHLSL
		}

		Pass{	//5 Tex Add
			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex DefaultPassVertex
			#pragma fragment TexAddPassVertex
			ENDHLSL
		}
	}
}
