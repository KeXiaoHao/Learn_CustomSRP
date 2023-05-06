#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

/////////////////////// B R D F Functions /////////////////////////////////////

float Square (float v) {return v * v; } // 平方操作 方便多次使用

// 计算高光强度 镜面发射强度
float SpecularStrength (Surface surface, BRDF brdf, Light light)
{
    //相关计算地址:https://catlikecoding.com/unity/tutorials/custom-srp/directional-lights/
    // SpecularTerm = r^2 / ( d^2 * max(0.1, dot(L, H)^2) * n)
    // r = roughness
    // d = dot(N, H)^2 * (r^2 - 1) + 1.0001
    // n = 4r + 2
    float3 h = SafeNormalize(light.direction + surface.viewDirection); //半角向量
    float nh2 = Square(saturate(dot(surface.normal, h))); // blinn phong
    float lh2 = Square(saturate(dot(light.direction, h)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2) * normalization);
}

// 计算光照反射出的总能量比例 漫反射+镜面反射
float3 DirectBRDF (Surface surface, BRDF brdf, Light light)
{
    // 观察角度接收到的高光能量比例 * 物体镜面反射的高光能量 + 各向均匀的漫反射能量
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

// 计算间接光照 即GI中的漫反射和镜面反射与B R D F的计算
float3 IndirectBRDF (Surface surface, BRDF brdf, float3 diffuse, float3 specular)
{
    float fresnelStrength = Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection))) * surface.fresnelStrength;
    
    float3 reflection = specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength); // 根据fresnel强度在B RDF镜面和菲涅耳颜色之间进行插值
    // 粗糙会散射这种反射 因此应该减少我们最终看到的镜面反射
    reflection /= brdf.roughness * brdf.roughness + 1.0; // 低粗糙度值并不重要 而最大粗糙度会使反射减半
    
    return diffuse * brdf.diffuse + reflection;
}

/////////////////////// Lighting Functions /////////////////////////////////////

// 计算物体表面接收到的光能量总和
float3 IncomingLight (Surface surface, Light light)
{
    return saturate(dot(surface.normal, light.direction)) * light.color * light.attenuation;
}

// 返回光照计算结果 即物体表面最终反射的RGB光能量
float3 GetLighting (Surface surface, BRDF brdf, Light light)
{
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

// 返回光照计算结果
float3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi)
{
    ShadowData shadowData = GetShadowData(surfaceWS);
    shadowData.shadowMask = gi.shadowMask;
    float3 color = IndirectBRDF(surfaceWS, brdf, gi.diffuse, gi.specular); //间接光照
    for (int i = 0; i < GetDirectionalLightCount(); i++)
    {
        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        color += GetLighting(surfaceWS, brdf, light);
    }
    return color;
}

#endif