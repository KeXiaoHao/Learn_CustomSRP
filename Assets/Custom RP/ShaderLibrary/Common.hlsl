#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "UnityInput.hlsl"

//将Unity内置着色器变量转换为SRP库需要的变量
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

//使用2021版本的坑，我们还需要定义两个PREV标识符，才不会报错，但这两个变量具体代表什么未知
#define UNITY_PREV_MATRIX_M unity_ObjectToWorld
#define UNITY_PREV_MATRIX_I_M unity_WorldToObject

// 遮挡数据可以自动实例化 仅定义SHADOWS_SHADOWMASK时才执行
#if defined(_SHADOW_MASK_ALWAYS) || defined(_SHADOW_MASK_DISTANCE)
    #define SHADOWS_SHADOWMASK
#endif

//我们直接使用SRP库中已经帮我们写好的函数
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

// 判断是否是正交相机
bool IsOrthographicCamera()
{
    return unity_OrthoParams.w; // 如果是正交相机 其最后一个分量将为1 否则将为零
}

//求出视角空间下的线性深度值
float OrthographicDepthBufferToLinear(float rawDepth)
{
    #if UNITY_REVERSED_Z
        rawDepth = 1.0 - rawDepth;
    #endif
    // z分量 远平面 y分量 近平面
    // 要将其转换为视图空间深度 必须按相机的近远范围对其进行缩放 然后加上近平面距离
    return (_ProjectionParams.z - _ProjectionParams.y) * rawDepth + _ProjectionParams.y;
}

#include "Fragment.hlsl"

// LOD混合
void ClipLOD (Fragment fragment, float fade)
{
    #if defined(LOD_FADE_CROSSFADE)
    float dither = InterleavedGradientNoise(fragment.positionSS, 0);
    clip(fade + (fade < 0.0 ? dither : -dither));
    #endif
}

// 解码法线贴图
float3 DecodeNormal (float4 sample, float scale)
{
    #if defined(UNITY_NO_DXT5nm)
        return UnpackNormalRGB(sample, scale);
    #else
        return UnpackNormalmapRGorAG(sample, scale);
    #endif
}
//将法线从切线空间转到世界空间下
float3 NormalTangentToWorld (float3 normalTS, float3 normalWS, float4 tangentWS)
{
    float3x3 tangentToWorld = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
    return TransformTangentToWorld(normalTS, tangentToWorld);
}

float Square (float v) {return v * v; } // 平方操作 方便多次使用

#endif