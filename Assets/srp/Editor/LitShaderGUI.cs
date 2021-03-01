/*****************************************************************************
*                                                                            *
*  @file     LitShaderGUI.cs                                                 *
*  @brief    材质面板扩展，用于一键设置渲染类型的标准参数                    *
*  Details                                                                   *
*                                                                            *
*  @author   zhangfan                                                        *
*                                                                            *
*----------------------------------------------------------------------------*
*  Change History :                                                          *
*  <Date>     | <Version> | <Author>    | <Description>                      *
*----------------------------------------------------------------------------*
*  2019/11/25 | 1.0.0.1   | zhangfan    | Create PostProcessing              *
*----------------------------------------------------------------------------*
*                                                                            *
*****************************************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class LitShaderGUI : ShaderGUI
{
    MaterialEditor m_editor;
    Object[] m_materials;
    MaterialProperty[] m_properties;

    private bool m_showPresets;

    enum EClipMode
    {
        Off, On, Shadows
    }

    private EClipMode clipping
    {
        set
        {
            FindProperty("_Clipping", m_properties).floatValue = (float)value;
            setKeywordEnabled("_CLIPPING_OFF", value == EClipMode.Off);
            setKeywordEnabled("_CLIPPING_ON", value == EClipMode.On);
            setKeywordEnabled("_CLIPPING_SHADOWS", value == EClipMode.Shadows);
        }
    }

    private bool reciveShadows
    {
        set
        {
            FindProperty("_ReceiveShadows", m_properties).floatValue = value ? 1 : 0;
            setKeywordEnabled("_RECEIVE_SHADOWS", value);
        }
    }

    private RenderQueue renderQueue
    {
        set
        {
            foreach (Material m in m_materials)
            {
                m.renderQueue = (int)value;
            }
        }
    }

    private CullMode cull
    {
        set
        {
            FindProperty("_Cull", m_properties).floatValue = (float)value;
        }
    }

    private BlendMode srcBlend
    {
        set
        {
            FindProperty("_SrcBlend", m_properties).floatValue = (float)value;
        }
    }

    private BlendMode dstBlend
    {
        set
        {
            FindProperty("_DstBlend", m_properties).floatValue = (float)value;
        }
    }

    private bool zWrite
    {
        set
        {
            FindProperty("_ZWrite", m_properties).floatValue = value ? 1 : 0;
        }
    }

    private bool premultiplyAlpha
    {
        set
        {
            FindProperty("_PremulAlpha", m_properties).floatValue = value ? 1 : 0;
            setKeywordEnabled("_PREMULTIPLY_ALPHA", value);
        }
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);

        this.m_editor = materialEditor;
        this.m_materials = materialEditor.targets;
        this.m_properties = properties;

        castShadowsToggle();

        EditorGUILayout.Space();
        m_showPresets = EditorGUILayout.Foldout(m_showPresets, "Presets", true);
        if (m_showPresets)
        {
            opaquePreset();
            clipPreset();
            clipDoubleSidedPreset();
            fadePreset();
            fadeWithShadowsPreset();
            transparentPreset();
            transparentWithShadowsPreset();
        }
    }

    private void setPassEnabled(string pass, bool enabled)
    {
        foreach (Material m in m_materials)
        {
            m.SetShaderPassEnabled(pass, enabled);
        }
    }

    private bool? isPassEnabled(string pass)
    {
        bool enabled = ((Material)m_materials[0]).GetShaderPassEnabled(pass);
        for (int i = 1; i < m_materials.Length; i++)
        {
            if (enabled != ((Material)m_materials[i]).GetShaderPassEnabled(pass))
                return null;
        }
        return enabled;
    }

    private void castShadowsToggle()
    {
        bool? enabled = isPassEnabled("ShadowCaster");
        if (!enabled.HasValue)
        {
            EditorGUI.showMixedValue = true;
            enabled = false;
        }

        EditorGUI.BeginChangeCheck();
        enabled = EditorGUILayout.Toggle("Cast Shadows", enabled.Value);
        if (EditorGUI.EndChangeCheck())
        {
            m_editor.RegisterPropertyChangeUndo("Cast Shadows");
            setPassEnabled("ShadowCaster", enabled.Value);
        }
        EditorGUI.showMixedValue = false;
    }

    private void setKeywordEnabled(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in m_materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in m_materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    private void clipPreset()
    {
        if (!GUILayout.Button("Clip"))
            return;
        m_editor.RegisterPropertyChangeUndo("Clip Preset");
        clipping = EClipMode.On;
        cull = CullMode.Back;
        srcBlend = BlendMode.One;
        dstBlend = BlendMode.Zero;
        zWrite = true;
        reciveShadows = true;
        setPassEnabled("ShadowCaster", true);
        renderQueue = RenderQueue.AlphaTest;
    }

    private void clipDoubleSidedPreset()
    {
        if (!GUILayout.Button("Clip Double-Slided"))
            return;
        m_editor.RegisterPropertyChangeUndo("Clip Double-Sided Preset");
        clipping = EClipMode.On;
        cull = CullMode.Off;
        srcBlend = BlendMode.One;
        dstBlend = BlendMode.Zero;
        zWrite = true;
        reciveShadows = true;
        setPassEnabled("ShadowCaster", true);
        renderQueue = RenderQueue.AlphaTest;
    }

    private void fadePreset()
    {
        if (!GUILayout.Button("Fade"))
            return;
        m_editor.RegisterPropertyChangeUndo("Fade Preset");
        clipping = EClipMode.Off;
        cull = CullMode.Back;
        srcBlend = BlendMode.SrcAlpha;
        dstBlend = BlendMode.OneMinusSrcAlpha;
        zWrite = false;
        reciveShadows = false;
        setPassEnabled("ShadowCaster", false);
        renderQueue = RenderQueue.Transparent;
    }

    private void fadeWithShadowsPreset()
    {
        if (!GUILayout.Button("Fade With Shadows"))
            return;
        m_editor.RegisterPropertyChangeUndo("Fade With Shadows Preset");
        clipping = EClipMode.Shadows;
        cull = CullMode.Back;
        srcBlend = BlendMode.SrcAlpha;
        dstBlend = BlendMode.OneMinusSrcAlpha;
        zWrite = false;
        reciveShadows = true;
        setPassEnabled("ShadowCaster", true);
        renderQueue = RenderQueue.Transparent;
    }

    private void opaquePreset()
    {
        if (!GUILayout.Button("Opaque"))
            return;
        m_editor.RegisterPropertyChangeUndo("Opaque Preset");
        clipping = EClipMode.Off;
        cull = CullMode.Back;
        srcBlend = BlendMode.One;
        dstBlend = BlendMode.Zero;
        zWrite = true;
        reciveShadows = true;
        setPassEnabled("ShadowCaster", true);
        renderQueue = RenderQueue.Geometry;

    }

    private void transparentPreset()
    {
        if (!GUILayout.Button("Transparent"))
            return;
        m_editor.RegisterPropertyChangeUndo("Transparent Preset");
        clipping = EClipMode.Off;
        cull = CullMode.Back;
        srcBlend = BlendMode.One;
        dstBlend = BlendMode.OneMinusSrcAlpha;
        zWrite = false;
        reciveShadows = false;
        premultiplyAlpha = true;
        setPassEnabled("ShadowCaster", false);
        renderQueue = RenderQueue.Transparent;
    }

    private void transparentWithShadowsPreset()
    {
        if (!GUILayout.Button("Transparent with Shadows"))
            return;
        m_editor.RegisterPropertyChangeUndo("Transparent Preset");
        clipping = EClipMode.Shadows;
        cull = CullMode.Back;
        srcBlend = BlendMode.One;
        dstBlend = BlendMode.OneMinusSrcAlpha;
        zWrite = false;
        reciveShadows = true;
        premultiplyAlpha = true;
        setPassEnabled("ShadowCaster", true);
        renderQueue = RenderQueue.Transparent;
    }

}
