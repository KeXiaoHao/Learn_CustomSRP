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

    private const int maxDirLightCount = 4; //定义最大数量的定向光

    // 获取CBUFFER中对应数据名称的id 相当于shader的全局变量
    private static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorId = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirections");

    private static Vector4[] dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount];

    private CullingResults cullingResults; //声明一个存储剔除结果的结构体
    
    private Shadows shadows = new Shadows();    //声明Shadows来调用

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        this.cullingResults = cullingResults;
        buffer.BeginSample(bufferName); //用来debug
        shadows.Setup(context, cullingResults, shadowSettings); //阴影相关
        SetupLights(); //将平行光数据传递给shader
        buffer.EndSample(bufferName);
        
        //再次提醒这里只是提交CommandBuffer到Context的指令队列中
        //只有等到context.Submit()才会真正依次执行指令
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void SetupLights()
    {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights; //用NativeArray存储可见光源(有效光源)
        // 遍历所有可见光并把灯光数据传递给shader
        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional) //当可见光类型是Directional执行定向光数据的传递
            {
                SetupDirectionalLight(dirLightCount++, ref visibleLight);
                if (dirLightCount >= maxDirLightCount)
                    break;
            }
        }
        buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
        buffer.SetGlobalVectorArray(dirLightColorId, dirLightColors);
        buffer.SetGlobalVectorArray(dirLightDirectionId, dirLightDirections);
    }

    void SetupDirectionalLight(int index, ref VisibleLight visibleLight) //消耗大 使用ref引用传递函数
    {
        dirLightColors[index] = visibleLight.finalColor; //光源颜色乘以强度 但默认不会将其转为线性空间 得手动设置
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2); //光源变换矩阵
        shadows.ReserveDirectionalShadow(visibleLight.light, index); //
    }
}
