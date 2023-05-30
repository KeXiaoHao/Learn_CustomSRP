using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CameraBufferSettings
{
    public bool allowHDR;
    
    public bool copyColor, copyColorReflection, copyDepth, copyDepthReflections;

    [Range(0.1f, 2f)]
    public float renderScale; // 渲染比例
    
    public enum BicubicRescalingMode { Off, UpOnly, UpAndDown }

    public BicubicRescalingMode bicubicRescaling; //是否开启双三次采样
}
