#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

// 定义表面数据
struct Surface
{
    float3 position;       //空间坐标
    float3 normal;         //表面法线
    float3 interpolatedNormal;   // 用于Shadow Bias的插值法线  也就是顶点传过来的NormalWS
    float3 viewDirection;  //观察方向
    float depth;           //观察空间深度值
    
    float3 color;          //表面颜色 固有色
    float alpha;           //透明度
    float metallic;        //金属度
    float smoothness;      //光滑度 平滑度
    float occlusion;       // AO
    float fresnelStrength; // 菲尼尔强度

    float dither;          //抖动值

    uint renderingLayerMask; //渲染层
};

#endif