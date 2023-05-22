using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 自定义SRP渲染管线
/// </summary>
public partial class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();

    private bool useDynamicBatching, useGPUInstancing, useLightsPerObject;

    private ShadowSettings shadowSettings; //阴影设置

    private PostFXSettings postFXSettings; //后处理设置

    private bool allowHDR; //是否开启HDR
    
    int colorLUTResolution; // LUT位数

    public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatches, bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution) //构造函数
    {
        this.colorLUTResolution = colorLUTResolution;
        this.allowHDR = allowHDR;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.useLightsPerObject = useLightsPerObject;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatches; // 开启SRP Batch
        GraphicsSettings.lightsUseLinearIntensity = true; //light强度乘以线性颜色值
        this.shadowSettings = shadowSettings; //阴影相关设置
        this.postFXSettings = postFXSettings; //后处理设置
        InitializeForEditor();
    }

    // 必须重写Render函数
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // 循环渲染所有的摄像机
        foreach (var camera in cameras)
        {
            renderer.Render(context, camera, allowHDR, useDynamicBatching, useGPUInstancing, useLightsPerObject, shadowSettings, postFXSettings, colorLUTResolution);
        }
    }
}