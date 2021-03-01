/*****************************************************************************
*                                                                            *
*  @file     CustomRenderPipelineAsset.cs                                    *
*  @brief    自定义渲染管线资源创建编辑器脚本                                *
*  Details.                                                                  *
*                                                                            *
*  @author   zhangfan                                                        *
*                                                                            *
*----------------------------------------------------------------------------*
*  Change History :                                                          *
*  <Date>     | <Version> | <Author>    | <Description>                      *
*----------------------------------------------------------------------------*
*  2019/11/25 | 1.0.0.1   | zhangfan    | Create Custom Render Pipeline Menu *
*----------------------------------------------------------------------------*
*                                                                            *
*****************************************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "SRP-Demo/01 - Create Basic Asset Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    public enum EShadowMapSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096
    }

    public enum EShadowCascades
    {
        Zero = 0,
        Two = 2,
        Four = 4
    }

    public enum EMsaaMode
    {
        Off = 1,
        _2x = 2,
        _4x = 4,
        _8x = 8,
    }

    /// <summary>
    /// 阴影贴图大小
    /// </summary>
    [SerializeField]
    private EShadowMapSize m_shadowMapSize = EShadowMapSize._1024;

    /// <summary>
    /// 级联阴影参数
    /// </summary>
    [SerializeField]
    private EShadowCascades m_shadowCascades = EShadowCascades.Four;

    [SerializeField, HideInInspector]
    private float m_twoCascadesSplit = 0.25f;

    [SerializeField, HideInInspector]
    private Vector3 m_fourCascadesSplit = new Vector3(0.067f, 0.2f, 0.467f);

    /// <summary>
    /// 管线清除颜色
    /// </summary>
    public Color clearColor = Color.clear;

    /// <summary>
    /// 是否启用动态批处理
    /// </summary>
    public bool enableDynamicBatching;

    /// <summary>
    /// 是否开启GUP Instancing;
    /// </summary>
    public bool enableInstancing;

    /// <summary>
    /// 级联阴影距离
    /// </summary>
    public float shadowDistance;

    /// <summary>
    /// 后处理栈
    /// </summary>
    public MyPostProcessingStack defaultStack;

    /// <summary>
    /// 缩放分辨率
    /// </summary>
    [SerializeField, Range(0.25f, 2.0f)]
    private float m_renderScale = 1.0f;

    /// <summary>
    /// 多采样抗锯齿
    /// </summary>
    [SerializeField]
    private EMsaaMode m_msaa = EMsaaMode.Off;

    [SerializeField]
    private bool m_allowHDR = false;

    [SerializeField]
    private RenderingPath m_renderPath = RenderingPath.Forward;

    [SerializeField]
    private bool m_enableForwardAdd = true;

    protected override RenderPipeline CreatePipeline()
    {
        SRenderPipelineParam rpp = new SRenderPipelineParam();
        rpp.clearColor = clearColor;
        rpp.bDynamicBatching = enableDynamicBatching;
        rpp.bInsancing = enableInstancing;
        rpp.shadowMapSize = (int)m_shadowMapSize;
        rpp.shadowDistance = shadowDistance;
        rpp.shadowCascades = (int)m_shadowCascades;
        rpp.shadowCascadeSplit = m_shadowCascades == EShadowCascades.Four ? m_fourCascadesSplit : new Vector3(m_twoCascadesSplit, 0.0f);
        rpp.defaultStack = defaultStack;
        rpp.renderScale = m_renderScale;
        rpp.msaa = (int)m_msaa;
        rpp.allowHDR = m_allowHDR;
        rpp.renderPath = m_renderPath;
        rpp.enableForwardAdd = m_enableForwardAdd;
        return new CustomRenderPipeline(rpp);
    }
}
