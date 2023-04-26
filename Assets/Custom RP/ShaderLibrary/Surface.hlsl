#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

// 定义表面数据
struct Surface
{
    float3 position;       //空间坐标
    float3 normal;         //表面法线
    float3 viewDirection;  //观察方向
    float3 color;          //表面颜色 固有色
    float alpha;           //透明度
    float metallic;        //金属度
    float smoothness;      //光滑度 平滑度
};

#endif