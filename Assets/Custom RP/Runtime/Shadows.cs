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

    private const int maxShadowedDirectionalLightCount = 4, //定义最大数量的定向光源阴影
        maxShadowedOtherLightCount = 16; // //定义最大数量的其他光源阴影
    private const int maxCascade = 4; //定义最大级数的级联阴影

    private int ShaowedDirectionalLightCount, shadowedOtherLightCount; //定向光和其他光阴影的数量

    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"), //获取定向光的阴影图集的shader属性
        dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"), //世界空间下的阴影纹理坐标
        otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),                    //获取其他光的阴影图集的shader属性
        otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),              //其他光阴影的世界空间下的阴影纹理坐标
        otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),
        cascadeCountId = Shader.PropertyToID("_CascadeCount"), //级联阴影级数
        cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"), //级联阴影剔除球体
        cascadeDataId = Shader.PropertyToID("_CascadeData"), //级联数据
        shadowAtlasSizedId = Shader.PropertyToID("_ShadowAtlasSize"), //阴影图集大小
        shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"), //淡出阴影距离
        shadowPancakingId = Shader.PropertyToID("_ShadowPancaking"); 

    private static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascade],
        otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

    private static Vector4[] cascadeCullingSphere = new Vector4[maxCascade], // xyz 球心位置坐标 z 半径
            cascadeData = new Vector4[maxCascade], // 级联数据矢量数组
            otherShadowTiles = new Vector4[maxShadowedOtherLightCount]; 

    private static string[] directionalFilterKeywords =
        { "_DIRECTIONAL_PCF3", "_DIRECTIONAL_PCF5", "_DIRECTIONAL_PCF7", };  //为过滤模式创建shader变体
    static string[] otherFilterKeywords =
        { "_OTHER_PCF3", "_OTHER_PCF5", "_OTHER_PCF7", };
    
    static string[] cascadeBlendKeywords =
        { "_CASCADE_BLEND_SOFT", "_CASCADE_BLEND_DITHER" }; //级联阴影混合模式

    private static string[] shadowMaskKeywords = { "_SHADOW_MASK_ALWAYS", "_SHADOW_MASK_DISTANCE" }; //阴影蒙版
    private bool useShadowMask; // 是否使用阴影蒙版
    
    Vector4 atlasSizes;
    
    
    
    ////////////////////////////////////////// 缓冲区阴影相关流程操作 /////////////////////////////////////////////////////

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        this.context = context;
        this.cullingResults = cullingResults;
        this.settings = settings;

        ShaowedDirectionalLightCount = shadowedOtherLightCount = 0; //先初始为0
        useShadowMask = false;
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
        if (shadowedOtherLightCount > 0)
            buffer.ReleaseTemporaryRT(otherShadowAtlasId);
        ExecuteBuffer();
    }

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
        
        if (shadowedOtherLightCount > 0) 
            RenderOtherShadows();
        else
        {
            buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
        }
        
        buffer.BeginSample(bufferName);
        SetKerwords(shadowMaskKeywords, useShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
        
        buffer.SetGlobalInt(cascadeCountId, ShaowedDirectionalLightCount > 0 ? settings.directional.cascadeCount : 0); //传递级联阴影级数
        
        float f = 1f - settings.directional.cascadeFade;
        buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f))); //传递淡出阴影距离
        
        buffer.SetGlobalVector(shadowAtlasSizedId, atlasSizes);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
    
    ////////////////////////////////////////// 各光源类型的阴影渲染 /////////////////////////////////////////////////////

    /// <summary>
    /// 定向光的阴影渲染
    /// </summary>
    void RenderDirectionalShadows()
    {
        //在GPU上申请一张RT来存放Depth Map
        int atlasSize = (int)settings.directional.atlasSize;
        atlasSizes.x = atlasSize;
        atlasSizes.y = 1f / atlasSize;
        // GetTemporaryRT 获取临时渲染纹理 Shader属性id 宽 高 深度缓冲区的位数 过滤模式 纹理类型
        buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        // 指示GPU渲染到此纹理而不是相机的目标
        // RenderBufferLoadAction.DontCare 指示GPU不考虑该RenderBuffer的现有内容 意味着不需要将RenderBuffer内容加载到区块内存中 从而实现性能提升
        // RenderBufferStoreAction.Store   需要存储到 RAM 中的 RenderBuffer 内容
        buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        buffer.ClearRenderTarget(true, false, Color.clear);
        
        buffer.SetGlobalFloat(shadowPancakingId, 1f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        // 级联阴影相关参数操作
        int tiles = ShaowedDirectionalLightCount * settings.directional.cascadeCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        int tileSize = atlasSize / split;
        
        for (int i = 0; i < ShaowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize); //要渲染单个阴影 添加一个方法重载 然后调用 利用循环渲染所有阴影
        }
        
        buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSphere); //传递级联剔除球体
        buffer.SetGlobalVectorArray(cascadeDataId, cascadeData); //传递级联数据
        buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices); //一旦渲染了所有阴影光源 通过缓冲区上调用传递给Shader
        
        SetKerwords(directionalFilterKeywords, (int)settings.directional.filter - 1); //启用或关闭阴影贴图过滤模式的shader关键字
        SetKerwords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1); //阴影混合模式的关键字传递
        // buffer.SetGlobalVector(shadowAtlasSizedId, new Vector4(atlasSize, 1f / atlasSize)); //传递阴影图集大小
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        
        // ShadowDrawingSettings其构造函数 创建正确的配置 其中包含剔除结果和可见光索引
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex){useRenderingLayerMaskTest = true};
        
        // 级联阴影参数
        int cascadeCount = settings.directional.cascadeCount;
        int tileOffset = index * cascadeCount; //当前要渲染的第一个tile在shadow atlas中的索引
        Vector3 ratios = settings.directional.CascadeRatios;

        float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade); //剔除因子
        
        float tileScale = 1f / split;

        for (int i = 0; i < cascadeCount; i++)
        {
            // 计算方向光的视图和投影矩阵以及阴影分割数据
            // 输入 可见光源索引 级联索引 级联数量 级联比率 阴影贴图分辨率 光源的近平面偏移量
            // 输出 计算出的视图矩阵 计算出的投影矩阵 计算出的级联阴影数据
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount, ratios, 
                tileSize, light.nearPlaneOffset, 
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);

            //应用于剔除球体半径的乘数 值必须介于0到1范围内 值越大 Unity剔除的对象越多 越小 级联共享的渲染对象越多
            //如果使用较小值 可在不同级联之间混合 因为这样它们会共享对象
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            
            shadowSettings.splitData = splitData; //级联阴影数据中含有如何剔除阴影投射对象的信息 传递给shadowSettings

            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize); //级联阴影有关参数的计算
            }

            int tileIndex = tileOffset + i; // 当前要渲染的tile区域
  
            //将光源的阴影投影矩阵和视图矩阵相乘 得到从世界空间到光源空间的转换矩阵
            dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), tileScale);
        
            buffer.SetViewProjectionMatrices(viewMatrix, projMatrix); //在缓冲区上调用 来应用视图矩阵和投影矩阵
            
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias); //全局深度偏差
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings); // 为单个光源调用阴影投射物的绘制
            // bias:缩放 GPU 的最小可解析深度缓冲区值以产生恒定的深度偏移
            // slopBias:缩放最大 Z 斜率（也称为深度坡度）以为每个面生成可变深度偏移
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }
    
    /// <summary>
    /// 其他光的阴影渲染
    /// </summary>
    void RenderOtherShadows()
    {
        int atlasSize = (int)settings.other.atlasSize;
        atlasSizes.z = atlasSize;
        atlasSizes.w = 1f / atlasSize;
        // GetTemporaryRT 获取临时渲染纹理 Shader属性id 宽 高 深度缓冲区的位数 过滤模式 纹理类型
        buffer.GetTemporaryRT(otherShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
        // 指示GPU渲染到此纹理而不是相机的目标
        // RenderBufferLoadAction.DontCare 指示GPU不考虑该RenderBuffer的现有内容 意味着不需要将RenderBuffer内容加载到区块内存中 从而实现性能提升
        // RenderBufferStoreAction.Store   需要存储到 RAM 中的 RenderBuffer 内容
        buffer.SetRenderTarget(otherShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        
        buffer.ClearRenderTarget(true, false, Color.clear);
        
        buffer.SetGlobalFloat(shadowPancakingId, 0f);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();

        // 级联阴影相关参数操作
        int tiles = shadowedOtherLightCount;
        int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4; //根据光源数量来决定将阴影贴图划分几次
        int tileSize = atlasSize / split;
        
        for (int i = 0; i < shadowedOtherLightCount;)
        {
            if (shadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6; //每个点光源需要6个tile
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }
        
        buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices); //一旦渲染了所有阴影光源 通过缓冲区上调用传递给Shader
        buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
        
        SetKerwords(otherFilterKeywords, (int)settings.other.filter - 1); //启用或关闭阴影贴图过滤模式的shader关键字
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }

    /// <summary>
    /// 渲染点光源的阴影
    /// </summary>
    void RenderPointShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex){useRenderingLayerMaskTest = true};
        
        float texelSize = 2f / tileSize;
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        float tileScale = 1f / split;
        
        //增加点光源阴影渲染时透视投影矩阵的fov值 使得距离光源1m处的tile的世界空间大小大于2
        float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for (int i = 0; i < 6; i++)
        {
            cullingResults.ComputePointShadowMatricesAndCullingPrimitives(light.visibleLightIndex, (CubemapFace)i, fovBias,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
            // 发生这种情况是因为Unity为点光源渲染阴影的方式。它将它们上下颠倒，从而颠倒了三角形的缠绕顺序。
            // 通常，从光的角度绘制正面，但是现在可以绘制背面。这可以防止大多数粉刺，但会引起漏光。
            // 我们不能阻止翻转，但是可以通过对从ComputePointShadowMatricesAndCullingPrimitives中获得的视图矩阵进行取反来撤消翻转。
            // 让我们取反它的第二行。这第二次将图集中的所有内容颠倒过来，从而使所有内容恢复正常。因为该行的第一个成分始终为零，所以我们只需将其他三个成分取反即可。
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;
            shadowSettings.splitData = splitData;
            int tileIndex = index + i; //每个点光源的阴影需要6张tile
           
            Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
            
            SetOtherTileData(tileIndex, offset, tileScale, bias);
            otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);

            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0f, 0f);
        }
    }
    
    /// <summary>
    /// 渲染聚光源的阴影
    /// </summary>
    void RenderSpotShadows(int index, int split, int tileSize)
    {
        ShadowedOtherLight light = shadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex){useRenderingLayerMaskTest = true};
        cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
        shadowSettings.splitData = splitData;
        
        //在聚光灯光源位置使用透视投影渲染场景深度
        //聚光灯使用透视投影来渲染shadowmap
        //使用normal bias处理shadow acne
        float texelSize = 2f / (tileSize * projectionMatrix.m00);
        float filterSize = texelSize * ((float)settings.other.filter + 1f);
        float bias = light.normalBias * filterSize * 1.4142136f;
        Vector2 offset = SetTileViewport(index, split, tileSize);
        float tileScale = 1f / split;
        SetOtherTileData(index, offset, tileScale, bias);
        otherShadowMatrices[index] = ConvertToAtlasMatrix(projectionMatrix * viewMatrix, offset, tileScale);
        
        buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        context.DrawShadows(ref shadowSettings);
        buffer.SetGlobalDepthBias(0f, 0f);
    }

    /// <summary>
    /// 索引和偏差
    /// </summary>
    void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        float border = atlasSizes.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        otherShadowTiles[index] = data;
    }

    /// <summary>
    /// 分割渲染视口 让每个光源提供自己的图块来渲染 防止各光源重叠在一个图块上
    /// 指定在当前绑定的FrameBuffer上的某个矩形区域内渲染
    /// </summary>
    Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        if (SystemInfo.usesReversedZBuffer) //使用反向的 Z 缓冲区，则反向z的值
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
        // 由于裁剪空间的范围是-1到1 而纹理坐标和深度的范围是0到1 所以将xyz缩放偏移一般 再对xy应用视口偏移
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

    /// <summary>
    /// 设置级联阴影相关数据
    /// </summary>
    void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)settings.directional.filter + 1f); // 增加正态偏置以匹配过滤器大小
        // 需要在shader中检测模型是否在剔除球体内 通过比较模型到球体中心的距离平方与球的半径平方
        // 所以直接在cpu端计算并储存半径平方 省的在shader中去计算了
        cullingSphere.w -= filterSize; // 将球体的半径减小到滤波器尺寸来避免超出剔除范围
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSphere[index] = cullingSphere;
        cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f); //传递剔除球体
    }

    // 设置关键字
    void SetKerwords( string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
                buffer.EnableShaderKeyword(keywords[i]);
            else
            {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }
    
    ////////////////////////////////////////// 各光源类型阴影的剔除筛选 /////////////////////////////////////////////////////

    /// <summary>
    /// 定向光阴影的相关数据
    /// </summary>
    struct ShadowedDirectionalLight
    {
        public int visibleLightIndex; //可见定向光的索引
        public float slopeScaleBias;  //阴影斜率偏差
        public float nearPlaneOffset; //近平面偏移
    }
    
    /// <summary>
    /// 目前通过函数得到已存储的可见光阴影的索引
    /// </summary>
    ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];

    /// <summary>
    /// 在阴影图集中为光源的阴影贴图保留空间 并存储渲染它们所需的信息
    /// <example>可见阴影光源的索引 阴影强度 偏差 法线偏差 阴影图块索引 阴影遮罩</example>
    /// </summary>
    public Vector4 ReserveDirectionalShadow(Light light, int visibleLightIndex)
    {
        // 当前定向光阴影数量 < 最大定向光阴影数量
        // 且 场景内灯光的阴影模式设置不是无 且 灯光的阴影强度不为零
        if (ShaowedDirectionalLightCount < maxShadowedDirectionalLightCount 
            && light.shadows != LightShadows.None && light.shadowStrength >0f)
        {
            float maskChannel = -1; // 当光源不使用阴影遮罩时 我们通过将其索引设置为 −1 来指示这一点
            
            // LightBakingOutput 描述给定光源的全局光照烘焙结果的结构体
            LightBakingOutput lightBaking = light.bakingOutput; // 最后一个全局光照烘焙的输出
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
            {
                useShadowMask = true; // 当光源的类型是Mixed 且 光照贴图的混合光照模式为Shadowmask时 开启shadow mask
                maskChannel = lightBaking.occlusionMaskChannel; // 对于Mixed光源包含要使用的遮挡遮罩通道的索引（如果有） 否则为-1
            }
            
            // 检查是否没有实时阴影投射器 在这种情况下 只有阴影强度是相关的
            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel); // 当阴影强度大于零时 着色器将对阴影贴图进行采样 通过取反阴影强度来使这项工作
            }
            
            ShadowedDirectionalLights[ShaowedDirectionalLightCount] = new ShadowedDirectionalLight
                { visibleLightIndex = visibleLightIndex, slopeScaleBias = light.shadowBias, nearPlaneOffset = light.shadowNearPlane}; // 存储光源的可见索引并增加计数
            
            return new Vector4(light.shadowStrength, settings.directional.cascadeCount * ShaowedDirectionalLightCount++, light.shadowNormalBias, maskChannel);
        }

        return new Vector4(0f, 0f, 0f, -1f);
    }

    /// <summary>
    /// 其他光阴影的相关数据
    /// </summary>
    struct ShadowedOtherLight
    {
        public int visibleLightIndex;  //可见其他光的索引
        public float slopeScaleBias;   //阴影斜率偏差
        public float normalBias;       //近平面偏移
        public bool isPoint;           //是否有点光源
    }

    /// <summary>
    /// 目前通过函数得到已存储的可见光阴影的索引
    /// </summary>
    ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];
    
    /// <summary>
    /// 在阴影图集中为其他光源的阴影贴图保留空间 并存储渲染它们所需的信息
    /// <example>可见阴影光源的索引 阴影强度 偏差 法线偏差 阴影图块索引 阴影遮罩</example>
    /// </summary>
    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f,0f, -1f);
        }
        
        float maskChannel = -1f;
        LightBakingOutput lightBaking = light.bakingOutput;
        
        if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            useShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }
        
        bool isPoint = light.type == LightType.Point;
        int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
        
        // 检查增加的光源计数是否会超过最大值 或者是否没有要渲染此光源的阴影
        // 如果是这样 则返回负阴影强度和遮罩通道
        // 否则 继续递增光源计数并设置tile的索引
        if (newLightCount >= maxShadowedOtherLightCount || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }
        
        shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight { visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias, normalBias = light.shadowNormalBias, isPoint = isPoint};

        Vector4 data = new Vector4(light.shadowStrength, shadowedOtherLightCount, isPoint ? 1f : 0f, maskChannel);
        shadowedOtherLightCount = newLightCount;
        return data;
    }
}
