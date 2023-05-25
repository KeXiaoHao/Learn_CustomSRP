#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    #if defined(_FLIPBOOK_BLENDING)
        float4 baseUV : TEXCOORD0;
        float flipbookBlend : TEXCOORD1;
    #else
        float2 baseUV : TEXCOORD0;
    #endif
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID //添加对象索引
};

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    #if defined(_VERTEX_COLORS)
        float4 color : VAR_COLOR;
    #endif
    float2 baseUV : VAR_BASE_UV;
    #if defined(_FLIPBOOK_BLENDING)
        float3 flipbookUVB : VAR_FLIPBOOK;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID //接收对象索引
};

Varyings UnlitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input); //提取索引并将其储存在其他实例化宏所依赖的全局静态变量中
    UNITY_TRANSFER_INSTANCE_ID(input, output); //对象索引从顶点传到片元
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(positionWS);

    #if defined(_VERTEX_COLORS)
        output.color = input.color;
    #endif

    output.baseUV.xy = TransformBaseUV(input.baseUV.xy);
    #if defined(_FLIPBOOK_BLENDING)
        output.flipbookUVB.xy = TransformBaseUV(input.baseUV.zw);
        output.flipbookUVB.z = input.flipbookBlend;
    #endif
    return output;
}

float4 UnlitPassFragment(Varyings i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);

    InputConfig config = GetInputConfig(i.positionCS_SS, i.baseUV);
    // return float4(config.fragment.bufferDepth.xxx / 20.0, 1.0);
    // return GetBufferColor(config.fragment, 0.05);
    #if defined(_VERTEX_COLORS)
        config.color = i.color;
    #endif
    
    #if defined(_FLIPBOOK_BLENDING)
        config.flipbookUVB = i.flipbookUVB;
        config.flipbookBlending = true;
    #endif
    
    #if defined(_NEAR_FADE)
        config.nearFade = true;
    #endif

    #if defined(_SOFT_PARTICLES)
        config.softParticles = true;
    #endif
    
    float4 base = GetBase(config);
    #if defined(_CLIPPING)
    clip(base.a - GetCutoff(config));
    #endif

    #if defined(_DISTORTION)
        float2 distortion = GetDistortion(config) * base.a;
        base.rgb = lerp(GetBufferColor(config.fragment, distortion).rgb, base.rgb, saturate(base.a - GetDistortionBlend(config)));
    #endif

    return float4(base.rgb, GetFinalAlpha(base.a));
}

#endif
