// ReSharper disable CommentTypo
#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED

#if defined(FXAA_QUALITY_LOW)
    #define EXTRA_EDGE_STEPS 3                // 定义额外边缘步进次数
    #define EDGE_STEP_SIZES 1.0, 1.0, 1.0     // 定义额外边缘步进的偏移量大小
    #define LAST_EDGE_STEP_GUESS 1.0          // 定义最后一个步进猜测的偏移量
#elif defined(FXAA_QUALITY_MEDIUM)
    #define EXTRA_EDGE_STEPS 8
    #define EDGE_STEP_SIZES 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 4.0
    #define LAST_EDGE_STEP_GUESS 8.0
#else
    #define EXTRA_EDGE_STEPS 10
    #define EDGE_STEP_SIZES 1.0, 1.0, 1.0, 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 4.0
    #define LAST_EDGE_STEP_GUESS 8.0
#endif

static const float edgeStepSizes[EXTRA_EDGE_STEPS] = { EDGE_STEP_SIZES }; // 建立静态常量数组

float4 _FXAAConfig; // fxaa配置参数 x:固定阈值 y:相对阈值 z:子像素混合

////////////////////////////////////////// Param ////////////////////////////////////////////////////////////////

struct LumaNeighborhood
{
    float m, n, e, s, w, ne, se, sw, nw; // 像素中心以及邻边像素
    float highest, lowest, range; // 最高和最低亮度值 亮度范围
};

struct FXAAEdge
{
    bool isHorizontal; //水平边缘检测
    float pixelStep;   //像素步长
    float lumaGradient, otherLuma; // 当前亮度梯度 和 边缘另一侧的亮度
};

////////////////////////////////////////// Function ////////////////////////////////////////////////////////////////

// 得到线性亮度
float GetLuma(float2 uv, float uOffset = 0.0, float vOffset = 0.0)
{
    // 局部对比度是通过对源像素邻域中像素的亮度进行采样来找到的
    // 添加两个便宜参数 以便沿 U 和 V 维度以像素为单位进行偏移
    uv += float2(uOffset, vOffset) * GetSourceTexelSize().xy;
    
    // 因为我们比浅色更能感知深色的变化 所以我们必须对亮度进行伽马调整以获得适当的亮度值
    // 伽马值为 2 足够准确 通过取线性亮度的平方根得到
    // return sqrt(Luminance(GetSource(uv)));

    #if defined(FXAA_ALPHA_CONTAINS_LUMA)
        return GetSource(uv).a; // 当亮度存储在其中时为a通道 否则为g通道
    #else
        // 由于我们在视觉上对绿色最敏感 因此计算亮度的常见替代方法是直接使用绿色通道
        // 这会降低质量 但避免了点积和平方根运算
        return GetSource(uv).g;
    #endif
}

