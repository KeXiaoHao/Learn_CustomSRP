Shader "Hidden/Custom RP/Post FX Stack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "../Shaders/PostFXStackPasses.hlsl"
        ENDHLSL

        Pass
        {
	        Name "Bloom Horizontal"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomHorizontalPassFragment
        	ENDHLSL
        }
        
        Pass
        {
	        Name "Bloom Vertical"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomVerticalPassFragment
        	ENDHLSL
        }
    	
    	Pass
        {
	        Name "Bloom Combine"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomCombinePassFragment
        	ENDHLSL
        }
    	
    	Pass
        {
	        Name "Bloom Scatter"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomScatterPassFragment
        	ENDHLSL
        }
    	
    	Pass
        {
	        Name "Bloom Prefilter"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomPrefilterPassFragment
        	ENDHLSL
        }
    	
    	Pass
        {
	        Name "Bloom PrefilterFireflies"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomPrefilterFirefliesPassFragment
        	ENDHLSL
        }
    	
    	Pass
        {
	        Name "Bloom BloomScatterFinal"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomScatterFinalPassFragment
        	ENDHLSL
        }
    	
    	Pass
        {
            Name "Copy"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
            ENDHLSL
        }
    	
    	Pass
        {
            Name "ToneMappingACES"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ToneMappingACESPassFragment
            ENDHLSL
        }
    	
        Pass
        {
	        Name "ToneMappingNeutral"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ToneMappingNeutralPassFragment
        	ENDHLSL
        }
    	
    	Pass
        {
	        Name "ToneMappingReinhard"

	        HLSLPROGRAM
        	#pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ToneMappingReinhardPassFragment
        	ENDHLSL
        }

    }
}