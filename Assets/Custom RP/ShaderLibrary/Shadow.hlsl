#ifndef CUSTOM_SHADOW_INCLUDED
#define CUSTOM_SHADOW_INCLUDED

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
float4 _ShadowDistanceFade; //淡出阴影距离
float4 _CascadeData[MAX_CASCADE_COUNT]; //级联阴影数据组
CBUFFER_END

TEXTURE2D_SHADOW(_DirectionalShadowAtlas); //使用TEXTURE2D_SHADOW来明确我们接收的是阴影贴图
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);
//阴影贴图只有一种采样方式 因此我们显式定义一个阴影采样器状态 不需要依赖任何纹理
//其名字为sampler_linear_clamp_compare(使用宏定义它为SHADOW_SAMPLER)
//由此 对于任何阴影贴图 我们都可以使用SHADOW_SAMPLER这个采样器状态

// 阴影数据
struct ShadowData
{
    int cascadeIndex; //级联索引
    float strength;   //强度
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
    data.strength = FadeShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    int i;
    // 遍历所有级联剔除球体 直到找到包含表面位置的球体 找到后断开循环 然后将当前循环迭代器用作级联索引
    for (i = 0; i < _CascadeCount; i++)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        if (distanceSqr < sphere.w)
        {
            if (i == _CascadeCount - 1)
            {
                // 级联的淡入淡出计算
                // 公式为 (1 - (d^2 / r^2)) / f 其中d是模型与球心距离平方 r是剔除球体半径 f是淡入淡出范围 CPU端有提前计算一部分
                data.strength *= FadeShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
            }
            break;
        }
    }
    if (i == _CascadeCount)
        data.strength = 0.0;
    data.cascadeIndex = i;
    return data;
}

// 定向光的阴影数据
struct DirectionalShadowData
{
    float strength; //阴影强度
    int tileIndex;  //阴影索引
    float normalBias; //法线偏差
};

// 采样定向光阴影纹理
float SampleDirectionalShadowAtlas (float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 求出定向光的阴影
float GetDirectionalShadowAttenuation (DirectionalShadowData directional, ShadowData global, Surface surfaceWS)
{
    if (directional.strength <= 0)
        return 1.0; //当阴影强度为0时 根本不需要计算阴影 直接返回1
    float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
    //将世界空间下的位置坐标转换到光源空间下的坐标 并应该法线偏移
    float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;
    float shadow = SampleDirectionalShadowAtlas(positionSTS); //采样阴影纹理
    return lerp(1.0, shadow, directional.strength); //最终衰减(阴影)的值应该是通过阴影强度来进行插值
}

#endif