// 采样源像素及相邻四个像素的亮度值
LumaNeighborhood GetLumaNeighborhood(float2 uv)
{
    LumaNeighborhood luma;
    luma.m = GetLuma(uv);
    luma.n = GetLuma(uv, 0.0, 1.0);
    luma.e = GetLuma(uv, 1.0, 0.0);
    luma.s = GetLuma(uv, 0.0, -1.0);
    luma.w = GetLuma(uv, -1.0, 0.0);
    luma.ne = GetLuma(uv, 1.0, 1.0);
    luma.se = GetLuma(uv, 1.0, -1.0);
    luma.sw = GetLuma(uv, -1.0, -1.0);
    luma.nw = GetLuma(uv, -1.0, 1.0);
    luma.highest = max(max(max(max(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.lowest = min(min(min(min(luma.m, luma.n), luma.e), luma.s), luma.w);
    luma.range = luma.highest - luma.lowest;
    return luma;
}

bool CanSkipFXAA(LumaNeighborhood luma)
{
    // 当亮度范围 < ( 固定阈值 和 相对阈值 * 最高亮度 )的最大值
    return luma.range <  max(_FXAAConfig.x, _FXAAConfig.y * luma.highest);
}

// 计算相邻像素的平均值
float GetSubpixelBlendFactor(LumaNeighborhood luma)
{
    float filter = 2.0 * (luma.n + luma.e + luma.s + luma.w);
    filter += luma.ne + luma.nw + luma.se + luma.sw;
    filter *= 1.0 / 12.0;
    filter = saturate(filter / luma.range);
    filter = smoothstep(0, 1, filter);
    return filter * filter * _FXAAConfig.z;
}

// 判断水平边缘
bool IsHorizontalEdge(LumaNeighborhood luma)
{
    float horizontal = 2.0 * abs(luma.n + luma.s - 2.0 * luma.m) + abs(luma.ne + luma.se - 2.0 * luma.e) + abs(luma.nw + luma.sw - 2.0 * luma.w);
    float vertical = 2.0 * abs(luma.e + luma.w - 2.0 * luma.m) + abs(luma.ne + luma.nw - 2.0 * luma.n) + abs(luma.se + luma.sw - 2.0 * luma.s);
    return horizontal >= vertical;
}

// 获取边缘数据
FXAAEdge GetFXAAEdge(LumaNeighborhood luma)
{
    FXAAEdge edge;
    edge.isHorizontal = IsHorizontalEdge(luma);
    // 通过比较中间两侧的对比度（亮度梯度）
    // 如果有一个水平边 那么北是正邻边 南是负邻边 如果有一个垂直边 那么东是正邻边 西是负邻边
    float lumaP, lumaN; //定义正负邻边
    if (edge.isHorizontal)
    {
        edge.pixelStep = GetSourceTexelSize().y;
        lumaP = luma.n;
        lumaN = luma.s;
    }
    else
    {
        edge.pixelStep = GetSourceTexelSize().x;
        lumaP = luma.e;
        lumaN = luma.w;
    }
    float gradientP = abs(lumaP - luma.m);
    float gradientN = abs(lumaN - luma.m);
    // 如果正梯度小于负梯度 则中间位于 ege 的右侧 必须沿负方向混合 通过取反步长来实现
    if (gradientP < gradientN)
    {
        edge.pixelStep = -edge.pixelStep;
        edge.lumaGradient = gradientN;
        edge.otherLuma = lumaN;
    }
    else
    {
        edge.lumaGradient = gradientP;
        edge.otherLuma = lumaP;
    }
    return edge;
}

// 获取边缘混合因子
float GetEdgeBlendFactor(LumaNeighborhood luma, FXAAEdge edge, float2 uv)
{
    // 确定用于边缘采样的UV坐标 将原始 UV 坐标向边缘偏移半个像素
    float2 edgeUV = uv;
    float2 uvStep = 0.0;
    if (edge.isHorizontal)
    {
        edgeUV.y += 0.5 * edge.pixelStep;
        uvStep.x = GetSourceTexelSize().x;
    }
    else
    {
        edgeUV.x += 0.5 * edge.pixelStep;
        uvStep.y = GetSourceTexelSize().y;
    }
    // 确定采样的亮度值与最初检测到的边缘上的亮度平均值之间的对比度
    // 如果这种对比变得太大 就偏离了边缘 FXAA使用边缘亮度梯度的四分之一作为此检查的阈值
    float edgeLuma = 0.5 * (luma.m + edge.otherLuma);
    float gradientThreshold = 0.25 * edge.lumaGradient;

    // 确定正偏移UV坐标 计算该偏移与原始边缘之间的亮度梯度 并检查它是否等于或超过阈值
    float2 uvP = edgeUV + uvStep;
    float lumaDeltaP = GetLuma(uvP) - edgeLuma;
    bool atEndP = abs(lumaDeltaP) >= gradientThreshold;
    
    UNITY_UNROLL //基于循环次数展开循环 可以持续提高性能一点点 免费的
    for (int i = 0; i < EXTRA_EDGE_STEPS && !atEndP; i++)
    {
        uvP += uvStep * edgeStepSizes[i];
        lumaDeltaP = GetLuma(uvP) - edgeLuma;
        atEndP = abs(lumaDeltaP) >= gradientThreshold;
    }
    if (!atEndP)
        uvP += uvStep * LAST_EDGE_STEP_GUESS;

    // 确定负偏移UV坐标 计算该偏移与原始边缘之间的亮度梯度 并检查它是否等于或超过阈值
    float2 uvN = edgeUV - uvStep;
    float lumaDeltaN = GetLuma(uvN) - edgeLuma;
    bool atEndN = abs(lumaDeltaN) >= gradientThreshold;
    
    UNITY_UNROLL
    for (int i = 0; i < EXTRA_EDGE_STEPS && !atEndN; i++)
    {
        uvN -= uvStep * edgeStepSizes[i];;
        lumaDeltaN = GetLuma(uvN) - edgeLuma;
        atEndN = abs(lumaDeltaN) >= gradientThreshold;
    }
    if (!atEndN)
        uvN -= uvStep * LAST_EDGE_STEP_GUESS;

    // 通过从最终偏移分量中减去适当的原始 UV 坐标分量来找到到 UV 空间中正端的距离
    float distanceToEndP, distanceToEndN;
    if (edge.isHorizontal)
    {
        distanceToEndP = uvP.x - uv.x;
        distanceToEndN = uv.x - uvN.x;
    }
    else
    {
        distanceToEndP = uvP.y - uv.y;
        distanceToEndN = uv.y - uvN.y;
    }

    // 到边最近端的距离
    float distanceToNearestEnd;
    bool deltaSign;
    if (distanceToEndP <= distanceToEndN)
    {
        distanceToNearestEnd = distanceToEndP;
        deltaSign = lumaDeltaP >= 0;
    }
    else
    {
        distanceToNearestEnd = distanceToEndN;
        deltaSign = lumaDeltaN >= 0;
    }

    if (deltaSign == (luma.m - edgeLuma >= 0))
    {
        return 0.0;
    }
    else
    {
        // 如果在边缘的正确一侧 则混合系数 0.5 减去沿边缘到最近端点的相对距离
        // 意味着越接近终点 混合得越多 并且在边缘中间根本不会混合
        return 0.5 - distanceToNearestEnd / (distanceToEndP + distanceToEndN);
    }
}

////////////////////////////////////////// Fragment ////////////////////////////////////////////////////////////////

float4 FXAAPassFragment(Varyings input) : SV_TARGET
{
    // FXAA的工作原理是有选择地降低图像的对比度 平滑视觉上明显的锯齿和孤立的像素
    // 对比度是通过比较像素的感知强度来确定的
    // 由于目标是减少我们感知到的伪影 FXAA只关注感知亮度 即伽马调整的亮度 称为亮度 像素的确切颜色并不重要 重要的是它们的亮度
    // ReSharper disable once CommentTypo
    // 因此 FXAA分析灰度图像
    LumaNeighborhood luma = GetLumaNeighborhood(input.screenUV);

    if (CanSkipFXAA(luma))
    {
        return GetSource(input.screenUV); //如果跳过fxaa 则直接返回原图
    }

    FXAAEdge edge = GetFXAAEdge(luma);

    // 为了同时应用边缘和亚像素混合 使用两者中最大的混合因子
    float blendFactor = max(GetSubpixelBlendFactor(luma), GetEdgeBlendFactor(luma, edge, input.screenUV));

    float2 blendUV = input.screenUV;
    if (edge.isHorizontal)
    {
        blendUV.y += blendFactor * edge.pixelStep;
    }
    else
    {
        blendUV.x += blendFactor * edge.pixelStep;
    }
    
    return GetSource(blendUV);
}

#endif