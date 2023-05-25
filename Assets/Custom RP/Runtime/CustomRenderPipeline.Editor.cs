using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;

public partial class CustomRenderPipeline
{
    partial void InitializeForEditor();
    partial void DisposeForEditor();


#if UNITY_EDITOR

    partial void InitializeForEditor()
    {
        Lightmapping.SetDelegate(lightsDelegate);
    }

    partial void DisposeForEditor()
    {
        //base.Dispose(disposing);
        Lightmapping.ResetDelegate();
    }

    // 在管线被释放时清理和重置委托
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeForEditor();
        renderer.Dispose();
    }

    // RequestLightsDelegate 在将光源转换为烘焙后端可理解的形式时调用的委托
    // requests 要转换的光源列表
    // lightsOutput 委托函数生成的输出 必须将应跳过的光源添加到输出 并使用LightDataGI结构中的 InitNoBake 对其进行初始化
    private static Lightmapping.RequestLightsDelegate lightsDelegate = (Light[] lights, NativeArray < LightDataGI > output) => {
        var lightData = new LightDataGI();
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            switch (light.type)
            {
                case LightType.Directional:
                    var directionalLight = new DirectionalLight();
                    LightmapperUtils.Extract(light, ref directionalLight);
                    lightData.Init(ref directionalLight);
                    break;
                case LightType.Point:
                    var pointLight = new PointLight();
                    LightmapperUtils.Extract(light, ref pointLight);
                    lightData.Init(ref pointLight);
                    break;
                case LightType.Spot:
                    var spotLight = new SpotLight();
                    LightmapperUtils.Extract(light, ref spotLight);
                    spotLight.innerConeAngle = light.innerSpotAngle * Mathf.Deg2Rad;
                    spotLight.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                    lightData.Init(ref spotLight);
                    break;
                case LightType.Area:
                    var rectangleLight = new RectangleLight();
                    LightmapperUtils.Extract(light, ref rectangleLight);
                    rectangleLight.mode = LightMode.Baked; //不支持实时区域光源 因此强制其光源模式为烘焙
                    lightData.Init(ref rectangleLight);
                    break;
                default:
                    lightData.InitNoBake(light.GetInstanceID()); //初始化光源以使烘焙后端忽略它
                    break;
            }
            lightData.falloff = FalloffType.InverseSquared; //用于烘焙点光源和聚光灯的衰减模型设置成平方反比距离衰减
            output[i] = lightData;
        }
    };
#endif
}
