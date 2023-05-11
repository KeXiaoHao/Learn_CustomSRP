using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        // 当没有多种不同的光源类型 且 灯光类型是spot时
        if (!settings.lightType.hasMultipleDifferentValues && (LightType)settings.lightType.enumValueIndex == LightType.Spot)
        {
            settings.DrawInnerAndOuterSpotAngle(); //添加内-外角滑块
            settings.ApplyModifiedProperties();    //应用属性更改
        }
    }
}
