#ifndef CUSTOM_POST_FX_PASSES_INCLUDED
#define CUSTOM_POST_FX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_PostFXSource);
TEXTURE2D(_PostFXSource2);
SAMPLER(sampler_linear_clamp);

TEXTURE2D(_ColorGradingLUT); // lut纹理

float4 _PostFXSource_TexelSize; // x = 1/width y = 1/height

bool _BloomBicubicUpsampling; //是否需要三线性过滤

// x = t
// y = -t + tk
// z = 2tk
// w = 1.0 / (4tk + 0.00001)
float4 _BloomThreshold;  //Bloom阈值

float _BloomIntensity;   //强度

float4 _ColorAdjustments; // x:曝光度 y:对比度 z:色相偏移 w:饱和度
float4 _ColorFilter;      //颜色滤镜
float4 _WhiteBalance;     //白平衡参数
float4 _SplitToningShadows, _SplitToningHighlights; // 色调分离的阴影和高光
float4 _ChannelMixerRed, _ChannelMixerGreen, _ChannelMixerBlue; // 通道混合
float4 _SMHShadows, _SMHMidtones, _SMHHighlights, _SMHRange;    // 阴影中间调高光和范围

float4 _ColorGradingLUTParameters; // lut颜色
bool _ColorGradingLUTInLogC; // 是否使用LogC空间

//=====================================================================================================//

//采样脚本传来的临时纹理
float4 GetSource(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);
}

