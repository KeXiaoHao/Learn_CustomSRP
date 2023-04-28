Shader "CustomRP/Lit"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _BaseMap ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)]_Clipping ("Alpha Clipping", float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
        _Metallic ("Metallic", range(0, 1)) = 0
        _Smoothness ("Smoothness", range(0, 1)) = 0.5
        [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
        
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend ("Src Blend", float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend ("Dst Blend", float) = 0
        [Enum(Off, 0, On, 1)]_ZWrite ("Z Write", float) = 1
    }
    SubShader
    {
        Pass
        {
            Tags { "LightMode" = "CustomLit"}
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            
            HLSLPROGRAM
            //不生成OpenGL ES 2.0等图形API的着色器变体，其不支持可变次数的循环与线性颜色空间
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            
            #pragma multi_compile_instancing

            #pragma shader_feature _RECEIVE_SHADOWS
            // #pragma shader_feature _CLIPPING
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma shader_feature _PREMULTIPLY_ALPHA
            // 阴影过滤模式
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            // 阴影混合模式
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #include "LitPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Tags { "LightMode" = "ShadowCaster"}
            ColorMask 0
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            
            #pragma multi_compile_instancing
            #pragma shader_feature _CLIPPING
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}