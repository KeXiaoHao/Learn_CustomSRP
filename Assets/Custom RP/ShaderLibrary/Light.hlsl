#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

//定义最大数量的定向光和其他光源
#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 64

//用CBuffer包裹构造方向光源的两个属性，cpu会每帧传递（修改）
//这两个属性到GPU的常量缓冲区，对于一次渲染过程这两个值恒定
CBUFFER_START(_CustomLight)
    int _DirectionalLightCount; //最大数量的定向光
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT]; //定向光的颜色
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT]; //定向光的方向
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT]; //定向光阴影

    int _OtherLightCount;       //最大数量的其他光源
    float4 _OtherLightColors[MAX_OTHER_LIGHT_COUNT];              //其他光的颜色
    float4 _OtherLightPositions[MAX_OTHER_LIGHT_COUNT];           //其他光的位置
    float4 _OtherLightDirections[MAX_OTHER_LIGHT_COUNT];          //聚光灯的朝向
    float4 _OtherLightSpotAngles[MAX_OTHER_LIGHT_COUNT];          //聚光灯的角度
    float4 _OtherLightShadowData[MAX_OTHER_LIGHT_COUNT];          //其他光的阴影数据
CBUFFER_END

// 灯光数据结构体
struct Light
{
    float3 color;         // 灯光颜色
    float3 direction;     // 灯光方向
    float attenuation;    //灯光阴影
};

///////////////////////////////////////////////// 定向光获取 //////////////////////////////////////////////

// 获取定向光的最大数量 方便其他地方调用
int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

// 获取定向光的阴影数据
DirectionalShadowData GetDirectionalShadowData (int lightIndex, ShadowData shadowdata)
{
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[lightIndex].x;
    // 通过将级联索引添加到光源的阴影图集偏移量来选择正确的tile索引
    data.tileIndex = _DirectionalLightShadowData[lightIndex].y + shadowdata.cascadeIndex;
    data.normalBias = _DirectionalLightShadowData[lightIndex].z;
    data.shadowMaskChannel = _DirectionalLightShadowData[lightIndex].w;
    return data;
}

// 得到平行光的灯光数据
Light GetDirectionalLight (int index, Surface surfaceWS, ShadowData shadowdata)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    DirectionalShadowData dirShadowData = GetDirectionalShadowData(index, shadowdata); //获取定向光的阴影数据
    light.attenuation = GetDirectionalShadowAttenuation(dirShadowData, shadowdata, surfaceWS); //计算定向光的阴影
    // light.attenuation = shadowdata.cascadeIndex * 0.25;
    return light;
}

///////////////////////////////////////////////// 其他光获取 //////////////////////////////////////////////

// 获取其他光的最大数量 方便其他地方调用
int GetOtherLightCount()
{
    return _OtherLightCount;
}

// 获取其他光的阴影数据
OtherShadowData GetOtherShadowData (int lightIndex)
{
    OtherShadowData data;
    data.strength = _OtherLightShadowData[lightIndex].x;
    data.tileIndex = _OtherLightShadowData[lightIndex].y;
    data.shadowMaskChannel = _OtherLightShadowData[lightIndex].w;
    data.isPoint = _OtherLightShadowData[lightIndex].z == 1.0;
    data.lightPositionWS = 0.0;
    data.lightDirectionWS = 0.0;
    data.spotDirectionWS = 0.0;
    return data;
}

// 得到其他光的灯光数据
Light GetOtherLight(int index, Surface surfaceWS, ShadowData shadowData)
{
    Light light;
    light.color = _OtherLightColors[index].rgb;

    float3 position = _OtherLightPositions[index].xyz;
    float3 ray = position - surfaceWS.position; //灯光方向
    light.direction = normalize(ray);
    
    float distanceSqr = max(dot(ray, ray), 0.00001); //距离平方
    float rangeAttenuation = Square(saturate(1.0 - Square(distanceSqr * _OtherLightPositions[index].w))); //范围衰减

    float4 spotAngles = _OtherLightSpotAngles[index]; //聚光灯的角度
    float3 spotDirection = _OtherLightDirections[index].xyz;
    float spotAttenuation = Square(saturate(dot(spotDirection, light.direction) * spotAngles.x + spotAngles.y)); //聚光灯朝向

    OtherShadowData otherShadowData = GetOtherShadowData(index);
    otherShadowData.lightPositionWS = position;
    otherShadowData.lightDirectionWS = light.direction;
    otherShadowData.spotDirectionWS = spotDirection;
    light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS) * spotAttenuation * rangeAttenuation / distanceSqr;
    // light.attenuation = GetOtherShadowAttenuation(otherShadowData, shadowData, surfaceWS);
    
    return light;
}

#endif