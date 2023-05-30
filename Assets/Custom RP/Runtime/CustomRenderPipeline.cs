using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 自定义SRP渲染管线
/// </summary>
public partial class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer;

    private bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

    private ShadowSettings shadowSettings; //阴影设置

    private PostFXSettings postFXSettings; //后处理设置

    CameraBufferSettings cameraBufferSettings;
    
    int colorLUTResolution; // LUT位数

    public CustomRenderPipeline(CameraBufferSettings cameraBufferSettings, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatches, bool useLightsPerObject,
        ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution, Shader cameraRendererShader) //构造函数
    {
        this.colorLUTResolution = colorLUTResolution;
        this.cameraBufferSettings = cameraBufferSettings;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatches; // 开启SRP Batch
        GraphicsSettings.lightsUseLinearIntensity = true; //light强度乘以线性颜色值
        this.shadowSettings = shadowSettings; //阴影相关设置
        this.postFXSettings = postFXSettings; //后处理设置
        InitializeForEditor();
        renderer = new CameraRenderer(cameraRendererShader);
    }

    // 必须重写Render函数
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // 循环渲染所有的摄像机
        foreach (var camera in cameras)
        {
            renderer.Render(context, camera, cameraBufferSettings, useDynamicBatching, useGPUInstancing, useLightsPerObject, shadowSettings, postFXSettings, colorLUTResolution);
        }
    }
    
    
    // 在管线被释放时清理和重置委托
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        DisposeForEditor();
        renderer.Dispose();
    }
}