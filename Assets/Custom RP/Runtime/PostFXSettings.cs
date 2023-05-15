using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    private Shader shader = default;

    [System.NonSerialized]
    private Material material;

    public Material Material
    {
        get
        {
            if (material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.HideAndDontSave; // 将其设置为隐藏而且不保存在项目中
            }
            return material;
        }
    }
    
    //////////////////////////////////////////// Bloom /////////////////////////////////////////////////////////////
    [System.Serializable]
    public struct BloomSettings //Bloom参数配置
    {
        [Range(0f, 16f)]public int maxInterations; //最大迭代次数
        
        [Min(1f)]public int downscaleLimit; //最低的降采样像素
        
        public bool bicubicUpsampling;      //是否需要三线性过滤
        
        [Min(0f)] public float threshold;   //阈值

        [Range(0f, 1f)] public float thresholdKnee; //阈值滑块
        
        [Min(0f)] public float intensity; //强度

        public bool fadeFireflies; //是否淡化闪烁
        
        public enum Mode { Additive, Scattering }

        public Mode mode; //散射模式

        [Range(0.05f, 0.95f)]
        public float scatter; //散射量
    }

    [SerializeField]private BloomSettings bloom = new BloomSettings{scatter = 0.7f};

    public BloomSettings Bloom => bloom;
    
    //////////////////////////////////////////// ToneMapping /////////////////////////////////////////////////////////////
    
    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None = -1,
            ACES,
            Neutral,
            Reinhard
        }

        public Mode mode;
    }

    [SerializeField]
    ToneMappingSettings toneMapping = default;

    public ToneMappingSettings ToneMapping => toneMapping;
}
