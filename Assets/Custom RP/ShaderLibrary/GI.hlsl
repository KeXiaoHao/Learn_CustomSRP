#ifndef CUSTOM_GI_INCLUDED
#define CUSTOM_GI_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

TEXTURE2D(unity_Lightmap); SAMPLER(samplerunity_Lightmap);

TEXTURE2D(unity_ShadowMask); SAMPLER(samplerunity_ShadowMask);

// LPPV
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

#if defined(LIGHTMAP_ON)
    #define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;
    #define GI_VARYINGS_DATA float2 lightMapUV : VAR_LIGHT_MAP_UV;

    // 宏定义可以拆分为多行 每行的末尾（最后一行除外）用反斜杠标记
    #define TRANSFER_GI_DATA(input, output) \
    output.lightMapUV = input.lightMapUV * \
    unity_LightmapST.xy + unity_LightmapST.zw;

    #define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
    #define GI_ATTRIBUTE_DATA
    #define GI_VARYINGS_DATA
    #define TRANSFER_GI_DATA(input, output)
    #define GI_FRAGMENT_DATA(input) 0.0
#endif

/////////////////////////////////////////// GI 采样 /////////////////////////////////////

// 采样烘焙后的阴影贴图
float4 SampleBakedShadows(float2 lightMapUV, Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, lightMapUV); //采样光照贴图里的阴影数据
    #else
        if (unity_ProbeVolumeParams.x)
        {
            // 采样LPPV的遮挡数据
            return SampleProbeOcclusion(TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
                surfaceWS.position, unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz);
        }
        else
        {
            //采样灯光探针的遮挡数据
            return unity_ProbesOcclusion;
        }
    #endif
}

// 采样光照贴图
float3 SampleLightMap (float2 lightMapUV) {
    #if defined(LIGHTMAP_ON)
    // 关于 SampleSingleLightmap 函数
    // 将纹理和采样器状态作为前两个参数传递 使用TEXTURE2D_ARGS这个宏
    // lightMapUV
    // 要应用的scale和translate 因为我们之前已经这样做了 所以我们将在这里使用标识转换
    // 一个布尔值来指示光照贴图是否被压缩
    // 最后一个参数是包含解码指令 用于其第一个组件和第二个组件 不使用其其他组件
    return SampleSingleLightmap(TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), lightMapUV,
        float4(1.0, 1.0, 0.0, 0.0),
        #if defined(UNITY_LIGHTMAP_FULL_HDR)
            false,
        #else
            true,
        #endif
        float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0));
    #else
        return 0.0;
    #endif
}

// 采样灯光探针
float3 SampleLightProbe(Surface surfaceWS)
{
    #if defined(LIGHTMAP_ON)
        return 0.0;
    #else
        // 通过unity_ProbeVolumeParams的第一个分量来确定使用插值的灯光探针还是LPPV
        if (unity_ProbeVolumeParams.x)
        {
            return SampleProbeVolumeSH4(
                TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
                surfaceWS.position, surfaceWS.normal,
                unity_ProbeVolumeWorldToObject,
                unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
                unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
            );
        }
        else
        {
            float4 coefficients[7];
            coefficients[0] = unity_SHAr;
            coefficients[1] = unity_SHAg;
            coefficients[2] = unity_SHAb;
            coefficients[3] = unity_SHBr;
            coefficients[4] = unity_SHBg;
            coefficients[5] = unity_SHBb;
            coefficients[6] = unity_SHC;
            return max(0.0, SampleSH9(coefficients, surfaceWS.normal));
        }
    #endif
}

/////////////////////////////////////////// GI 相关处理 /////////////////////////////////////

struct GI
{
    float3 diffuse;
    ShadowMask shadowMask;
};

GI GetGI (float2 lightMapUV, Surface surfaceWS)
{
    GI gi;
    gi.diffuse = SampleLightMap(lightMapUV) + SampleLightProbe(surfaceWS);
    gi.shadowMask.always = false;
    gi.shadowMask.distance = false;
    gi.shadowMask.shadows = 1.0;
    
    #if defined(_SHADOW_MASK_ALWAYS)
        gi.shadowMask.always = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #elif  defined(_SHADOW_MASK_DISTANCE)
        gi.shadowMask.distance = true;
        gi.shadowMask.shadows = SampleBakedShadows(lightMapUV, surfaceWS);
    #endif
    
    return gi;
}

#endif