Shader "CustomRP/Unlit"
{
    Properties
    {
        [HDR]_BaseMap ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)]_Clipping ("Alpha Clipping", float) = 0
        _BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
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
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}