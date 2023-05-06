#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_NormalMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);

// CBUFFER_START(UnityPerMaterial)
// float4 _BaseColor;
// CBUFFER_END

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _DetailMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _Occlusion)
    UNITY_DEFINE_INSTANCED_PROP(float, _Fresnel)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailAlbedo)
    UNITY_DEFINE_INSTANCED_PROP(float, _DetailSmoothness)
    UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

// 简化 UNITY_ACCESS_INSTANCED_PROP
#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

float2 TransformBaseUV(float2 baseUV)
{
    float4 baseST = INPUT_PROP(_BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

float2 TransformDetailUV (float2 detailUV)
{
    float4 detailST = INPUT_PROP(_DetailMap_ST);
    return detailUV * detailST.xy + detailST.zw;
}

float4 GetDetail (float2 detailUV)
{
    float4 map = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUV);
    return map * 2.0 - 1.0;
}

float4 GetBase(float2 baseUV, float2 detailUV = 0.0)
{
    float4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    float4 color = INPUT_PROP(_BaseColor);

    // 只有R通道影响反照率 将其推向黑色或白色 这可以通过用0或1插值颜色来完成
    float detail = GetDetail(detailUV).r * INPUT_PROP(_DetailAlbedo);
    //插值器是绝对细节值 这应该只影响反照率 而不会影响map的a通道
    map.rgb = lerp(sqrt(map.rgb), detail < 0.0 ? 0.0 : 1.0, abs(detail));
    //在伽马空间中执行此操作将更好地匹配视觉上相等的分布 通过插值反照率的平方根 然后进行平方来近似
    map.rgb *= map.rgb;
    return map * color;
}

float4 GetMask (float2 baseUV)
{
    return SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, baseUV);
}

float GetCutoff(float2 baseUV)
{
    return INPUT_PROP(_Cutoff);
}

float GetMetallic(float2 baseUV)
{
    float metallic = INPUT_PROP(_Metallic);
    metallic *= GetMask(baseUV).r;
    return metallic;
}

float GetSmoothness(float2 baseUV, float2 detailUV = 0.0)
{
    float smoothness = INPUT_PROP(_Smoothness);
    smoothness *= GetMask(baseUV).a;

    float detail = GetDetail(detailUV).b * INPUT_PROP(_DetailSmoothness);
    float mask = GetMask(baseUV).b;
    smoothness = lerp(smoothness, detail < 0.0 ? 0.0 : 1.0, abs(detail) * mask);
    
    return smoothness;
}

float GetOcclusion (float2 baseUV)
{
    float strength = INPUT_PROP(_Occlusion);
    float occlusion = GetMask(baseUV).g;
    occlusion = lerp(occlusion, 1.0, strength);
    return occlusion;
}

float3 GetEmission(float2 baseUV)
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
    float4 color = INPUT_PROP(_EmissionColor);
    return map.rgb * color.rgb;
}

float GetFresnel (float2 baseUV)
{
    return INPUT_PROP(_Fresnel);
}

float3 GetNormalTS (float2 baseUV)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, baseUV);
    float scale = INPUT_PROP(_NormalScale);
    float3 normal = DecodeNormal(map, scale);
    return normal;
}

// LOD混合
void ClipLOD (float2 positionCS, float fade)
{
    #if defined(LOD_FADE_CROSSFADE)
    float dither = InterleavedGradientNoise(positionCS.xy, 0);
    clip(fade + (fade < 0.0 ? dither : -dither));
    #endif
}

#endif