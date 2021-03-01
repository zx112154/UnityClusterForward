using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

public class CameraRenderBase
{

    /// <summary>
    /// 当前渲染相机
    /// </summary>
    protected Camera m_camera;

    /// <summary>
    /// 渲染管道Context
    /// </summary>
    protected ScriptableRenderContext m_context;

    protected SRenderPipelineParam m_params;

    /// <summary>
    /// 剔除结果
    /// </summary>
    protected CullingResults m_cullingResults;

    /// <summary>
    /// 相机渲染Buffer
    /// </summary>
    protected CommandBuffer m_cameraBuffer = new CommandBuffer { name = "Custom Camera" };

#if UNITY_EDITOR
    private static ShaderTagId[] m_legacyShaderTagIds = {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM"),
    };

    protected Material m_errorMaterial;

    protected string sampleName { get; set; }

    public virtual void Render(ScriptableRenderContext _context, Camera _camera)
    {
    }

    /// <summary>
    /// 绘制不支持的材质
    /// </summary>
    protected void drawUnsupportedShaders()
    {
        if (m_errorMaterial == null)
            m_errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));

        var drawingSettings = new DrawingSettings(m_legacyShaderTagIds[0], new SortingSettings(m_camera))
        {
            overrideMaterial = m_errorMaterial
        };
        for (int i = 1; i < m_legacyShaderTagIds.Length; i++)
        {
            drawingSettings.SetShaderPassName(i, m_legacyShaderTagIds[i]);
        }
        var filteringSettings = FilteringSettings.defaultValue;
        m_context.DrawRenderers(m_cullingResults, ref drawingSettings, ref filteringSettings);
    }

    /// <summary>
    /// 绘制相机空间
    /// </summary>
    protected void drawGizmos()
    {
        if (Handles.ShouldRenderGizmos())
        {
            m_context.DrawGizmos(m_camera, GizmoSubset.PreImageEffects);
            m_context.DrawGizmos(m_camera, GizmoSubset.PostImageEffects);
        }
    }

    /// <summary>
    /// 在编辑下显示UI
    /// </summary>
    protected void prepareForSceneWindow()
    {
        if (m_camera.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(m_camera);
        }
    }

    /// <summary>
    /// 执行单独的一个CommandBuffer
    /// </summary>
    /// <param name="_cmd"></param>
    protected void executeBuffer(CommandBuffer _cmd)
    {
        m_context.ExecuteCommandBuffer(_cmd);
        _cmd.Clear();
    }

    /// <summary>
    /// 设置Command Buffer 名字
    /// </summary>
    protected void prepareBuffer()
    {
        Profiler.BeginSample("Editor Only");
        m_cameraBuffer.name = sampleName = m_camera.name;
        Profiler.EndSample();
    }
#else
    string sampleName => "Custom Renderer Pipeline";
#endif

}
