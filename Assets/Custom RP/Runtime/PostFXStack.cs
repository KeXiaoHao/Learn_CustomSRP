using UnityEngine;
using UnityEngine.Rendering;

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
        BloomPrefilter,
        Copy
    }

    int bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling"),
        bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter"),
        bloomThresholdId = Shader.PropertyToID("_BloomThreshold"),
        bloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
        fxSourceId = Shader.PropertyToID("_PostFXSource"),
        fxSource2Id = Shader.PropertyToID("_PostFXSource2");

    public bool IsActive => settings != null; //当后处理设置不为空时返回true
    
    const int maxBloomPyramidLevels = 16;   //定义最大的bloom分层级别
    int bloomPyramidId;
    
    public PostFXStack ()       //创建一个构造函数  跟踪bloom中的纹理
    {
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }
    
    ////////////////////////////////////////////////// buffer处理逻辑 //////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 后处理有关设置
    /// </summary>
    public void SetUp(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
    {
        this.context = context;
        this.camera = camera;
        //当有场景视图摄像机或游戏视图摄像机时 就启用后处理settings 否则为空
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
        ApplySceneViewState();
    }

    /// <summary>
    /// 后处理渲染流程
    /// </summary>
    public void Render(int sourceID)
    {
        DoBloom(sourceID);
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
    /// 后处理的Bloom效果
    /// </summary>
    /// <param name="sourceId">源纹理 输入纹理</param>
    void DoBloom(int sourceId)
    {
        buffer.BeginSample("Bloom");
        PostFXSettings.BloomSettings bloom = settings.Bloom; //bloom参数配置
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
        
        // 如果我们最终完全跳过bloom 将不得不中止并执行复制
        if (bloom.maxInterations == 0 || bloom.intensity <= 0f || height < bloom.downscaleLimit * 2 || width < bloom.downscaleLimit * 2)
        {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            buffer.EndSample("Bloom");
            return;
        }
        
        // 亮度阈值曲线计算参数
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);
        
        RenderTextureFormat format = RenderTextureFormat.Default;
        
        // 预过滤 以一半分辨率作为起点进行bloom的金字塔
        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        //预先进行一次降采样 降低bloom的消耗
        Draw(sourceId, bloomPrefilterId, Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        
        int fromId = bloomPrefilterId, toId = bloomPyramidId + 1;
        
        int i;
        // 遍历所有bloom块 并复制作为新的源 再尺寸减半 递增下去
        for (i = 0; i < bloom.maxInterations; i++)
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

        buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f); //是否需要三线性过滤
        buffer.SetGlobalFloat(bloomIntensityId, 1f);
        
        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            // 然后递减循环 释放所有的纹理
            for (i -= 1; i > 0; i--)
            {
                buffer.SetGlobalTexture(fxSource2Id, toId + 1);
                Draw(fromId, toId, Pass.BloomCombine);
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
        
        buffer.SetGlobalFloat(bloomIntensityId, bloom.intensity);
        
        buffer.SetGlobalTexture(fxSource2Id, sourceId);
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);
        buffer.ReleaseTemporaryRT(fromId);
        
        buffer.EndSample("Bloom");
    }
}
