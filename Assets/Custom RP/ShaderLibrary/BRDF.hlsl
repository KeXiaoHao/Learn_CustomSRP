#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

//定义非金属的反射率 约为 0.04
#define MIN_REFLECTIVITY 0.04

// B R D F相关数据结构体
struct BRDF
{
    float3 diffuse;     //漫反射颜色
    float3 specular;    //高光颜色 镜面反射颜色
    float roughness;    //粗糙度
    float perceptualRoughness;   //感知粗糙度 用来计算mipmap
    float fresnel;      // 菲尼尔参数
};

//计算Diffuse反射率
float OneMinusReflectivity (float metallic)
{
    float range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
} 

BRDF GetBRDF (Surface surface, bool applyAlphaToDiffuse = false)
{
    BRDF brdf;
    //Reflectivity表示Specular反射率，oneMinusReflectivity表示Diffuse反射率
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    brdf.diffuse = surface.color * oneMinusReflectivity;

    if (applyAlphaToDiffuse)
        brdf.diffuse *= surface.alpha; //Alpha预乘判断

    // 由于遵循能量守恒 镜面反射颜色应等于表面颜色减去漫反射颜色
    // 考虑金属影响镜面反射的颜色而非金属不影响 非金属的镜面发射颜色应该是白色
    // 通过使用金属度在最小反射率和表面颜色之间进行插值
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);

    // 粗糙度与平滑度相反，因此可以 1.0 - 平滑度 使用内置函数得出感知粗糙度
    // 再通过函数转换为实际粗糙度 也就是对其平方
    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);
    brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
    return brdf;
}

#endif