#ifndef CUSTOM_UNLIT_INPUT_INCLUDED
#define CUSTOM_UNLIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_DistortionMap);
SAMPLER(sampler_DistortionMap);

// CBUFFER_START(UnityPerMaterial)
// float4 _BaseColor;
// CBUFFER_END

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _ZWrite)
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeDistance)
    UNITY_DEFINE_INSTANCED_PROP(float, _NearFadeRange)
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesDistance)
    UNITY_DEFINE_INSTANCED_PROP(float, _SoftParticlesRange)
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionStrength)
    UNITY_DEFINE_INSTANCED_PROP(float, _DistortionBlend)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

// 简化 UNITY_ACCESS_INSTANCED_PROP
#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig
{
    float4 color;
    float2 baseUV;
    float3 flipbookUVB;
    bool flipbookBlending;
    Fragment fragment;
    bool nearFade;
    bool softParticles;
};

InputConfig GetInputConfig(float4 positionSS, float2 baseUV)
{
    InputConfig c;
    c.color = 1.0;
    c.baseUV = baseUV;
    c.flipbookUVB = 0.0;
    c.flipbookBlending = false;
    c.fragment = GetFragment(positionSS);
    c.nearFade = false;
    c.softParticles = false;
    return c;
}

float GetFinalAlpha(float alpha)
{
    return INPUT_PROP(_ZWrite) ? 1.0 : alpha;
}

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float4 GetBase(InputConfig c)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.baseUV);
    if (c.flipbookBlending)
    {
        map = lerp(map, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, c.flipbookUVB.xy),c.flipbookUVB.z);
    }
    if (c.nearFade)
    {
        // 衰减因子等于像素深度减去淡入淡出距离 然后除以淡入淡出范围 可能为负 所以saturate
        float nearAttenuation = (c.fragment.depth - INPUT_PROP(_NearFadeDistance)) / INPUT_PROP(_NearFadeRange);
        map.a *= saturate(nearAttenuation);
    }
    if (c.softParticles)
    {
        float depthDelta = c.fragment.bufferDepth - c.fragment.depth; // 基于片段的缓冲区深度减去其自身的深度
        float nearAttenuation = (depthDelta - INPUT_PROP(_SoftParticlesDistance)) / INPUT_PROP(_SoftParticlesRange);
        map.a *= saturate(nearAttenuation);
    }
    float4 color = INPUT_PROP(_BaseColor);
    return map * color * c.color;
}

float GetCutoff(InputConfig c)
{
    return INPUT_PROP(_Cutoff);
}

float3 GetEmission(InputConfig c)
{
    return GetBase(c).rgb;
}

float GetFresnel (InputConfig c)
{
    return 0.0;
}

float GetDistortionBlend(InputConfig c)
{
    return INPUT_PROP(_DistortionBlend);
}

// 对失真纹理采样并返回XY分量
float2 GetDistortion(InputConfig c)
{
    float4 rawMap = SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, c.baseUV);
    if (c.flipbookBlending)
    {
        rawMap = lerp(rawMap, SAMPLE_TEXTURE2D(_DistortionMap, sampler_DistortionMap, c.flipbookUVB.xy), c.flipbookUVB.z);
    }
    return DecodeNormal(rawMap, INPUT_PROP(_DistortionStrength)).xy;
}

#endif