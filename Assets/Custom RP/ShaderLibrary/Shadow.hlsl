#ifndef CUSTOM_SHADOW_INCLUDED
#define CUSTOM_SHADOW_INCLUDED

//包含阴影的过滤器样本
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

//定义最大数量的定向光阴影 与CPU端相同
#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
//定义最大级数的级联阴影 与CPU端相同
#define MAX_CASCADE_COUNT 4

//用CBuffer包裹构造方向光源的两个属性，cpu会每帧传递（修改）
//这两个属性到GPU的常量缓冲区，对于一次渲染过程这两个值恒定
CBUFFER_START(_CustomShadows)
int _CascadeCount; //级联阴影级数
float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT]; //级联阴影剔除球体
float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT]; //定向光阴影矩阵
float4 _ShadowAtlasSize; // 阴影图集大小
float4 _ShadowDistanceFade; //淡出阴影距离
float4 _CascadeData[MAX_CASCADE_COUNT]; //级联阴影数据组
CBUFFER_END

TEXTURE2D_SHADOW(_DirectionalShadowAtlas); //使用TEXTURE2D_SHADOW来明确我们接收的是阴影贴图
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);
//阴影贴图只有一种采样方式 因此我们显式定义一个阴影采样器状态 不需要依赖任何纹理
//其名字为sampler_linear_clamp_compare(使用宏定义它为SHADOW_SAMPLER)
//由此 对于任何阴影贴图 我们都可以使用SHADOW_SAMPLER这个采样器状态

//////////////////////////////////////////// 阴影相关参数 //////////////////////////////////////////////

// 阴影蒙版
struct ShadowMask
{
    bool always;   // 是否始终使用阴影遮罩
    bool distance; // 是否开启距离阴影遮罩
    float4 shadows; //烘焙的阴影
};

// 阴影数据
struct ShadowData
{
    int cascadeIndex; //级联索引
    float cascadeBlend; //级联混合
    float strength;   //强度
    ShadowMask shadowMask; //阴影蒙版
};

// 计算两向量之间距离的平方
float DistanceSquared (float3 pA, float3 pB)
{
    return  dot(pA - pB, pA - pB);
}

// 计算阴影淡入淡出的阴影强度
// 公式为 (1 - (d / m)) / f 其中d是表面深度 m是最大阴影距离 f是淡入淡出范围 CPU端有提前计算一部分
float FadeShadowStrength (float distance, float sacle, float fade)
{
    return saturate((1.0 - distance * sacle) * fade);
}

// 获取阴影数据
ShadowData GetShadowData (Surface surfaceWS)
{
    ShadowData data;

    data.shadowMask.always = false;
    data.shadowMask.distance = false;
    data.shadowMask.shadows = 1.0;
    
    data.cascadeBlend = 1.0;
    data.strength = FadeShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    int i;
    // 遍历所有级联剔除球体 直到找到包含表面位置的球体 找到后断开循环 然后将当前循环迭代器用作级联索引
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w)
        {
            // 级联的淡入淡出计算
            // 公式为 (1 - (d^2 / r^2)) / f 其中d是模型与球心距离平方 r是剔除球体半径 f是淡入淡出范围 CPU端有提前计算一部分
            float fade = FadeShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
            if (i == _CascadeCount - 1)
            {
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }
    if (i == _CascadeCount)
        data.strength = 0.0;
    // 当使用抖动混合时 如果不在最后一个级联中 如果混合值小于抖动值 则跳到下一个级联
    #if defined(_CASCADE_BLEND_DITHER)
    else if (data.cascadeBlend < surfaceWS.dither)
        i += 1;
    #endif

    //如果未使用软混合 将级联混合设置为零 这样 整个分支将从这些着色器变体中消除
    #if !defined(_CASCADE_BLEND_SOFT)
        data.cascadeBlend = 1.0;
    #endif
    
    data.cascadeIndex = i;
    return data;
}

//////////////////////////////////////////// 定向光的阴影相关参数 //////////////////////////////////////////////

// 定向光的阴影数据
struct DirectionalShadowData
{
    float strength; //阴影强度
    int tileIndex;  //阴影索引
    float normalBias; //法线偏差
    int shadowMaskChannel; // 阴影遮罩的通道
};

