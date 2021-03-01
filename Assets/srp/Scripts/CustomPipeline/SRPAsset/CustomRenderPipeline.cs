/*****************************************************************************
*                                                                            *
*  @file     CustomRenderPipeline.cs                                         *
*  @brief    自定义渲染管线资源创建脚本                                      *
*  Details.                                                                  *
*                                                                            *
*  @author   zhangfan                                                        *
*                                                                            *
*----------------------------------------------------------------------------*
*  Change History :                                                          *
*  <Date>     | <Version> | <Author>       | <Description>                   *
*----------------------------------------------------------------------------*
*  2019/11/25 | 1.0.0.1   | zhangfan      | Create Custom Render Pipeline    *
*----------------------------------------------------------------------------*
*                                                                            *
*****************************************************************************/


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public struct SRenderPipelineParam
{
    public Color clearColor;
    public bool bDynamicBatching;
    public bool bInsancing;
    public int shadowMapSize;
    public float shadowDistance;
    public int shadowCascades;
    public Vector3 shadowCascadeSplit;
    public MyPostProcessingStack defaultStack;
    public float renderScale;
    public int msaa;
    public bool allowHDR;
    public RenderingPath renderPath;
    public bool enableForwardAdd;           //是否启用forward+
}

public class CustomRenderPipeline : RenderPipeline
{
    /// <summary>
    /// forward srp
    /// </summary>
    private CameraRenderer m_renderer;
    /// <summary>
    /// deffered srp
    /// </summary>
    private DeferredCameraRenderer m_deferred_renderer;

    private ForwardAddSRP m_forwardAddSRP;

    /// <summary>
    /// 渲染管线参数集合
    /// </summary>
    private SRenderPipelineParam m_rpp = new SRenderPipelineParam();

    public CustomRenderPipeline(SRenderPipelineParam _rpp)
    {
        m_rpp = _rpp;
        //将光照强度从gamma空间变换到线性空间
        GraphicsSettings.lightsUseLinearIntensity = true;
        m_renderer = new CameraRenderer(_rpp);
        m_deferred_renderer = new DeferredCameraRenderer(_rpp);
        m_forwardAddSRP = new ForwardAddSRP(_rpp);
    }

    protected override void Render(ScriptableRenderContext _context, Camera[] _cameras)
    {
        foreach (var camera in _cameras)
        {
            if (m_rpp.renderPath == RenderingPath.Forward)
            {
                if (m_rpp.enableForwardAdd)
                    m_forwardAddSRP.Render(_context, camera);
                else
                    m_renderer.Render(_context, camera);

            }
            else if (m_rpp.renderPath == RenderingPath.DeferredLighting)
                m_deferred_renderer.Render(_context, camera);
        }
    }
}
