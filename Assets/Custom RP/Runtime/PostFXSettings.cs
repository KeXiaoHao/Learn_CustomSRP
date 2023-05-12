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
    
    [System.Serializable]
    public struct BloomSettings //Bloom参数配置
    {
        [Range(0f, 16f)]public int maxInterations; //最大迭代次数
        
        [Min(1f)]public int downscaleLimit; //最低的降采样像素
        
        public bool bicubicUpsampling;      //是否需要三线性过滤
        
        [Min(0f)] public float threshold;   //阈值

        [Range(0f, 1f)] public float thresholdKnee; //阈值滑块
        
        [Min(0f)] public float intensity; //强度
    }

    [SerializeField]private BloomSettings bloom = default;

    public BloomSettings Bloom => bloom;
}
