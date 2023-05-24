#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID //添加对象索引
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
    UNITY_VERTEX_INPUT_INSTANCE_ID //接收对象索引
};

Varyings UnlitPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input); //提取索引并将其储存在其他实例化宏所依赖的全局静态变量中
    UNITY_TRANSFER_INSTANCE_ID(input, output); //对象索引从顶点传到片元
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);

    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

float4 UnlitPassFragment(Varyings i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);
    
    float4 base = GetBase(i.baseUV);
    #if defined(_CLIPPING)
    clip(base.a - GetCutoff(i.baseUV));
    #endif

    return float4(base.rgb, GetFinalAlpha(base.a));
}

#endif
