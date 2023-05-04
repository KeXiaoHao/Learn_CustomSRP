using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.Rendering;

public class MeshBall : MonoBehaviour
{
    private static int baseColorId = Shader.PropertyToID("_BaseColor"),
        metallicId = Shader.PropertyToID("_Metallic"),
        smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField] private Mesh mesh = default;
    [SerializeField]private Material material = default;

    // 创建每实例数据
    private Matrix4x4[] matrices = new Matrix4x4[1023];
    private Vector4[] baseColors = new Vector4[1023];

    private float[] metallic = new float[1023],
        smoothness = new float[1023];

    private MaterialPropertyBlock block;
    
    [SerializeField]
    LightProbeProxyVolume lightProbeVolume = null;

    private void Awake()
    {
        for (int i = 0; i < matrices.Length; i++)
        {
            // 在半径10米的球形空间内随机实例化小球的位置
            matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10f, Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f), Vector3.one * Random.Range(0.5f, 1.5f));
            baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
        }
    }

    private void Update()
    {
        if (block == null)
        {
            block = new MaterialPropertyBlock(); // 由于没有创建game object 需要每帧绘制 
            block.SetVectorArray(baseColorId, baseColors); // 设置向量属性数组
            block.SetFloatArray(metallicId, metallic);
            block.SetFloatArray(smoothnessId, smoothness);

            if (!lightProbeVolume)
            {
                var positions = new Vector3[1023];
                for (int i = 0; i < matrices.Length; i++)
                {
                    positions[i] = matrices[i].GetColumn(3);
                }

                var lightProbes = new SphericalHarmonicsL2[1023];
                // 计算给定世界空间位置处的光照探针和遮挡探针
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, lightProbes, null);
            
                block.CopySHCoefficientArraysFrom(lightProbes);
            }
        }
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block,
            ShadowCastingMode.On, true, 0, null, 
            lightProbeVolume ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided, lightProbeVolume);
    }
}
