using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 自定义SRP渲染管线资源
/// </summary>
[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    private bool DynamicBatching = true, GPUInstancing = true, SRPBatches = true, useLightsPerObject = true;

    [SerializeField]
    private ShadowSettings shadows = default;

    [SerializeField]
    private PostFXSettings postFXSettings = default;
    
    // 重写创建实际的RenderPipeline函数
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(DynamicBatching, GPUInstancing, SRPBatches, useLightsPerObject, shadows, postFXSettings);
    }
}
