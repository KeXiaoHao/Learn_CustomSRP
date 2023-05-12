using UnityEngine;
using UnityEditor;

partial class PostFXStack
{
    partial void ApplySceneViewState();

    
#if UNITY_EDITOR
    /// <summary>
    /// 切换场景视图的后处理启用或禁用
    /// </summary>
    partial void ApplySceneViewState()
    {
        if (camera.cameraType == CameraType.SceneView && !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
        {
            settings = null;
        }
    }
#endif
}
