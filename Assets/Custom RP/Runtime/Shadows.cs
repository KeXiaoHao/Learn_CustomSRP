using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 所有阴影相关逻辑处理的类
/// 其上级为 Lighting类
/// </summary>
public class Shadows
{
    private const string bufferName = "Shadow";
    private CommandBuffer buffer = new CommandBuffer { name = bufferName }; //准备开启名为 shadow 的缓冲区

    private ScriptableRenderContext context; //渲染指令

    private CullingResults cullingResults;   //剔除结果数据体
    
    private ShadowSettings settings;         //阴影设置参数

    private const int maxShadowedDirectionalLightCount = 1; //定义最大数量的定向光源阴影

    private int ShaowedDirectionalLightCount; //定向光阴影的数量

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;

        ShaowedDirectionalLightCount = 0; //先初始为0
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer); //提交指令到缓冲区
        buffer.Clear();                       //清除缓冲区
    }

    /// <summary>
    /// 定向光阴影的相关数据
    /// </summary>
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex; //可见定向光的索引
    }
    
    ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    /// <summary>
    /// 在阴影图集中为光源的阴影贴图保留空间 并存储渲染它们所需的信息
    /// <example>可见阴影光源的索引</example>
    /// </summary>
    public void ReserveDirectionalShadow(Light light, int visibleLightIndex)
    {
        // 当前定向光阴影数量 < 最大定向光阴影数量 且 场景内灯光的阴影模式设置不是无 且 灯光的阴影强度不为零
        if (ShaowedDirectionalLightCount < maxShadowedDirectionalLightCount && light.shadows != LightShadows.None && light.shadowStrength >0f)
        {
            ShadowedDirectionalLights[ShaowedDirectionalLightCount++] = new ShadowedDirectionalLight
                { visibleLightIndex = visibleLightIndex }; // 存储光源的可见索引并增加计数
        }
    }
}
