using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    private MaterialEditor editor; //材质编辑器 负责显示和编辑材质的基础编辑器对象
    private Object[] materials;            //对正在编辑的材质的引用
    private MaterialProperty[] properties; //可以编辑的属性数组

    private bool showPresets; //存储当前折叠标签的状态
    
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck(); //开始检查材质是否有修改
        base.OnGUI(materialEditor, properties);
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;

        BakedEmission();
        
        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true); //使用折叠
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SetShadowCasterPass(); //有修改就调用此方法
            CopyLightMappingProperties();
        }
    }

    ///////////////////////////// 设置属性和关键字 //////////////////////////////////
    /// <summary>
    /// 设置Float类型的属性
    /// </summary>
    bool SetProperty(string name, float value)
    {
        MaterialProperty property = FindProperty(name, properties, false);
        if (property != null)
        {
            property.floatValue = value;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 设置关键字
    /// </summary>
    void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }
    /// <summary>
    /// 设置关键字类型的属性
    /// </summary>
    void SetProperty(string name, string keyword, bool value)
    {
        if (SetProperty(name, value ? 1f : 0f))
            SetKeyword(keyword, value);
    }

    private bool Clipping
    {
        set => SetProperty("_Clipping", "_CLIPPING", value);
    }
    bool PremultiplyAlpha {
        set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
    }

    BlendMode SrcBlend {
        set => SetProperty("_SrcBlend", (float)value);
    }

    BlendMode DstBlend {
        set => SetProperty("_DstBlend", (float)value);
    }

    bool ZWrite {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }

    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    ////////////////////////////////// 预设按钮 ////////////////////////////////////////
    /// <summary>
    /// 创建按钮并可撤回
    /// </summary>
    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            editor.RegisterPropertyChangeUndo(name); //更改材质属性时 将为操作添加撤销标签
            return true;
        }
        return false;
    }
    
    void OpaquePreset()
    {
        if (PresetButton("Qpaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
            Shadows = ShadowMode.On;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
            Shadows = ShadowMode.Clip;
        }
    }

    void FadePreset()
    {
        if (PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
            Shadows = ShadowMode.Off;
        }
    }

    void TransparentPreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Transparent"))
        {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
            Shadows = ShadowMode.Off;
        }
    }
    
    ////////////////////////////// 可隐藏相关预设 ///////////////////////////

    // 检测返回的属性是否存在 存在就返回ture
    bool HasProperty(string name) => FindProperty(name, properties, false) != null;
    
    // 创建一个属性方便检查
    private bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");
    
    ////////////////////////////// 阴影相关处理 ///////////////////////////

    enum ShadowMode
    {
        On, Clip, Dither, Off
    }

    ShadowMode Shadows
    {
        set
        {
            if (SetProperty("_Shadows", (float)value))
            {
                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
            }
        }
    }

    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        // 如果属性不存在或者这个属性有多个不同的值 直接return
        if (shadows == null || shadows.hasMixedValue)
            return;
        // 否则启用或者禁用ShadowCater这个pss 当阴影关闭时 为false 自然就关闭这个pass
        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled("ShadowCater", enabled);
        }
    }
    
    ////////////////////////////// 烘焙相关处理 ///////////////////////////

    void BakedEmission()
    {
        EditorGUI.BeginChangeCheck();
        
        // 绘制用于光照贴图自发光属性的UI（无、实时、烘焙）
        editor.LightmapEmissionProperty();

        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in editor.targets)
            {
                // Unity在烘焙时会积极尝试避免单独的发射通道 如果材质的发射设置为零 则忽略它
                // 通过在更改自发光模式时禁用所有选定材质属性的默认标志来覆盖此行为
                m.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    /// <summary>
    /// 复制光照贴图属性
    /// </summary>
    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
        if (mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }

        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
        if (color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }
}
