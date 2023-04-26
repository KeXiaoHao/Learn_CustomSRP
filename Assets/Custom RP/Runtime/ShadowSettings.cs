using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 阴影相关设置的参数序列化
/// </summary>
[System.Serializable]
public class ShadowSettings
{
    [Min(0f)]public float maxDistance = 100f;  //最远阴影显示的距离
    
    public enum TextureSize // 阴影贴图尺寸
    {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }

    [System.Serializable]
    public struct Directional //声明结构体 是为了将来会支持其他光源类型 这些光源类型将获得自己的阴影设置
    {
        public TextureSize atlasSize;
    }

    public Directional directional = new Directional { atlasSize = TextureSize._1024 };
}
