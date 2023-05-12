using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

/// <summary>
/// 相机渲染流程设置
/// 其上级为 CustomRenderPipeline类
/// </summary>
public partial class CameraRenderer
{
    private ScriptableRenderContext context; // 定义自定义渲染管线使用的状态和绘制命令
    
    private Camera camera; // 存放摄像机渲染器当前应该渲染的摄像机
    
    private const string bufferName = "Render Camera";
    private CommandBuffer buffer = new CommandBuffer { name = bufferName }; // 创建缓冲区 并给其命名Render Camera

    private CullingResults cullingResults; // 剔除操作后的结果的结构体 包括objects, lights, and reflection probes

    private static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"), litShaderTagId = new ShaderTagId("CustomLit"); // 获取shader tag里的light mode

    private Lighting lighting = new Lighting(); //声明lighting来获取灯光数据

    private PostFXStack postFXStack = new PostFXStack(); //声明一个后处理栈来调用
    private static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");

    /// <summary>
    /// 摄像机渲染器的渲染函数 在当前渲染context的基础上渲染当前摄像机
    /// </summary>
    /// <param name="context">context</param>
    /// <param name="camera">当前的摄像机</param>
    /// <param name="useDynamicBatching">是否开启动态批处理</param>
    /// <param name="useGPUInstancing">是否开启GPU实例化</param>
    /// <param name="useLightsPerObject">是否使用每个对象的光源模式</param>
    /// <param name="shadowSettings">阴影相关设置</param>
    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;

        PrepareBuffer(); //将摄像机名字传给缓冲区名字
        PrepareForSceneWindow(); //在剔除之前执行Scene窗口UI绘制
        
        if (!Cull(shadowSettings.maxDistance))
            return; // 剔除

        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject); //灯光相关设置 传递数据 渲染阴影等
        postFXStack.SetUp(context, camera, postFXSettings); //后处理相关设置
        buffer.EndSample(SampleName);
        
        Setup(); // 初始设置
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject); // 绘制可见的几何体
        DrawUnsupportedShaders(); // 绘制不支持的shader
        DrawGizmosBeforeFX(); // 绘制编辑器图标 指定应在 ImageEffects 之前渲染的辅助图标
        if (postFXStack.IsActive)
            postFXStack.Render(frameBufferId); //执行后处理的具体操作
        DrawGizmosAfterFX(); // 指定应在 ImageEffects 之后渲染的辅助图标
        Cleanup();    //释放相关临时的纹理(后处理 阴影)
        Submit(); //提交执行
    }
    
    ////////////////////////////////////////// 相机渲染流程相关函数 /////////////////////////////////////////////////////

    bool Cull(float maxShadowDistance)
    {
        // 获取摄像机的剔除参数 如果无效(视口矩形为空 裁剪面设置无效等) 则返回false
        if (camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            // shadowDistance用于剔除的阴影距离 并且取最大阴影距离和摄像机远裁剪平面的最小值
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            // 基于通常从当前渲染的摄像机获取的 ScriptableCullingParameters 来执行剔除
            // 剔除结果绑定到将与之结合使用的 ScriptableRenderContext；剔除结果所用的内存会在渲染循环完成后得到释放
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }
    void Setup()
    {
        context.SetupCameraProperties(camera); // 调度特定于摄像机的全局着色器变量的设置 这样shader可以获取到当前帧下摄像机的信息 比如 MVP矩阵

        CameraClearFlags flags = camera.clearFlags; // 相机渲染时要清除的内容的枚举

        if (postFXStack.IsActive)
        {
            if (flags > CameraClearFlags.Color)
                flags = CameraClearFlags.Color; // 除非使用天空盒 否则始终清除深度和清除颜色
            // 获取临时的渲染纹理(RT) 此纹理的着色器属性名称 像素宽度 像素高度 深度缓冲区位 纹理过滤模式 渲染纹理的格式
            buffer.GetTemporaryRT(frameBufferId, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Bilinear, RenderTextureFormat.Default);
            // 设置渲染目标 加载操作:忽视 即不加载到区块内存中 存储操作:储存在内存中
            buffer.SetRenderTarget(frameBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color, flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear); //清除渲染目标 包括深度 颜色 模板缓冲
        
        buffer.BeginSample(SampleName); // 在Profiler和Frame Debugger中开启对缓冲区的监测

        ExecuteBuffer(); //提交指令

    }

    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
    {
        PerObjectData lightsPerObjectFlags =
            useLightsPerObject ? PerObjectData.LightData | PerObjectData.LightIndices : PerObjectData.None; // 是否应使用每个对象的光源模式
        // 考虑透明与不透明物体 正确的渲染顺序应该是 不透明物体 > 天空盒 > 透明物体
        
        var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque }; // 决定物体的绘制顺序 当前：不透明对象的典型排序 即从前往后渲染
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching, //配置动态批处理
            enableInstancing = useGPUInstancing, // 配置GPU实例化
            perObjectData = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps | PerObjectData.ShadowMask | PerObjectData.LightProbe | PerObjectData.OcclusionProbe | PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume | lightsPerObjectFlags //设置反射探针 光照贴图 阴影蒙版 灯光探针 遮挡探针 LPPV LPPV遮挡数据
        }; // 决定摄像机支持的shader pass 和绘制顺序等的配置
        drawingSettings.SetShaderPassName(1, litShaderTagId); //添加lit shader
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 决定过滤哪些可见objects的配置 包括支持的RenderQueue等
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings); // 渲染cullingResults内的几何体 不透明物体
        
        context.DrawSkybox(camera); // 调度天空盒的绘制

        sortingSettings.criteria = SortingCriteria.CommonTransparent; // 从后往前
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent; //过滤出属于透明队列的物体
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings); //再次绘制 透明物体
        
    }

    void Submit()
    {
        buffer.EndSample(SampleName); // 结束监测

        ExecuteBuffer(); //提交指令
        
        context.Submit(); // 将所有调度命令都提交给渲染循环来执行
    }

    // 执行并清除缓冲区 默认这两操作一起执行的 为了复用
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer); // 调度自定义图形命令缓冲区的执行 即提交指令 并不执行 需要context.Submit();
        buffer.Clear(); // 清除缓冲区中的所有命令
    }

    // 释放相关纹理
    void Cleanup()
    {
        lighting.Cleanup(); //灯光相关清理
        if (postFXStack.IsActive)
        {
            buffer.ReleaseTemporaryRT(frameBufferId); //释放后处理操作用到的临时纹理
        }
    }
}
