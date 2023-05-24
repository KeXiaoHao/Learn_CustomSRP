using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(CustomRenderPipelineAsset))]
public class CustomLightEditor : LightEditor
{
    static GUIContent renderingLayerMaskLabel =
        new GUIContent("Rendering Layer Mask", "Functional version of above property.");
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        RenderingLayerMaskDrawer.Draw(settings.renderingLayerMask, renderingLayerMaskLabel);
        
        // 当没有多种不同的光源类型 且 灯光类型是spot时
        if (!settings.lightType.hasMultipleDifferentValues && (LightType)settings.lightType.enumValueIndex == LightType.Spot)
        {
            settings.DrawInnerAndOuterSpotAngle(); //添加内-外角滑块
        }
        
        settings.ApplyModifiedProperties();    //应用属性更改
        
        var light = target as Light;
        if (light.cullingMask != -1)
        {
            // 指示剔除遮罩仅影响阴影并显示警告图标
            EditorGUILayout.HelpBox(light.type == LightType.Directional ?
                    "Culling Mask only affects shadows." : "Culling Mask only affects shadow unless Use Lights Per Objects is on.",
                MessageType.Warning);
        }
    }

    // void DrawRenderingLayerMask()
    // {
    //     SerializedProperty property = settings.renderingLayerMask;
    //     EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
    //     EditorGUI.BeginChangeCheck();
    //     int mask = property.intValue;
    //     if (mask == int.MaxValue)
    //         mask = -1;
    //     mask = EditorGUILayout.MaskField(renderingLayerMaskLabel, mask, GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames);
    //     if (EditorGUI.EndChangeCheck())
    //     {
    //         property.intValue = mask == -1 ? int.MaxValue : mask;
    //     }
    //     
    //     EditorGUI.showMixedValue = false;
    // }
}
