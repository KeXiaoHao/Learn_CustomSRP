using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 阴影相关设置的参数序列化
/// </summary>
[System.Serializable]
public class ShadowSettings
{
    [Min(0.001f)]public float maxDistance = 100f;  //最远阴影显示的距离

    [Range(0.001f, 1f)] public float distanceFade = 0.1f;
    
    public enum TextureSize // 阴影贴图尺寸
    {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }

    [System.Serializable]
    public struct Directional //声明结构体 是为了将来会支持其他光源类型 这些光源类型将获得自己的阴影设置
    {
        public TextureSize atlasSize; //阴影贴图大小

        [Range(1, 4)]
        public int cascadeCount; //级联阴影层数
        
        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3; //级联阴影比例

        public Vector3 CascadeRatios => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3); //属性方法get

        [Range(0.001f, 1f)]public float cascadeFade;
    }

    public Directional directional = new Directional { atlasSize = TextureSize._1024, cascadeCount = 4, cascadeRatio1 = 0.1f, cascadeRatio2 = 0.25f, cascadeRatio3 = 0.5f, cascadeFade = 0.1f};
    
}
