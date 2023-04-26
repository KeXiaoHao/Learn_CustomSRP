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
    
    private ShadowSettings settings;         //声明阴影设置参数类

    private const int maxShadowedDirectionalLightCount = 4; //定义最大数量的定向光源阴影

    private int ShaowedDirectionalLightCount; //定向光阴影的数量

    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"), //获取定向光的阴影图集的shader属性
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"); //世界空间下的阴影纹理坐标

    private static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount];

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
    /// 释放阴影的临时渲染纹理并保留
    /// </summary>
    public void Cleanup()
    {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
    
    ////////////////////////////////////////// 各光源类型的阴影渲染 /////////////////////////////////////////////////////
    
    /// <summary>
    /// 创建阴影图集并渲染
    /// </summary>
    public void Render()
    {
        if (ShaowedDirectionalLightCount > 0)
            RenderDirectionalShadows();
        else
        {
            // 在不需要阴影时获得1x1虚拟纹理 避免额外的shader变体 防止webgl 2.0等平台报错
            // 因为此类平台纹理和采样器绑定在一起 当加载带有此shader的材质时缺少纹理时 会失败
            buffer.GetTemporaryRT(dirShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        }
    }

    /// <summary>
    /// 定向光的阴影渲染
    /// </summary>
    void RenderDirectionalShadows()
    {
        int atlasSize = (int)settings.directional.atlasSize;
        // GetTemporaryRT 获取临时渲染纹理 Shader属性id 宽 高 深度缓冲区的位数 过滤模式 纹理类型
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        // 指示GPU渲染到此纹理而不是相机的目标
        // RenderBufferLoadAction.DontCare 指示GPU不考虑该RenderBuffer的现有内容 意味着不需要将RenderBuffer内容加载到区块内存中 从而实现性能提升
        // RenderBufferStoreAction.Store   需要存储到 RAM 中的 RenderBuffer 内容
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        buffer.ClearRenderTarget(true, false, Color.clear);
        
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        int split = ShaowedDirectionalLightCount <= 1 ? 1 : 2;
        int tileSize = atlasSize / split;
        
        for (int i = 0; i < ShaowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize); //要渲染单个阴影 添加一个方法重载 然后调用 利用循环渲染所有阴影
        }
        
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices); //一旦渲染了所有阴影光源 通过缓冲区上调用传递给Shader
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        
        // ShadowDrawingSettings其构造函数 创建正确的配置 其中包含剔除结果和可见光索引
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        
        // 计算方向光的视图和投影矩阵以及阴影分割数据
        // 输入 可见光源索引 级联索引 级联数量 级联比率 阴影贴图分辨率 光源的近平面偏移量
        // 输出 计算出的视图矩阵 计算出的投影矩阵 计算出的级联阴影数据
        cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, 0, 1, Vector3.zero, 
            tileSize, 0f, 
            out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);

        shadowSettings.splitData = splitData; //级联阴影数据中含有如何剔除阴影投射对象的信息 传递给shadowSettings
  
        //将光源的阴影投影矩阵和视图矩阵相乘 得到从世界空间到光源空间的转换矩阵
        dirShadowMatrices[index] = ConvertToAtlasMatrix(projMatrix * viewMatrix, SetTileViewport(index, split, tileSize), split);
        
        buffer.SetViewProjectionMatrices(viewMatrix, projMatrix); //在缓冲区上调用 来应用视图矩阵和投影矩阵
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings); // 为单个光源调用阴影投射物的绘制
    }

    /// <summary>
    /// 分割渲染视口 让每个光源提供自己的图块来渲染 防止各光源重叠在一个图块上
    /// </summary>
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer) //使用反向的 Z 缓冲区，则反向z的值
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        // 由于裁剪空间的范围是-1到1 而纹理坐标和深度的范围是0到1 所以将xyz缩放偏移一般 再对xy应用视口偏移
        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }
    
    ////////////////////////////////////////// 各光源类型阴影的剔除筛选 /////////////////////////////////////////////////////

    /// <summary>
    /// 定向光阴影的相关数据
    /// </summary>
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex; //可见定向光的索引
    }
    
    /// <summary>
    /// 目前通过函数得到已存储的可见光阴影的索引
    /// </summary>
    ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    /// <summary>
    /// 在阴影图集中为光源的阴影贴图保留空间 并存储渲染它们所需的信息
    /// <example>可见阴影光源的索引 阴影强度 阴影图块索引</example>
    /// </summary>
    public Vector2 ReserveDirectionalShadow(Light light, int visibleLightIndex)
    {
        // 当前定向光阴影数量 < 最大定向光阴影数量
        // 且 场景内灯光的阴影模式设置不是无 且 灯光的阴影强度不为零
        // 且 返回封装了可见阴影投射物的包围盒 如果光源影响了场景中至少一个阴影投射对象 则为 true
        if (ShaowedDirectionalLightCount < maxShadowedDirectionalLightCount 
            && light.shadows != LightShadows.None && light.shadowStrength >0f 
            && cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            ShadowedDirectionalLights[ShaowedDirectionalLightCount] = new ShadowedDirectionalLight
                { visibleLightIndex = visibleLightIndex }; // 存储光源的可见索引并增加计数
            return new Vector2(light.shadowStrength, ShaowedDirectionalLightCount++);
        }

        return Vector2.zero;
    }
}
