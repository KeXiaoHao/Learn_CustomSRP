using UnityEngine;
using UnityEngine.Rendering;
//类似于使用命名空间，但用于类。它使类或结构体的所有常量、静态和类型成员都可以直接访问，而无需完全限定它们
using static PostFXSettings;

public partial class PostFXStack
{
    private const string bufferName = "Post FX";
    private CommandBuffer buffer = new CommandBuffer() { name = bufferName };
    private ScriptableRenderContext context;
    private Camera camera;

    private PostFXSettings settings;

    enum Pass //要操作的Shader的Pass
    {
        BloomHorizontal,
        BloomVertical,
        BloomCombine,
        BloomScatter,
        BloomPrefilter,
        BloomPrefilterFireflies,
        BloomScatterFinal,
        Copy,
        ColorGradingNone,
        ToneMappingACES,
        ToneMappingNeutral,
        ToneMappingReinhard,
        Final,
        FinalRescale
    }

    int bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomResultId = Shader.PropertyToID("_BloomResult"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
        colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments"),
        colorFilterId = Shader.PropertyToID("_ColorFilter"),
        whiteBalanceId = Shader.PropertyToID("_WhiteBalance"),
        splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows"),
        splitToningHighlightsId = Shader.PropertyToID("_SplitToningHighlights"),
        channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed"),
        channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen"),
        channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue"),
        smhShadowsId = Shader.PropertyToID("_SMHShadows"),
        smhMidtonesId = Shader.PropertyToID("_SMHMidtones"),
        smhHighlightsId = Shader.PropertyToID("_SMHHighlights"),
        smhRangeId = Shader.PropertyToID("_SMHRange"),
        colorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters"),
        colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC"),
        colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT"),
        copyBicubicId = Shader.PropertyToID("_CopyBicubic"),
        finalResultId = Shader.PropertyToID("_FinalResult");

    public bool IsActive => settings != null; //当后处理设置不为空时返回true
    
    const int maxBloomPyramidLevels = 16;   //定义最大的bloom分层级别
    int bloomPyramidId;
    
    public PostFXStack ()       //创建一个构造函数  跟踪bloom中的纹理
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 0; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    private bool useHDR;
    
    int colorLUTResolution;

    private CameraSettings.FinalBlendMode finalBlendMode;
    int finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
        finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");

    private Vector2Int bufferSize;
    
    CameraBufferSettings.BicubicRescalingMode bicubicRescaling; //双三次采样
    
    ////////////////////////////////////////////////// buffer处理逻辑 //////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 后处理有关设置
    /// </summary>
    public void SetUp(ScriptableRenderContext context, Camera camera, Vector2Int bufferSize, PostFXSettings settings, bool useHDR, int colorLUTResolution, CameraSettings.FinalBlendMode finalBlendMode, CameraBufferSettings.BicubicRescalingMode bicubicRescaling)
    {
        this.bicubicRescaling = bicubicRescaling;
        this.bufferSize = bufferSize;
        this.finalBlendMode = finalBlendMode;
        this.colorLUTResolution = colorLUTResolution;
        this.context = context;
        this.camera = camera;
        //当有场景视图摄像机或游戏视图摄像机时 就启用后处理settings 否则为空
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        this.useHDR = useHDR;
        ApplySceneViewState();
    }

    /// <summary>
    /// 后处理渲染流程
    /// </summary>
    public void Render(int sourceID)
    {
        if (DoBloom(sourceID))
        {
            DoColorGradingAndToneMapping(bloomResultId);
            buffer.ReleaseTemporaryRT(bloomResultId);
        }
        else
        {
            DoColorGradingAndToneMapping(sourceID);
        }
        context.ExecuteCommandBuffer(buffer); //提交指令
        buffer.Clear(); //清除缓冲区中的所有命令
    }
    
    ////////////////////////////////////////////////// 后处理操作方法 //////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 临时RT的绘制方法
    /// </summary>
    /// <param name="form">源纹理</param>
    /// <param name="to">目标纹理</param>
    /// <param name="pass">操作的Pass</param>
    void Draw(RenderTargetIdentifier form, RenderTargetIdentifier to, Pass pass )
    {
        buffer.SetGlobalTexture(fxSourceId, form);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        // DrawProcedural 绘制程序化几何体
        // matrix 要使用的变换矩阵 material 要使用的材质 shaderPass 要使用着色器的哪个通道 topology 程序化几何体的拓扑 vertexCount 要渲染的顶点数
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }
    
    /// <summary>
    /// 多相机的临时RT的绘制方法
    /// </summary>
    /// <param name="form">源纹理</param>
    /// <param name="to">目标纹理</param>
    /// <param name="pass">操作的Pass</param>
    void DrawFinal(RenderTargetIdentifier form, Pass pass)
    {
        buffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.source);
        buffer.SetGlobalFloat(finalDstBlendId, (float)finalBlendMode.destination);
        buffer.SetGlobalTexture(fxSourceId, form);
        buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
            finalBlendMode.destination == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
            RenderBufferStoreAction.Store);
        buffer.SetViewport(camera.pixelRect); //设置渲染视口为当前相机在屏幕上的渲染位置(像素坐标)
        // DrawProcedural 绘制程序化几何体
        // matrix 要使用的变换矩阵 material 要使用的材质 shaderPass 要使用着色器的哪个通道 topology 程序化几何体的拓扑 vertexCount 要渲染的顶点数
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    /// <summary>
    /// 后处理的Bloom效果
    /// </summary>
    /// <param name="sourceId">源纹理 输入纹理</param>
    bool DoBloom(int sourceId)
    {
        PostFXSettings.BloomSettings bloom = settings.Bloom; //bloom参数配置
        int width, height; //缓冲区的宽高
        if (bloom.ignoreRenderScale)
        {
            width = camera.pixelWidth / 2;
            height = camera.pixelHeight / 2;
        }
        else
        {
            width = bufferSize.x / 2;
            height = bufferSize.y / 2;
        }
        
        // 如果我们最终完全跳过bloom 将不得不中止并执行复制
        if (bloom.maxInterations == 0 || bloom.intensity <= 0f || height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
        {
            return false;
        }
        
        buffer.BeginSample("Bloom");
        
        // 亮度阈值曲线计算参数
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);
        
        RenderTextureFormat format = useHDR? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        
        // 预过滤 以一半分辨率作为起点进行bloom的金字塔
        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        //预先进行一次降采样 降低bloom的消耗
        Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        
        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
        
        int i = 0;
        // 遍历所有bloom块 并复制作为新的源 再尺寸减半 递增下去
        for (; i < bloom.maxInterations; i++)
        {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }
            int midId = toId - 1;
            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }
        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        
        buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);

        buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f); //是否需要三线性过滤
        
        Pass combinePass, finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomCombine;
            buffer.SetGlobalFloat(bloomIntensityId, 1f);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }
        
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            // 然后递减循环 释放所有的纹理
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, combinePass);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        }
        else //当迭代次数只有一次时 应该直接跳过剩个采样阶段 所以只需要释放用于第一次水平通道的纹理
        {
            buffer.ReleaseTemporaryRT(bloomPyramidId);
        }
        
        buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
        
        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        buffer.GetTemporaryRT(bloomResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, format);
        Draw(fromId, bloomResultId, finalPass);
        buffer.ReleaseTemporaryRT(fromId);
        
        buffer.EndSample("Bloom");
        return true;
    }
    
    ////////////////////////////////////////////////// 颜色分级和色调映射 //////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 配置颜色调整
    /// </summary>
    void ConfigureColorAdjustments()
    {
        ColorAdjustmentsSettings colorAdjustments = settings.ColorAdjustments;
        buffer.SetGlobalVector(colorAdjustmentsId, new Vector4(
            Mathf.Pow(2f, colorAdjustments.postExposure),   // 曝光以postExposure为单位测量 我们必须将 2 提高到配置的曝光值的幂
            colorAdjustments.contrast * 0.01f + 1f,              // 对比度转换为 0–2 范围
            colorAdjustments.hueShift * (1f / 360f),             // 将色相偏移转换为 -1到1
            colorAdjustments.saturation * 0.01f + 1f));         // 饱和度转换为 0–2 范围
        buffer.SetGlobalColor(colorFilterId, colorAdjustments.colorFilter.linear); //必须处于线性颜色空间中
    }

    /// <summary>
    /// 配置白平衡
    /// </summary>
    void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalance = settings.WhiteBalance;
        // ColorBalanceToLMSCoeffs 将白平衡参数转换为LMS系数
        buffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(whiteBalance.temperature, whiteBalance.tint));
    }

    /// <summary>
    /// 配置色调分离
    /// </summary>
    void ConfigureSplitToning()
    {
        SplitToningSettings splitToning = settings.SplitToning;
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        buffer.SetGlobalColor(splitToningShadowsId, splitColor);
        buffer.SetGlobalColor(splitToningHighlightsId, splitToning.highlights);
    }

    /// <summary>
    /// 配置通道混合
    /// </summary>
    void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = settings.ChannelMixer;
        buffer.SetGlobalVector(channelMixerRedId, channelMixer.red);
        buffer.SetGlobalVector(channelMixerGreenId, channelMixer.green);
        buffer.SetGlobalVector(channelMixerBlueId, channelMixer.blue);
    }

    /// <summary>
    /// 配置阴影中间调高光
    /// </summary>
    void ConfigureShadowsMidtonesHighlights()
    {
        ShadowsMidtonesHighlightsSettings smh = settings.ShadowsMidtonesHighlights;
        buffer.SetGlobalColor(smhShadowsId, smh.shadows.linear);
        buffer.SetGlobalColor(smhMidtonesId, smh.midtones.linear);
        buffer.SetGlobalColor(smhHighlightsId, smh.highlights.linear);
        buffer.SetGlobalVector(smhRangeId, new Vector4(smh.shadowsStart, smh.shadowsEnd, smh.highlightsStart, smh.highLightsEnd));
    }
    
    /// <summary>
    /// 颜色分级和色调映射处理
    /// </summary>
    /// <param name="sourceId">源纹理</param>
    void DoColorGradingAndToneMapping(int sourceId)
    {
        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHighlights();
        
        // LUT是3D的 但常规着色器无法渲染为3D纹理 我们将使用宽2D纹理来模拟3D纹理 方法是将2D切片放置在一行中
        int lutHeight = colorLUTResolution; // LUT纹理的高度等于配置的分辨率
        int lutWidth = lutHeight * lutHeight; // 其宽度等于分辨率的平方
        buffer.GetTemporaryRT(colorGradingLUTId, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
        buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1f)));
        
        ToneMappingSettings.Mode mode = settings.ToneMapping.mode;
        Pass pass = Pass.ColorGradingNone + (int)mode;
        buffer.SetGlobalFloat(colorGradingLUTInLogId, useHDR && pass != Pass.ColorGradingNone ? 1f : 0f);
        Draw(sourceId, colorGradingLUTId, pass);
        
        buffer.SetGlobalVector(colorGradingLUTParametersId, new Vector4(1f / lutWidth, 1f / lutHeight, lutHeight - 1f));
        
        if (bufferSize.x == camera.pixelWidth)
        {
            DrawFinal(sourceId, Pass.Final);
        }
        else
        {
            // 如果我们需要重新缩放渲染 那么我们必须绘制两次
            buffer.SetGlobalFloat(finalSrcBlendId, 1f);
            buffer.SetGlobalFloat(finalDstBlendId, 0f); // // 将最终混合模式设置为 One Zero
            // 首先获取与当前缓冲区大小匹配的新临时渲染纹理 当我们在其中存储LDR颜色时 我们可以使用默认的渲染纹理格式
            buffer.GetTemporaryRT(finalResultId, bufferSize.x, bufferSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
            // 然后使用 Pass.Final 通道执行常规绘制 并将最终混合模式设置为 One Zero
            Draw(sourceId, finalResultId, Pass.Final);
            bool bicubicSampling =
                bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpAndDown ||
                bicubicRescaling == CameraBufferSettings.BicubicRescalingMode.UpOnly &&
                bufferSize.x < camera.pixelWidth; //双三次采样仅用于上下模式 或者如果使用缩小的渲染比例 则仅向上模式
            buffer.SetGlobalFloat(copyBicubicId, bicubicSampling ? 1f : 0f); //是否采样双三次采样
            // 使用最终的 Pass.FinalRescale 通道执行最终的绘制 然后释放临时缓冲区
            DrawFinal(finalResultId, Pass.FinalRescale);
            buffer.ReleaseTemporaryRT(finalResultId);
        }
        buffer.ReleaseTemporaryRT(colorGradingLUTId);
    }
}