float4 GetSource2(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

// 采样双三次滤波
float4 GetSourceBicubic(float2 screenUV)
{
    return SampleTexture2DBicubic(TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
        _PostFXSource_TexelSize.zwxy, 1.0, 0.0);
}

//应用bloom阈值
float3 ApplyBloomThreshold(float3 color)
{
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
}



// 后处理的曝光度
float3 ColorGradePostExposure(float3 color)
{
    return color * _ColorAdjustments.x;
}

// 白平衡调整
float3 ColorGradeWhiteBalance(float3 color)
{
    // 将颜色与 LMS 色彩空间中的矢量相乘来应用白平衡
    // LMS色彩空间 即将颜色描述为人眼中三种感光体锥细胞类型的反应
    color = LinearToLMS(color);
    color *= _WhiteBalance.rgb;
    return LMSToLinear(color);
}

// 使用 ACES 色调映射时 Unity 在 ACES 色彩空间（而不是线性色彩空间）中执行大多数颜色分级 以产生更好的结果
// 后处理的曝光度和白平衡始终在线性空间中应用

// 调整对比度
float3 ColorGradingContrast(float3 color, bool useACES)
{
    // 为了获得最佳结果 此转换是在Log C而不是线性颜色空间中完成的
    color = useACES ? ACES_to_ACEScc(unity_to_ACES(color)) : LinearToLogC(color); 
    // ACEScc 是 ACES 色彩空间的对数子集 中间灰色值为 0.4135884
    // 通过从颜色中减去均匀的中灰色 然后按对比度缩放 并添加中灰色来应用
    color = (color - ACEScc_MIDGRAY) * _ColorAdjustments.y + ACEScc_MIDGRAY;
    return useACES ? ACES_to_ACEScg(ACEScc_to_ACES(color)) : LogCToLinear(color);
}

float Luminance(float3 color, bool useACES)
{
    return useACES ? AcesLuminance(color) : Luminance(color);
}

// 颜色滤镜
float3 ColorGradeColorFilter(float3 color)
{
    return color * _ColorFilter.rgb;
}

// 色调分离
float3 ColorGradeSplitToning(float3 color, bool useACES)
{
    color = PositivePow(color, 1.0 / 2.2); // 在近似伽马空间中执行拆分色调 之后再提高到2.2

    // 将色调限制在各自的区域 在混合之前将它们插值在中性 0.5 和自身之间
    // 对于高光 根据饱和亮度加上平衡 再次饱和 对于阴影 相反
    float t = saturate(Luminance(saturate(color), useACES) + _SplitToningShadows.w);
    float3 shadows = lerp(0.5, _SplitToningShadows.rgb, 1.0 - t);
    float3 highlights = lerp(0.5, _SplitToningHighlights.rgb, t);
    // 通过在颜色和阴影色调之间进行柔和的光线混合来应用色调 高光色调同理
    color = SoftLight(color, shadows);
    color = SoftLight(color, highlights);
    
    return PositivePow(color, 2.2);
}

// 色相偏移
float3 ColorGradingHueShift(float3 color)
{
    color = RgbToHsv(color);
    float hue = color.x + _ColorAdjustments.z;
    color.x = RotateHue(hue, 0.0, 1.0);
    return HsvToRgb(color);
}

// 饱和度调整
float3 ColorGradingSaturation(float3 color, bool useACES)
{
    float luminance = Luminance(color, useACES);
    return (color - luminance) * _ColorAdjustments.w + luminance;
}

// 通道混合
float3 ColorGradingChannelMixer(float3 color)
{
    return mul(float3x3(_ChannelMixerRed.rgb, _ChannelMixerGreen.rgb, _ChannelMixerBlue.rgb),color);
}

// 阴影中间调高光
float3 ColorGradingShadowsMidtonesHighlights(float3 color, bool useACES)
{
    // 将颜色分别乘以三种颜色 每种颜色按自己的权重缩放 对结果求和
    // 权重基于亮度 阴影权重从 1 开始 在其开始和结束之间减少到零
    // 高光的权重从零增加到1 中间调权重等于1 - 减去其他两个权重
    float luminance = Luminance(color, useACES);
    float shadowsWeight = 1.0 - smoothstep(_SMHRange.x, _SMHRange.y, luminance);
    float highlightsWeight = smoothstep(_SMHRange.z, _SMHRange.w, luminance);
    float midtonesWeight = 1.0 - shadowsWeight - highlightsWeight;
    return
        color * _SMHShadows.rgb * shadowsWeight +
        color * _SMHMidtones.rgb * midtonesWeight +
        color * _SMHHighlights.rgb * highlightsWeight;
}

// 颜色分级
float3 ColorGrade(float3 color, bool useACES = false)
{
    color = ColorGradePostExposure(color);
    color = ColorGradeWhiteBalance(color);
    color = ColorGradingContrast(color, useACES);
    color = ColorGradeColorFilter(color);
    color = max(color, 0.0); //当对比度增加时 可能会导致负色分量
    color = ColorGradeSplitToning(color, useACES);
    color = ColorGradingChannelMixer(color);
    color = max(color, 0.0); // 防止负值
    color = ColorGradingShadowsMidtonesHighlights(color, useACES);
    color = ColorGradingHueShift(color);
    color = ColorGradingSaturation(color, useACES);
    return max(useACES ? ACEScg_to_ACES(color) : color, 0.0); //同样 防止负值
}

// 采样LUT纹理
float3 ApplyColorGradingLUT(float3 color)
{
    // 需要LUT纹理和采样器状态作为参数 然后是饱和的输入颜色（根据需要是线性或Log C空间） 最后是参数向量
    return ApplyLut2D(
        TEXTURE2D_ARGS(_ColorGradingLUT, sampler_linear_clamp),
        saturate(_ColorGradingLUTInLogC ? LinearToLogC(color) : color),
        _ColorGradingLUTParameters.xyz);
}

//=====================================================================================================//

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

// SV_VertexID: 顶点ID 接收具有“顶点编号”（为无符号整数）的变量
Varyings DefaultPassVertex (uint vertexID : SV_VertexID)
{
    Varyings output;
    output.positionCS = float4(vertexID <= 1 ? -1.0 : 3.0, vertexID == 1 ? 3.0 : -1.0, 0.0, 1.0);
    output.screenUV = float2(vertexID <= 1 ? 0.0 : 2.0, vertexID == 1 ? 2.0 : 0.0);
    if (_ProjectionParams.x < 0.0)
        output.screenUV.y = 1.0 - output.screenUV.y; //有些平台的V坐标是颠倒的 当x分量为负时 需要翻转
    return output;
}

//======================================== Bloom =============================================================//

float4 CopyPassFragment (Varyings i) : SV_TARGET
{
    return GetSource(i.screenUV);
}

float4 BloomHorizontalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {-4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0};
    float weights[] = {
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
        0.19459459, 0.12162162, 0.05405405, 0.01621622};
    for (int i = 0; i < 9; i++)
    {
        float offset = offsets[i] * 2.0 * GetSourceTexelSize().x;
        color += GetSource(input.screenUV + float2(offset, 0.0)).rgb * weights[i];
    }
    return float4(color, 1.0);
}

