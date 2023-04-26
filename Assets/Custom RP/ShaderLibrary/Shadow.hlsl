#ifndef CUSTOM_SHADOW_INCLUDED
#define CUSTOM_SHADOW_INCLUDED

//定义最大数量的定向光阴影
#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4

//用CBuffer包裹构造方向光源的两个属性，cpu会每帧传递（修改）
//这两个属性到GPU的常量缓冲区，对于一次渲染过程这两个值恒定
CBUFFER_START(_CustomShadows)
float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT]; //定向光阴影矩阵
CBUFFER_END

TEXTURE2D_SHADOW(_DirectionalShadowAtlas); //使用TEXTURE2D_SHADOW来明确我们接收的是阴影贴图
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);
//阴影贴图只有一种采样方式 因此我们显式定义一个阴影采样器状态 不需要依赖任何纹理
//其名字为sampler_linear_clamp_compare(使用宏定义它为SHADOW_SAMPLER)
//由此 对于任何阴影贴图 我们都可以使用SHADOW_SAMPLER这个采样器状态

// 定向光的阴影数据
struct DirectionalShadowData
{
    float strength; //阴影强度
    int tileIndex;  //阴影索引
};

// 采样定向光阴影纹理
float SampleDirectionalShadowAtlas (float3 positionSTS)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 求出定向光的阴影
float GetDirectionalShadowAttenuation (DirectionalShadowData data, Surface surfaceWS)
{
    if (data.strength <= 0)
        return 1.0; //当阴影强度为0时 根本不需要计算阴影 直接返回1
    //将世界空间下的位置坐标转换到光源空间下的坐标
    float3 positionSTS = mul(_DirectionalShadowMatrices[data.tileIndex], float4(surfaceWS.position, 1.0)).xyz;
    float shadow = SampleDirectionalShadowAtlas(positionSTS); //采样阴影纹理
    return lerp(1.0, shadow, data.strength); //最终衰减(阴影)的值应该是通过阴影强度来进行插值
}

#endif