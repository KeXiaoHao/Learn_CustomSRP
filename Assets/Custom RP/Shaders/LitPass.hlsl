#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadow.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float3 normalOS : NORMAL;

    #if defined(_NORMAL_MAP)
        float4 tangentOS : TANGENT;
    #endif
    
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID //添加对象索引
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float2 baseUV : VAR_BASE_UV;
    
    #if defined(_DETAIL_MAP)
        float2 detailUV : VAR_DETAIL_UV;
    #endif
    
    float3 normalWS : VAR_NORMAL;
    float4 tangentWS : VAR_TANGENT;
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID //接收对象索引
};

Varyings LitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input); //提取索引并将其储存在其他实例化宏所依赖的全局静态变量中
    UNITY_TRANSFER_INSTANCE_ID(input, output); //对象索引从顶点传到片元

    TRANSFER_GI_DATA(input, output); //光照贴图传递 即output.lightMapUV = input.lightMapUV
    
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

    #if defined(_NORMAL_MAP)
        output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    #endif
    
    output.baseUV = TransformBaseUV(input.baseUV);

    #if defined(_DETAIL_MAP)
        output.detailUV = TransformDetailUV(input.baseUV);
    #endif
    
    return output;
}

float4 LitPassFragment(Varyings i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);

    ClipLOD(i.positionCS.xy, unity_LODFade.x); //LOD抖动

    InputConfig config = GetInputConfig(i.baseUV);
    #if defined(_MASK_MAP)
        config.useMask = true;
    #endif
    
    #if defined(_DETAIL_MAP)
        config.detailUV = i.detailUV;
        config.useDetail = true;
    #endif
    
    float4 base = GetBase(config);
    
    #if defined(_SHADOWS_CLIP)
        clip(base.a - GetCutoff(config));
    #elif defined(_SHADOWS_DITHER)
        float dither = InterleavedGradientNoise(i.positionCS.xy, 0);
        clip(base.a - dither);
    #endif
    
    Surface surface;
    surface.position = i.positionWS;
    
    #if defined(_NORMAL_MAP)
        surface.normal = NormalTangentToWorld(GetNormalTS(config), i.normalWS, i.tangentWS);
    #else
        surface.normal = normalize(i.normalWS);
        surface.interpolatedNormal = surface.normal;
    #endif
    
    surface.interpolatedNormal = i.normalWS;
    surface.viewDirection = normalize(_WorldSpaceCameraPos - i.positionWS);
    surface.depth = -TransformWorldToView(i.positionWS).z;
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
    surface.smoothness = GetSmoothness(config);
    surface.occlusion = GetOcclusion(config);
    surface.fresnelStrength = GetFresnel(config);
    surface.dither = InterleavedGradientNoise(i.positionCS.xy, 0);

    #if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface,true);
    #else
    BRDF brdf = GetBRDF(surface);
    #endif

    GI gi = GetGI(GI_FRAGMENT_DATA(i), surface, brdf);
    
    float3 color = GetLighting(surface, brdf, gi);
    color += GetEmission(config);
    return float4(color, surface.alpha);
}

#endif
