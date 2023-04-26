Shader "CustomRP/Lit"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _BaseMap ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)]_Clipping ("Alpha Clipping", float) = 0
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
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #include "LitPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}