// 采样定向光阴影纹理
float SampleDirectionalShadowAtlas (float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 含有过滤模式的定向光阴影
float FilterDirectionalShadow(float3 positionSTS)
{
    #if defined (DIRECTIONAL_FILTER_SETUP)
        float weights[DIRECTIONAL_FILTER_SAMPLES]; // 权重
        float2 positions[DIRECTIONAL_FILTER_SAMPLES]; // 输出位置
        float4 size = _ShadowAtlasSize.yyxx; // 前两个分量中的 X 和 Y 纹素大小以及 Z 和 W 中的总纹理大小
        DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
        float shadow = 0;
        for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
        {
            shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
        }
        return shadow;
    #else
        return SampleDirectionalShadowAtlas(positionSTS);
    #endif
}

//////////////////////////////////////////// 阴影相关计算 //////////////////////////////////////////////

// 计算实时的级联阴影
float GetCascadedShadow ( DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
    float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    //将世界空间下的位置坐标转换到光源空间下的坐标 并应该法线偏移
    float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;
    float shadow = FilterDirectionalShadow(positionSTS); //采样阴影纹理

    // 现在检查在检索第一个阴影值后级联混合是否小于 1 如果是 则处于过渡区 必须从下一个级联中采样并在两个值之间进行插值
    if (global.cascadeBlend < 1.0)
    {
        normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex + 1].y);
        positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex + 1], float4(surfaceWS.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, global.cascadeBlend);
    }
    return shadow;
}

// 计算阴影蒙版里的烘焙的阴影
float GetBakedShadow(ShadowMask mask, int channel)
{
    float shadow = 1.0;
    if (mask.always || mask.distance)
    {
        if (channel >= 0)
        shadow = mask.shadows[channel]; //对于不同的光源 利用CPU传过来的索引 使用不同光源下的烘焙阴影贴图的通道 以支持多个光源混合阴影
    }
    return shadow;
}
// 计算阴影蒙版里的烘焙的阴影 具有强度参数
float GetBakedShadow(ShadowMask mask, int channel, float strength)
{
    if (mask.always || mask.distance)
        return lerp(1.0, GetBakedShadow(mask, channel), strength);
    return 1.0;
}

// 实时阴影和烘焙阴影的混合计算
float MixBakedAndRealtimeShadows ( ShadowData global, float shadow, int shadowMaskChannel, float strength )
{
    float baked = GetBakedShadow(global.shadowMask, shadowMaskChannel);

    if (global.shadowMask.always) //当始终使用阴影遮罩时
    {
        shadow = lerp(1.0, shadow, global.strength); // 首先实时阴影必须通过全局强度进行调制 以根据深度使其淡化
        shadow = min(baked, shadow); // 然后通过最小化来组合烘焙阴影和实时阴影
        return lerp(1.0, shadow, strength); // 之后光源的阴影强度将应用于合并的阴影
    }
    
    if (global.shadowMask.distance) // 当开启距离阴影遮罩时
    {
        shadow = lerp(baked, shadow, global.strength); //用全局阴影的强度来插值烘焙阴影和实时阴影
        return lerp(1.0, shadow, strength); // 接着再拿定向光的阴影强度来插值
    }
    return lerp(1.0, shadow, strength * global.strength); //最终衰减(阴影)的值应该是通过阴影强度来进行插值
}



//////////////////////////////////////////// 最终光源的阴影合并 //////////////////////////////////////////////

// 求出定向光的阴影
float GetDirectionalShadowAttenuation (DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
    #if !defined(_RECEIVE_SHADOWS)
        return 1.0;
    #endif

    float shadow;
    
    if (directional.strength * global.strength <= 0.0) //当没有实时阴影存在时
    {
        // 当没有实时阴影投射器时以及当我们超出最大阴影距离时 都可以实现烘焙阴影
        shadow = GetBakedShadow(global.shadowMask, directional.shadowMaskChannel, abs(directional.strength)); //返回1或者烘焙阴影
    }
    else
    {
        shadow = GetCascadedShadow(directional, global, surfaceWS);
        shadow = MixBakedAndRealtimeShadows(global, shadow, directional.shadowMaskChannel, directional.strength);
    }
    
    return shadow;
}

#endif