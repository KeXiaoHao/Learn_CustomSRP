using System;
using UnityEngine;

[DisallowMultipleComponent] //禁止挂多个该组件
public class PerObjectMaterialProperties : MonoBehaviour
{
    private static int baseColorId = Shader.PropertyToID("_BaseColor"),
        cutoffId = Shader.PropertyToID("_Cutoff"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness"); // 获取shader属性并转为int(消耗低)

    [SerializeField]private Color baseColor = Color.white;
    [SerializeField, Range(0f, 1f)]private float cutoff = 0.5f, metallic = 0f, smoothness = 0.5f;

    private static MaterialPropertyBlock block; // 要应用的材质值代码块

    private void OnValidate()
    {
        if (block == null)
            block = new MaterialPropertyBlock();
        
        block.SetColor(baseColorId, baseColor);
        block.SetFloat(cutoffId, cutoff);
        block.SetFloat(metallicId, metallic);
        block.SetFloat(smoothnessId, smoothness);
        GetComponent<Renderer>().SetPropertyBlock(block);
    }

    private void Awake()
    {
        OnValidate(); //在Runtime时也执行
    }
}
