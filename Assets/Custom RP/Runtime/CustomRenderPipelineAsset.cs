using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 自定义SRP渲染管线资源
/// </summary>
[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public partial class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    CameraBufferSettings cameraBuffer = new CameraBufferSettings
    {
        allowHDR = true, renderScale = 1f,
        fxaa = new CameraBufferSettings.FXAA { fixedThreshold = 0.0833f, relativeThreshold = 0.166f, subpixelBlending = 0.75f}
    };
    
    [SerializeField]
    private bool DynamicBatching = true, GPUInstancing = true, SRPBatches = true, useLightsPerObject = true;

    [SerializeField]
    private ShadowSettings shadows = default;

    [SerializeField]
    private PostFXSettings postFXSettings = default;

    // [SerializeField]
    // private bool allowHDR = true;
    
    public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

    [SerializeField]
    ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
    
    [SerializeField]
    Shader cameraRendererShader = default;

    // 重写创建实际的RenderPipeline函数
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(cameraBuffer,DynamicBatching, GPUInstancing, SRPBatches, useLightsPerObject, shadows, postFXSettings, (int)colorLUTResolution, cameraRendererShader);
    }
}
