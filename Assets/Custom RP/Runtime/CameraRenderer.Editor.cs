using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

public partial class CameraRenderer
{
    partial void DrawUnsupportedShaders(); // 分部方法
    partial void DrawGizmos();
    partial void PrepareForSceneWindow();
    partial void PrepareBuffer();

#if UNITY_EDITOR
    private static ShaderTagId[] legacyShaderTagIds =
    {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM"),
    };

    private static Material errorMaterial; //错误的材质 即粉红材质

    private string SampleName { get; set; }

    partial void DrawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawUnsupportedShaders()
    {
        if (errorMaterial == null)
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader")); //获取错误材质

        var drawingSettings = new DrawingSettings(legacyShaderTagIds[0], new SortingSettings(camera))
        {
            overrideMaterial = errorMaterial // 覆写材质
        }; //绘制设置 包括shader标签和绘制顺序
        for (int i = 1; i < legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i,
                legacyShaderTagIds[i]); //通过for循环和SetShaderPassName 把不支持的shader pass全设置上
        }

        var filteringSettings = FilteringSettings.defaultValue; //本来就是错误的shader 所以不用关心具体的过滤设置 给个默认值
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    partial void PrepareForSceneWindow()
    {
        if (camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera); //将 UI 几何形状发送到 Scene 视图以进行渲染
    }

    partial void PrepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        buffer.name = SampleName = camera.name;
        Profiler.EndSample();
    }

#else
    const string SampleName = bufferName;
#endif
}