Shader "Custom RP/Particles/Unlit"
{
    Properties
    {
        [HDR]_BaseMap ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)]_Clipping ("Alpha Clipping", float) = 0
        [HDR]_BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [Toggle(_VERTEX_COLORS)] _VertexColors ("Vertex Colors", Float) = 0
        [Toggle(_FLIPBOOK_BLENDING)] _FlipbookBlending ("Flipbook Blending", Float) = 0
        [Toggle(_NEAR_FADE)] _NearFade ("Near Fade", Float) = 0
		_NearFadeDistance ("Near Fade Distance", Range(0.0, 10.0)) = 1
		_NearFadeRange ("Near Fade Range", Range(0.01, 10.0)) = 1
        [Toggle(_SOFT_PARTICLES)] _SoftParticles ("Soft Particles", Float) = 0
		_SoftParticlesDistance ("Soft Particles Distance", Range(0.0, 10.0)) = 0
		_SoftParticlesRange ("Soft Particles Range", Range(0.01, 10.0)) = 1
        [Toggle(_DISTORTION)] _Distortion ("Distortion", Float) = 0
		[NoScaleOffset] _DistortionMap("Distortion Vectors", 2D) = "bumb" {}
		_DistortionStrength("Distortion Strength", Range(0.0, 0.2)) = 0.1
        _DistortionBlend("Distortion Blend", Range(0.0, 1.0)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend ("Src Blend", float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend ("Dst Blend", float) = 0
        [Enum(Off, 0, On, 1)]_ZWrite ("Z Write", float) = 1
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "UnlitInput.hlsl"
        ENDHLSL
        
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            
            HLSLPROGRAM
            //不生成OpenGL ES 2.0等图形API的着色器变体，其不支持可变次数的循环与线性颜色空间
            #pragma target 3.5
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            
            #pragma multi_compile_instancing
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _VERTEX_COLORS
            #pragma shader_feature _FLIPBOOK_BLENDING
            #pragma shader_feature _NEAR_FADE
            #pragma shader_feature _SOFT_PARTICLES
            #pragma shader_feature _DISTORTION
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}
