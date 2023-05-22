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
    
    //////////////////////////////////////////// 辉光 Bloom /////////////////////////////////////////////////////////////
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
    
    //////////////////////////////////////////// 颜色分级 Color Adjustments /////////////////////////////////////////////////////////////

    [Serializable]
    public struct ColorAdjustmentsSettings
    {
        public float postExposure;                  // 曝光度

        [Range(-100f, 100f)]
        public float contrast;                      // 对比度

        [ColorUsage(false, true)]
        public Color colorFilter;                   // 颜色滤镜

        [Range(-180f, 180f)]
        public float hueShift;                      // 色相偏移

        [Range(-100f, 100f)]
        public float saturation;                    // 饱和度
    }

    [SerializeField]
    ColorAdjustmentsSettings colorAdjustments = new ColorAdjustmentsSettings{colorFilter = Color.white};

    public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;
    
    //////////////////////////////////////////// 白平衡 White Balance /////////////////////////////////////////////////////////////
    
    [Serializable]
    public struct WhiteBalanceSettings
    {
        [Range(-100f, 100f)] public float temperature, tint;    // 色温和色调
    }

    [SerializeField]
    WhiteBalanceSettings whiteBalance = default;

    public WhiteBalanceSettings WhiteBalance => whiteBalance;
    
    //////////////////////////////////////////// 色调分离 Split Toning /////////////////////////////////////////////////////////////
    
    [Serializable]
    public struct SplitToningSettings
    {
        [ColorUsage(false)] public Color shadows, highlights;   // 阴影和高光

        [Range(-100f, 100f)] public float balance;                         // 平衡
    }

    [SerializeField] SplitToningSettings splitToning = new SplitToningSettings
    {
        shadows = Color.gray,
        highlights = Color.gray
    };

    public SplitToningSettings SplitToning => splitToning;
    
    //////////////////////////////////////////// 通道混合 Channel Mixer /////////////////////////////////////////////////////////////
    
    [Serializable]
    public struct ChannelMixerSettings {

        public Vector3 red, green, blue;
    }
	
    [SerializeField] ChannelMixerSettings channelMixer = new ChannelMixerSettings
    {
        red = Vector3.right,
        green = Vector3.up,
        blue = Vector3.forward
    };

    public ChannelMixerSettings ChannelMixer => channelMixer;
    
    //////////////////////////////////////////// 阴影中间调高光 Shadows Midtones Highlights /////////////////////////////////////////////////////////////
    
    [Serializable]
    public struct ShadowsMidtonesHighlightsSettings {

        [ColorUsage(false, true)]
        public Color shadows, midtones, highlights;  // 阴影色调 中间色调 高光色调

        [Range(0f, 2f)]
        public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd; // 阴影过渡区域和高光过渡区域的开始结束
    }

    [SerializeField] ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights = new ShadowsMidtonesHighlightsSettings
        {
            shadows = Color.white,
            midtones = Color.white,
            highlights = Color.white,
            shadowsEnd = 0.3f,
            highlightsStart = 0.55f,
            highLightsEnd = 1f
        };

    public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights => shadowsMidtonesHighlights;
    
    //////////////////////////////////////////// 色调映射 ToneMapping /////////////////////////////////////////////////////////////
    
    [System.Serializable]
    public struct ToneMappingSettings
    {
        public enum Mode
        {
            None,
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
