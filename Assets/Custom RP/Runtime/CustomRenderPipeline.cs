using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 自定义SRP渲染管线
/// </summary>
public class CustomRenderPipeline : RenderPipeline
{
    private CameraRenderer renderer = new CameraRenderer();

    private bool useDynamicBatching, useGPUInstancing;

    private ShadowSettings shadowSettings; //阴影设置

    public CustomRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatches, ShadowSettings shadowSettings) //构造函数
    {
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatches; // 开启SRP Batch
        GraphicsSettings.lightsUseLinearIntensity = true; //light强度乘以线性颜色值
        this.shadowSettings = shadowSettings; //阴影相关设置
    }

    // 必须重写Render函数
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // 循环渲染所有的摄像机
        foreach (var camera in cameras)
        {
            renderer.Render(context, camera, useDynamicBatching, useGPUInstancing, shadowSettings);
        }
    }
}