#ifndef FRAGMENT_INCLUDED
#define FRAGMENT_INCLUDED

TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

struct Fragment
{
    float2 positionSS;  // 屏幕空间坐标
    float2 screenUV;    // 屏幕UV
    float depth;        // 深度
    float bufferDepth;  // 缓冲区深度
};

Fragment GetFragment(float4 positionSS)
{
    Fragment f;
    f.positionSS = positionSS.xy;
    f.screenUV = f.positionSS / _ScreenParams.xy;
    // 片段深度存储在屏幕空间位置的w中 是用于执行透视划分以将 3D 位置投影到屏幕上的值
    // 这是视空间深度 因此它是与相机XY平面的距离 而不是其近平面
    f.depth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(positionSS.z) : positionSS.w;
    f.bufferDepth = LOAD_TEXTURE2D(_CameraDepthTexture, f.positionSS).r;
    // f.bufferDepth = SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, sampler_point_clamp, f.screenUV, 0); // 等价于
    f.bufferDepth = IsOrthographicCamera() ? OrthographicDepthBufferToLinear(f.bufferDepth) : LinearEyeDepth(f.bufferDepth, _ZBufferParams);
    return f;
}

float4 GetBufferColor(Fragment fragment, float2 uvOffset = float2(0.0, 0.0))
{
    float2 uv = fragment.screenUV + uvOffset;
    return SAMPLE_TEXTURE2D_LOD(_CameraColorTexture, sampler_linear_clamp, uv, 0);
}

#endif
