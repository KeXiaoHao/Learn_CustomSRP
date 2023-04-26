#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

//定义最大数量的定向光
#define MAX_DIRECTIONAL_LIGHT_COUNT 4

//用CBuffer包裹构造方向光源的两个属性，cpu会每帧传递（修改）
//这两个属性到GPU的常量缓冲区，对于一次渲染过程这两个值恒定
CBUFFER_START(_CustomLight)
int _DirectionalLightCount; //最大数量的定向光
float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT]; //定向光的颜色
float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT]; //定向光的方向
CBUFFER_END

// 灯光数据结构体
struct Light
{
    float3 color;     // 灯光颜色
    float3 direction; // 灯光方向
};

// 获取定向光的最大数量 方便其他地方调用
int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

// 得到平行光的灯光数据
Light GetDirectionalLight (int index)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    return light;
}

#endif