float4 BloomVerticalPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float offsets[] = {-3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923};
    float weights[] = {0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027};
    for (int i = 0; i < 5; i++)
    {
        float offset = offsets[i] * GetSourceTexelSize().y;
        color += GetSource(input.screenUV + float2(0.0, offset)).rgb * weights[i];
    }
    return float4(color, 1.0);
}

float4 BloomCombinePassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    else
        lowRes = GetSource(input.screenUV).rgb;
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lowRes * _BloomIntensity + highRes, 1.0);
}

float4 BloomScatterPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomScatterFinalPassFragment(Varyings input) : SV_TARGET
{
    float3 lowRes;
    if (_BloomBicubicUpsampling)
    {
        lowRes = GetSourceBicubic(input.screenUV).rgb;
    }
    else
    {
        lowRes = GetSource(input.screenUV).rgb;
    }
    float3 highRes = GetSource2(input.screenUV).rgb;
    lowRes += highRes - ApplyBloomThreshold(highRes);
    return float4(lerp(highRes, lowRes, _BloomIntensity), 1.0);
}

float4 BloomPrefilterPassFragment(Varyings input) : SV_TARGET
{
    float3 color = ApplyBloomThreshold(GetSource(input.screenUV).rgb);
    return float4(color, 1.0);
}

float4 BloomPrefilterFirefliesPassFragment(Varyings input) : SV_TARGET
{
    float3 color = 0.0;
    float weightSum = 0.0;
    float2 offsets[] = {float2(0.0, 0.0),float2(-1.0, -1.0), float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0)};
    for (int i = 0; i < 5; i++)
    {
        float3 c =GetSource(input.screenUV + offsets[i] * GetSourceTexelSize().xy * 2.0).rgb;
        c = ApplyBloomThreshold(c);
        float w = 1.0 / (Luminance(c) + 1.0);
        color += c * w;
        weightSum += w;
    }
    color /= weightSum;
    return float4(color, 1.0);
}

//======================================== 色调映射 =============================================================//

float3 GetColorGradedLUT(float2 uv, bool useACES = false)
{
    // 返回2D条形格式颜色查找表中给定位置的默认值
    // params = (lut_height, 0.5 / lut_width, 0.5 / lut_height, lut_height / lut_height - 1)
    float3 color = GetLutStripValue(uv, _ColorGradingLUTParameters);
    return ColorGrade(_ColorGradingLUTInLogC ? LogCToLinear(color) : color, useACES);
}

float4 ColorGradingNonePassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    return float4(color, 1.0);
}

float4 ColorGradingReinhardPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    color /= color + 1.0;
    return float4(color, 1.0);
}

float4 ColorGradingNeutralPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV);
    color = NeutralTonemap(color);
    return float4(color, 1.0);
}

float4 ColorGradingACESPassFragment(Varyings input) : SV_TARGET
{
    float3 color = GetColorGradedLUT(input.screenUV, true);
    color = AcesTonemap(color);
    return float4(color, 1.0);
}

float4 FinalPassFragment(Varyings input) : SV_TARGET
{
    float4 color = GetSource(input.screenUV);
    color.rgb = ApplyColorGradingLUT(color.rgb);
    return color;
}

#endif