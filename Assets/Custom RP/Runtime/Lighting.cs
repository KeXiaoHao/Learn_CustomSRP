using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 用于把场景中的光源信息通过CPU传递给GPU
/// 其上级为 CameraRenderer类
/// </summary>
public class Lighting
{
    private const string bufferName = "Lighting";

    private CommandBuffer buffer = new CommandBuffer() { name = bufferName };

    private const int maxDirLightCount = 4, maxOtherLightCount = 64; //定义最大数量的定向光和其他光源

    // 获取CBUFFER中对应数据名称的id 相当于shader的全局变量
    private static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

    private static Vector4[] dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];
    
    // 同上操作 针对于其他光源
    private static int otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
        otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
        otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions"),
        otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections"),
        otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles"),
        otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");

    private static Vector4[] otherLightColors = new Vector4[maxOtherLightCount],
        otherLightPositions = new Vector4[maxOtherLightCount],
        otherLightDirections = new Vector4[maxOtherLightCount], //聚光灯朝向
        otherLightSpotAngles = new Vector4[maxOtherLightCount], //光斑角度
        otherLightShadowData = new Vector4[maxOtherLightCount]; //阴影数据
    

    private CullingResults cullingResults; //声明一个存储剔除结果的结构体
    
    private Shadows shadows = new Shadows();    //声明Shadows来调用
    
    static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

    /// <summary>
    /// 场景内可见的有效的光源相关的设置
    /// <example>传递数据 阴影渲染</example>
    /// </summary>
    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings, bool useLightsPerObject)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName); //用来debug
        shadows.Setup(context, cullingResults, shadowSettings); //阴影的剔除筛选
        SetupLights(useLightsPerObject); //将各类灯光数据传递给shader
        shadows.Render(); //阴影的渲染
        buffer.EndSample(bufferName);
        
        //再次提醒这里只是提交CommandBuffer到Context的指令队列中
        //只有等到context.Submit()才会真正依次执行指令
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    /// <summary>
    /// 灯光相关操作的清理
    /// </summary>
    public void Cleanup()
    {
        shadows.Cleanup();
    }

    ////////////////////////////////////////// 各光源类型相关数据设置 /////////////////////////////////////////////////////
    
    /// <summary>
    /// 遍历场景内可见光源 并把其相关数据传递给shader
    /// </summary>
    void SetupLights(bool useLightsPerObject)
    {
        NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default; //提供了一个包含光索引的临时值 与可见光索引以及场景中的所有其他活动光源相匹配
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights; //用NativeArray存储可见光源(有效光源)
        // 遍历所有可见光并把灯光数据传递给shader
        int dirLightCount = 0, otherLightCount = 0;
        int i;
        for (i = 0; i < visibleLights.Length; i++)
        {
            int newIndex = -1;
            VisibleLight visibleLight = visibleLights[i];
            switch (visibleLight.lightType)
            {
                case LightType.Directional:
                    if (dirLightCount < maxDirLightCount)
                    {
                        SetupDirectionalLight(dirLightCount++, i, ref visibleLight);
                    }
                    break;
                case LightType.Point:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupPointLight(otherLightCount++, i, ref visibleLight);
                    }
                    break;
                case LightType.Spot:
                    if (otherLightCount < maxOtherLightCount)
                    {
                        newIndex = otherLightCount;
                        SetupSpotLight(otherLightCount++, i, ref visibleLight);
                    }
                    break;
            }

            if (useLightsPerObject)
            {
                indexMap[i] = newIndex; // // 仅当useLightsPerObject启用时 才设置新索引
            }
        }
        // 必须消除所有不可见的灯光的索引 如果我们使用每个对象的光源 则在第一个循环之后继续第二个循环来执行此操作
        if (useLightsPerObject)
        {
            for (; i < indexMap.Length; i++)
            {
                indexMap[i] = -1;
            }
            cullingResults.SetLightIndexMap(indexMap); //通过调用剔除结果将调整后的索引映射发送回 Unity
            indexMap.Dispose(); //释放
            Shader.EnableKeyword(lightsPerObjectKeyword);
        }
        else
        {
            Shader.DisableKeyword(lightsPerObjectKeyword);
        }
        
        buffer.SetGlobalInt(dirLightCountId, dirLightCount);       // 传递有效定向光源的数量
        if (dirLightCount > 0)
        {
            buffer.SetGlobalVectorArray(dirLightColorId, dirLightColors);           // 传递光源的颜色
            buffer.SetGlobalVectorArray(dirLightDirectionId, dirLightDirections);   // 传递光源的方向
            buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);  // 传递光源阴影的强度和索引
        }
        
        buffer.SetGlobalInt(otherLightCountId, otherLightCount);          // 传递有效其他光源的数量
        if (otherLightCount > 0)
        {
            buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);      // 传递光源的颜色
            buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);// 传递光源的位置
            buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections); //传递聚光灯的朝向
            buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles); //传递Spot Angel
            buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData); //传递阴影数据
        }
    }

    /// <summary>
    /// 定向光相关数据的设置
    /// <example>颜色 方向 阴影</example>
    /// </summary>
    void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight) //消耗大 使用ref引用传递函数
    {
        dirLightColors[index] = visibleLight.finalColor; //光源颜色乘以强度 但默认不会将其转为线性空间 得手动设置
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2); //光源变换矩阵
        dirLightShadowData[index] = shadows.ReserveDirectionalShadow(visibleLight.light, visibleIndex); //阴影的剔除相关 外加阴影强度和索引
    }

    /// <summary>
    /// 点光源相关数据的设置
    /// <example>颜色 位置 阴影</example>
    /// </summary>
    void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;                          //灯光颜色获取
        
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f); //将光照范围储存在w中
        otherLightPositions[index] = position;                    // 位置获取
        otherLightSpotAngles[index] = new Vector4(0f, 1f); //确保点光源不受角度衰减影响
        Light light = visibleLight.light;
        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex); //阴影数据获取
    }

    /// <summary>
    /// 聚光灯相关数据的设置
    /// <example>颜色 位置 朝向 阴影</example>
    /// </summary>
    void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight)
    {
        otherLightColors[index] = visibleLight.finalColor;                        //灯光颜色获取
        Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
        position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
        otherLightPositions[index] = position;                                              // 位置获取
        otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2); //聚光灯朝向获取
        
        // 角度计算
        Light light = visibleLight.light;
        float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
        float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
        float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
        otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv); //聚光灯角度获取

        otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex); //阴影数据获取
    }
}
