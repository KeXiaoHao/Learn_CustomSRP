#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

bool _ShadowPancaking;

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

Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input); //提取索引并将其储存在其他实例化宏所依赖的全局静态变量中
    UNITY_TRANSFER_INSTANCE_ID(input, output); //对象索引从顶点传到片元
    
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);

    if (_ShadowPancaking)
    {
        //防止超过视图范围的模型 其影子在视图中被裁剪 将顶点位置移到近平面
        #if UNITY_REVERSED_Z
            output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #else
            output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
        #endif
    }
    
    output.baseUV = TransformBaseUV(input.baseUV);
    return output;
}

void ShadowCasterPassFragment(Varyings i)
{
    UNITY_SETUP_INSTANCE_ID(i);

    InputConfig config = GetInputConfig(i.baseUV);

    ClipLOD(i.positionCS.xy, unity_LODFade.x);
    
    float4 base = GetBase(config);
    
    #if defined(_SHADOWS_CLIP)
        clip(base.a - GetCutoff(config));
    #elif defined(_SHADOWS_DITHER)
        float dither = InterleavedGradientNoise(i.positionCS.xy, 0);
        clip(base.a - dither);
    #endif
}

#endif
