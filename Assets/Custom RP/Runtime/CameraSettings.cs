using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class CameraSettings
{
    public bool copyColor = true, copyDepth = true;
    
    [RenderingLayerMaskField] //创建下拉菜单
    public int renderingLayerMask = -1;
    
    public bool maskLights = false;
    
    public enum RenderScaleMode { Inherit, Multiply, Override } //渲染缩放模式 继承 相乘 覆盖

    public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

    [Range(0.1f, 2f)]
    public float renderScale = 1f;
    
    public bool overridePostFX = false;

    public PostFXSettings postFXSettings = default;
    
    public bool allowFXAA = false;
    
    // 保持 Alpha 的原因是当多个摄像机堆叠透明度时
    public bool keepAlpha = false; //默认计算亮度用a通道 勾选用g通道
    
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source, destination;
    }

    public FinalBlendMode finalBlendMode = new FinalBlendMode
    {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };
    
    //////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// 根据渲染模式的选择来确定最终的渲染比例
    /// </summary>
    /// <param name="scale">渲染比例</param>
    /// <returns></returns>
    public float GetRenderScale(float scale)
    {
        return
            renderScaleMode == RenderScaleMode.Inherit ? scale : renderScaleMode == RenderScaleMode.Override ? renderScale : scale * renderScale;
    }
}
