/*****************************************************************************
*                                                                            *
*  @file     CameraRenderer.cs                                               *
*  @brief    单个相机自定义渲染脚本 基于前向渲染                             *
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
using UnityEngine.Rendering;
//控制函数版本
using Conditional = System.Diagnostics.ConditionalAttribute;

public class CameraRenderer : CameraRenderBase
{
    #region 渲染所需Buffer

    /// <summary>
    /// 阴影Buffer
    /// </summary>
    private CommandBuffer m_shadowBuffer = new CommandBuffer { name = "Render Shadows" };

    /// <summary>
    /// 后处理Buffer
    /// </summary>
    private CommandBuffer m_postProcessingBuffer = new CommandBuffer { name = "Post Processing" };

    #endregion

    #region 全局缓存变量

    /// <summary>
    /// 后处理堆栈
    /// </summary>
    private MyPostProcessingStack m_activeStack;

    /// <summary>
    /// Msaa
    /// </summary>
    private int m_renderSamples;

    /// <summary>
    /// 渲染分辨率
    /// </summary>
    private bool m_scaledRendering;
    private Vector2Int m_renderSize;

    /// <summary>
    /// 是否渲染到贴图
    /// </summary>
    private bool m_renderToTexture;

    /// <summary>
    /// 是否需要深度
    /// </summary>
    private bool m_needsDepth;

    private bool m_needsDirectDepth;

    /// <summary>
    /// 深度渲染是否需要单独pass
    /// </summary>
    private bool m_needsDepthOnlyPass;

    /// <summary>
    /// 渲染贴图格式
    /// </summary>
    private RenderTextureFormat m_format;

    #endregion

    #region 静态shader id

    /// <summary>
    /// 通用shader id
    /// </summary>
    private static ShaderTagId s_unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

    /// <summary>
    /// 渲染颜色缓冲id
    /// </summary>
    private static int s_cameraColorTextureId = Shader.PropertyToID("_CameraColorTexture");

    /// <summary>
    /// 渲染深度缓冲
    /// </summary>
    private static int s_cameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");

    #endregion

    #region 光照变量

    /// <summary>
    /// 最大灯光数量
    /// </summary>
    private const int m_maxVisibleLights = 8;

    static int m_visibleLightColorsId = Shader.PropertyToID("_VisibleLightColors");
    static int m_visibleLightDirectionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int m_visibleLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    static int m_visibleLightSpotDirectionId = Shader.PropertyToID("_VisibleLightSpotDirections");
    static int m_lightIndicesOffsetAndCountId = Shader.PropertyToID("unity_LightData");

    Vector4[] m_visibleLightColors = new Vector4[m_maxVisibleLights];
    Vector4[] m_visibleLightDirectionsOrPositions = new Vector4[m_maxVisibleLights];
    Vector4[] m_visibleLightAttenuations = new Vector4[m_maxVisibleLights];
    Vector4[] m_visibleLightSpotDirections = new Vector4[m_maxVisibleLights];
    Vector4[] m_shadowData = new Vector4[m_maxVisibleLights];

    #endregion

    #region 阴影变量

    //阴影贴图分块数量
    private int m_shadowTileCount = 0;

    private RenderTexture m_shadowMap, m_cascadedShadowMap;
    private static int m_shadowMapId = Shader.PropertyToID("_ShadowMap");
    private static int m_cascadedShadowMapId = Shader.PropertyToID("_CascadedShadowMap");
    private static int m_worldToShadowMatricesId = Shader.PropertyToID("_WorldToShadowMatrices");
    private static int m_shadowBiasId = Shader.PropertyToID("_ShadowBias");
    private static int m_shadowMapSizeId = Shader.PropertyToID("_ShadowMapSize");
    private static int m_shadowDataId = Shader.PropertyToID("_ShadowData");
    private static int m_globalShadowDataId = Shader.PropertyToID("_GlobalShadowData");
    const string m_shadowsSoftKeyWord = "_SHADOWS_SOFT";
    const string m_shadowsHardKeyWord = "_SHADOWS_HARD";

    //为主光源创建CSM
    private bool m_mainLightExits;

    //级联阴影
    const string m_cascadedShadowsHardKeyword = "_CASCADED_SHADOWS_HARD";
    const string m_cascadedShadowsSoftKeyword = "_CASCADED_SHADOWS_SOFT";

    private Matrix4x4[] m_worldToShadowCascadeMatrices = new Matrix4x4[5];
    private static int m_worldToShadowCascadeMatricesId = Shader.PropertyToID("_WorldToShadowCascadeMatrices");

    private static int m_cascadedShadowMapSizeId = Shader.PropertyToID("_CascadedShadowMapSize");
    private static int m_cascadedShadowStrengthId = Shader.PropertyToID("_CascadedShadowStrength");

    private static int m_cascadedCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    private Vector4[] m_cascadedCullingSpheres = new Vector4[4];
    #endregion

    public CameraRenderer(SRenderPipelineParam _params)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m_worldToShadowCascadeMatrices[4].m33 = 1.0f;
        }
        m_params = _params;
    }

    /// <summary>
    /// 单个相机主渲染函数
    /// </summary>
    /// <param name="_context"></param>
    /// <param name="_camera"></param>
    public override void Render(ScriptableRenderContext _context, Camera _camera)
    {
        this.m_context = _context;
        this.m_camera = _camera;

        //准备渲染前的数据
        perpareData();

        //准备相机Buffer
        prepareBuffer();

        //处理在场景视窗中的渲染
        prepareForSceneWindow();

        //对相机进行剔除
        if (!cull())
            return;

        //处理灯光
        if (m_cullingResults.visibleLights.Length > 0)
        {
            //根据场景中灯光的信息，构建灯光信息
            configureLights();

            //判断是否存在主光源
            if (m_mainLightExits)
            {
                renderCascadedShadows();
            }
            else
            {
                m_cameraBuffer.DisableShaderKeyword(m_cascadedShadowsHardKeyword);
                m_cameraBuffer.DisableShaderKeyword(m_cascadedShadowsSoftKeyword);
            }

            //渲染阴影贴图必须在剔除前，构建相机之后
            if (m_shadowTileCount > 0)
                renderShadows();
            else
            {
                m_cameraBuffer.DisableShaderKeyword(m_shadowsHardKeyWord);
                m_cameraBuffer.DisableShaderKeyword(m_shadowsSoftKeyWord);
            }
        }
        else
        {
            m_cameraBuffer.SetGlobalVector(m_lightIndicesOffsetAndCountId, Vector4.zero);
            m_cameraBuffer.DisableShaderKeyword(m_cascadedShadowsHardKeyword);
            m_cameraBuffer.DisableShaderKeyword(m_cascadedShadowsSoftKeyword);
            m_cameraBuffer.DisableShaderKeyword(m_shadowsHardKeyWord);
            m_cameraBuffer.DisableShaderKeyword(m_shadowsSoftKeyWord);
        }

        //设置相机渲染状态
        setUp();

        //开始绘制场景
        drawVisibleGeometry();

        drawUnsupportedShaders();

        drawGizmos();

        submit();

        if (m_shadowMap)
        {
            RenderTexture.ReleaseTemporary(m_shadowMap);
            m_shadowMap = null;
        }
        if (m_cascadedShadowMap)
        {
            RenderTexture.ReleaseTemporary(m_cascadedShadowMap);
            m_cascadedShadowMap = null;
        }
    }

    /// <summary>
    /// 准备渲染时需要的数据
    /// </summary>
    private void perpareData()
    {
        //当前相机是否需要后处理
        var myPipelineCamera = m_camera.GetComponent<MyPipelineCamera>();
        m_activeStack = myPipelineCamera ? myPipelineCamera.postProcessingStack : m_params.defaultStack;

        //根据是否开启HDR选用渲染纹理格式
        m_format = m_params.allowHDR && m_camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

        //获取多重采样抗锯齿参数
        //如果Msaa不被支持返回0
        QualitySettings.antiAliasing = m_params.msaa;
        m_params.msaa = Mathf.Max(QualitySettings.antiAliasing, 1);
        m_renderSamples = m_camera.allowMSAA ? m_params.msaa : 1;

        //缩放分辨率
        m_scaledRendering = (m_params.renderScale < 1.0f || m_params.renderScale > 1.0f) && m_camera.cameraType == CameraType.Game;
        m_renderSize.x = m_camera.pixelWidth;
        m_renderSize.y = m_camera.pixelHeight;
        if (m_scaledRendering)
        {
            m_renderSize.x = (int)(m_renderSize.x * m_params.renderScale);
            m_renderSize.y = (int)(m_renderSize.y * m_params.renderScale);
        }

        //判断是否渲染到纹理
        m_renderToTexture = m_scaledRendering || m_renderSamples > 1 || m_activeStack;

        //判断是否需要深度贴图
        m_needsDepth = m_activeStack && m_activeStack.needsDepth;

        //如果开启多重采样，需要单独的一个Pass渲染深度
        m_needsDirectDepth = m_needsDepth && m_renderSamples == 1;
        m_needsDepthOnlyPass = m_needsDepth && m_renderSamples > 1;
    }

    /// <summary>
    /// 构建相机相关属性
    /// </summary>
    private void setUp()
    {
        //构建相机属性
        m_context.SetupCameraProperties(m_camera);

        //设置相机渲染到纹理
        if (m_renderToTexture)
        {
            //创建相机深度和颜色纹理
            //设置相机纹理的格式和读写，以及采样次数
            m_cameraBuffer.GetTemporaryRT(
                s_cameraColorTextureId, m_renderSize.x, m_renderSize.y, m_needsDirectDepth ? 0 : 24, FilterMode.Bilinear,
                m_format, RenderTextureReadWrite.Default, m_renderSamples);

            if (m_needsDepth)
            {
                //深度纹理设置在线性空间
                m_cameraBuffer.GetTemporaryRT(s_cameraDepthTextureId, m_renderSize.x, m_renderSize.y, 24, FilterMode.Point, RenderTextureFormat.Depth,
                    RenderTextureReadWrite.Linear, 1);
            }

            //设置相机渲染目标
            if (m_needsDirectDepth)
            {
                m_cameraBuffer.SetRenderTarget(s_cameraColorTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                                               s_cameraDepthTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            }
            else
            {
                m_cameraBuffer.SetRenderTarget(s_cameraColorTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            }
        }

        m_cameraBuffer.ClearRenderTarget((m_camera.clearFlags & CameraClearFlags.Depth) != 0, (m_camera.clearFlags & CameraClearFlags.Color) != 0, m_params.clearColor);

        m_cameraBuffer.BeginSample(sampleName);

        m_cameraBuffer.SetGlobalVectorArray(m_visibleLightColorsId, m_visibleLightColors);
        m_cameraBuffer.SetGlobalVectorArray(m_visibleLightDirectionsId, m_visibleLightDirectionsOrPositions);
        m_cameraBuffer.SetGlobalVectorArray(m_visibleLightAttenuationsId, m_visibleLightAttenuations);
        m_cameraBuffer.SetGlobalVectorArray(m_visibleLightSpotDirectionId, m_visibleLightSpotDirections);

        executeBuffer(m_cameraBuffer);
    }

    /// <summary>
    /// 绘制场景
    /// </summary>
    private void drawVisibleGeometry()
    {
        //渲染不透明物体
        var sortingSettings = new SortingSettings(m_camera) { criteria = SortingCriteria.CommonOpaque };
        var drawingSettings = new DrawingSettings(s_unlitShaderTagId, sortingSettings);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        //设置单个物体输入灯光数据
        if (m_cullingResults.visibleLights.Length > 0)
            drawingSettings.perObjectData = PerObjectData.LightData | PerObjectData.LightIndices;

        //设置启用反射探针
        drawingSettings.perObjectData |= PerObjectData.ReflectionProbes;

        //是否开启动态批处理
        drawingSettings.enableDynamicBatching = m_params.bDynamicBatching;
        //是否开启GPU Instancing
        drawingSettings.enableInstancing = m_params.bInsancing;

        //提交绘制命令
        m_context.DrawRenderers(m_cullingResults, ref drawingSettings, ref filteringSettings);

        //处理不透明物体的后处理
        if (m_activeStack)
        {
            if (m_needsDepthOnlyPass)
            {
                //单独渲染一次深度纹理
                var sortSetting = new SortingSettings(m_camera);

                var depthOnlyDrawSettings = new DrawingSettings(new ShaderTagId("DepthOnly"), sortSetting);
                var depthOnlyFilterSettings = new FilteringSettings();
                depthOnlyFilterSettings.renderQueueRange = RenderQueueRange.opaque;

                m_cameraBuffer.SetRenderTarget(s_cameraDepthTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

                m_cameraBuffer.ClearRenderTarget(true, false, Color.clear);

                executeBuffer(m_cameraBuffer);

                m_context.DrawRenderers(m_cullingResults, ref depthOnlyDrawSettings, ref depthOnlyFilterSettings);
            }

            SPostProcessingParam p = new SPostProcessingParam
            {
                cb = m_postProcessingBuffer,
                cameraColorId = s_cameraColorTextureId,
                cameraDepthId = s_cameraDepthTextureId,
                width = m_renderSize.x,
                height = m_renderSize.y,
                samples = m_renderSamples,
                format = m_format
            };

            //调用不透明后处理
            m_activeStack.RenderAfterOpaque(p);
            executeBuffer(m_postProcessingBuffer);

            //设置相机渲染纹理
            if (m_needsDirectDepth)
            {
                m_cameraBuffer.SetRenderTarget(s_cameraColorTextureId,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,
                    s_cameraDepthTextureId,
                    RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            }
            else
            {
                m_cameraBuffer.SetRenderTarget(s_cameraColorTextureId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            }
            executeBuffer(m_cameraBuffer);
        }

        //渲染天空盒
        m_context.DrawSkybox(m_camera);

        //渲染不透明物体
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        m_context.DrawRenderers(m_cullingResults, ref drawingSettings, ref filteringSettings);

        //进行不透明后处理
        if (m_renderToTexture)
        {
            //进行后处理
            if (m_activeStack)
            {
                m_activeStack.RenderAfterTransparent();
                executeBuffer(m_postProcessingBuffer);
            }
            else
            {
                m_cameraBuffer.Blit(s_cameraColorTextureId, BuiltinRenderTextureType.CameraTarget);
            }

            //释放掉相机纹理
            m_cameraBuffer.ReleaseTemporaryRT(s_cameraColorTextureId);
            if (m_needsDepth)
                m_cameraBuffer.ReleaseTemporaryRT(s_cameraDepthTextureId);
        }

    }

    /// <summary>
    /// 提交命令
    /// </summary>
    private void submit()
    {
        m_cameraBuffer.EndSample(sampleName);
        executeBuffer(m_cameraBuffer);
        m_context.Submit();
    }

    /// <summary>
    /// 获取相机剔除结果
    /// </summary>
    /// <returns></returns>
    private bool cull()
    {
        if (m_camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(m_params.shadowDistance, m_camera.farClipPlane);
            m_cullingResults = m_context.Cull(ref p);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 构建灯光信息
    /// </summary>
    private void configureLights()
    {
        m_mainLightExits = false;
        m_shadowTileCount = 0;


        for (int i = 0; i < m_cullingResults.visibleLights.Length; i++)
        {
            //相机控制中存在的灯光大于最大灯光配置时停止
            if (i == m_maxVisibleLights)
                break;

            VisibleLight light = m_cullingResults.visibleLights[i];

            //设置灯光颜色
            m_visibleLightColors[i] = light.finalColor;

            //灯光衰减信息
            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1.0f;

            Vector4 shadow = Vector4.zero;

            if (light.lightType == LightType.Directional)
            {
                //通过取灯光变换矩阵的Z取反获得灯光方向
                Vector4 v = light.localToWorldMatrix.GetColumn(2);
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;

                //设置灯光的方向
                m_visibleLightDirectionsOrPositions[i] = v;

                shadow = configureShadows(i, light.light);
                shadow.z = 1.0f;

                //判断是否使用CSM 
                if (i == 0 && shadow.x > 0.0f && m_params.shadowCascades > 0)
                {
                    m_mainLightExits = true;
                    m_shadowTileCount -= 1;
                }
            }
            else
            {
                //提取第4列的位置变换保存灯光位置
                m_visibleLightDirectionsOrPositions[i] = light.localToWorldMatrix.GetColumn(3);

                attenuation.x = 1f / Mathf.Max(light.range * light.range, 0.00001f);

                if (light.lightType == LightType.Spot)
                {
                    Vector4 v = light.localToWorldMatrix.GetColumn(2);
                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;

                    //聚光灯方向
                    m_visibleLightSpotDirections[i] = v;

                    //计算聚光灯的半角余弦值
                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);

                    //定义聚光灯内外角关系，计算内角余弦值
                    float innerCos = Mathf.Cos(Mathf.Atan((46f / 64f) * outerTan));
                    //计算内外角余弦差值，避免为0
                    //cos(ri) - cos(r0)
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    attenuation.z = 1.0f / angleRange;
                    // - cos(r0) / cos(ri) - cos(r0)
                    attenuation.w = -outerCos * attenuation.z;

                    shadow = configureShadows(i, light.light);
                }
            }

            //方向光没有衰减信息
            m_visibleLightAttenuations[i] = attenuation;

            //构建阴影信息 x: 阴影强度 y: 阴影类型
            m_shadowData[i] = shadow;
        }

        //当场景灯光数量改变后清除没用的灯光信息
        if (m_cullingResults.visibleLights.Length > m_maxVisibleLights)
        {
            var indexs = m_cullingResults.GetLightIndexMap(new Unity.Collections.Allocator());

            if (m_mainLightExits)
                indexs[0] = -1;

            for (int i = m_maxVisibleLights; i < m_cullingResults.visibleLights.Length; i++)
            {
                indexs[i] = -1;
            }

            m_cullingResults.SetLightIndexMap(indexs);
        }
    }

    /// <summary>
    /// 渲染阴影
    /// </summary>
    /// <param name="_context"></param>
    private void renderShadows()
    {
        int split;
        if (m_shadowTileCount <= 1)
            split = 1;
        else if (m_shadowTileCount <= 4)
            split = 2;
        else if (m_shadowTileCount <= 9)
            split = 3;
        else
            split = 4;

        Matrix4x4 viewMatrix, projectionMatrix;
        ShadowSplitData splitData;

        float tileSize = m_params.shadowMapSize / split;
        float tileScale = 1.0f / split;

        m_shadowMap = setShadowRenderTarget();
        m_shadowBuffer.BeginSample("Render Shadows");

        m_shadowBuffer.SetGlobalVector(m_globalShadowDataId, new Vector4(tileScale, m_params.shadowDistance * m_params.shadowDistance));
        executeBuffer(m_shadowBuffer);

        m_shadowBuffer.ClearRenderTarget(true, true, Color.clear, 1);
        executeBuffer(m_shadowBuffer);

        Matrix4x4[] worldToShadowMatrices = new Matrix4x4[m_cullingResults.visibleLights.Length];

        int tileIndex = 0;
        bool hardShadows = false;
        bool softShadows = false;

        //主灯光跳过构建
        for (int i = m_mainLightExits ? 1 : 0; i < m_cullingResults.visibleLights.Length; i++)
        {
            if (i == m_maxVisibleLights)
                break;

            if (m_shadowData[i].x <= 0f)
                continue;

            bool bScb = m_cullingResults.GetShadowCasterBounds(i, out Bounds outBounds);

            bool bHaveShadow;

            //通过阴影数据判断是方向光还是聚光灯
            if (m_shadowData[i].z > 0.0f)
            {
                bHaveShadow = m_cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(i, 0, 1, Vector3.right, (int)tileSize,
                    m_cullingResults.visibleLights[i].light.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);
            }
            else
            {
                bHaveShadow = m_cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(i, out viewMatrix, out projectionMatrix, out splitData);
            }

            if (!(bHaveShadow && bScb))
            {
                m_shadowData[i].x = 0.0f;
                continue;
            }

            Vector2 tileOffset = configureShadowTile(tileIndex, split, tileSize);

            m_shadowData[i].z = tileOffset.x * tileScale;
            m_shadowData[i].w = tileOffset.y * tileScale;

            m_shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            m_shadowBuffer.SetGlobalFloat(m_shadowBiasId, m_cullingResults.visibleLights[i].light.shadowBias);
            executeBuffer(m_shadowBuffer);

            //绘制阴影贴图
            var shadowSetting = new ShadowDrawingSettings(m_cullingResults, i);
            shadowSetting.splitData = splitData;
            m_context.DrawShadows(ref shadowSetting);

            //检查Z轴是否反向
            //将深度从-1 - 1 映射到 0 - 1 
            calculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out worldToShadowMatrices[i]);

            tileIndex += 1;

            if (m_shadowData[i].y <= 0.0f)
                hardShadows = true;
            else
                softShadows = true;
        }

        //关闭裁剪
        m_shadowBuffer.DisableScissorRect();

        m_shadowBuffer.SetGlobalTexture(m_shadowMapId, m_shadowMap);
        m_shadowBuffer.SetGlobalVectorArray(m_shadowDataId, m_shadowData);
        m_shadowBuffer.SetGlobalMatrixArray(m_worldToShadowMatricesId, worldToShadowMatrices);

        float invShadowMapSize = 1.0f / m_params.shadowMapSize;
        m_shadowBuffer.SetGlobalVector(m_shadowMapSizeId, new Vector4(invShadowMapSize, invShadowMapSize, m_params.shadowMapSize, m_params.shadowMapSize));
        CoreUtils.SetKeyword(m_shadowBuffer, m_shadowsHardKeyWord, hardShadows);
        CoreUtils.SetKeyword(m_shadowBuffer, m_shadowsSoftKeyWord, softShadows);

        m_shadowBuffer.EndSample("Render Shadows");
        executeBuffer(m_shadowBuffer);
    }

    /// <summary>
    /// 渲染级联阴影
    /// </summary>
    private void renderCascadedShadows()
    {
        float tileSize = m_params.shadowMapSize / 2;

        m_cascadedShadowMap = setShadowRenderTarget();
        m_shadowBuffer.BeginSample("Render Cascaded Shadows");
        m_shadowBuffer.SetGlobalVector(m_globalShadowDataId, new Vector4(0.0f, m_params.shadowDistance * m_params.shadowDistance));
        executeBuffer(m_shadowBuffer);

        m_shadowBuffer.ClearRenderTarget(true, true, Color.clear);
        executeBuffer(m_shadowBuffer);

        Light shadowLight = m_cullingResults.visibleLights[0].light;
        m_shadowBuffer.SetGlobalFloat(m_shadowBiasId, shadowLight.shadowBias);

        var shadowSettings = new ShadowDrawingSettings(m_cullingResults, 0);
        var tileMatrix = Matrix4x4.identity;
        tileMatrix.m00 = tileMatrix.m11 = 0.5f;

        //构建级联世界到阴影贴图空间矩阵
        for (int i = 0; i < m_params.shadowCascades; i++)
        {
            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;
            m_cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(0, i, m_params.shadowCascades, m_params.shadowCascadeSplit,
                (int)tileSize, shadowLight.shadowNearPlane, out viewMatrix, out projectionMatrix, out splitData);

            //设置光照空间转换矩阵
            Vector2 tileOffset = configureShadowTile(i, 2, tileSize);
            m_shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            m_context.ExecuteCommandBuffer(m_shadowBuffer);
            m_shadowBuffer.Clear();

            //传入球半径的平方
            m_cascadedCullingSpheres[i] = splitData.cullingSphere;
            m_cascadedCullingSpheres[i].w *= splitData.cullingSphere.w;

            shadowSettings.splitData = splitData;
            m_context.DrawShadows(ref shadowSettings);
            calculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix, out m_worldToShadowCascadeMatrices[i]);

            //进行偏移
            tileMatrix.m03 = tileOffset.x * 0.5f;
            tileMatrix.m13 = tileOffset.y * 0.5f;
            m_worldToShadowCascadeMatrices[i] = tileMatrix * m_worldToShadowCascadeMatrices[i];
        }

        m_shadowBuffer.DisableScissorRect();
        m_shadowBuffer.SetGlobalTexture(m_cascadedShadowMapId, m_cascadedShadowMap);
        m_shadowBuffer.SetGlobalVectorArray(m_cascadedCullingSpheresId, m_cascadedCullingSpheres);
        m_shadowBuffer.SetGlobalMatrixArray(m_worldToShadowCascadeMatricesId, m_worldToShadowCascadeMatrices);

        float invShadowMapSize = 1.0f / m_params.shadowMapSize;

        m_shadowBuffer.SetGlobalVector(m_cascadedShadowMapSizeId, new Vector4(invShadowMapSize, invShadowMapSize, m_params.shadowMapSize, m_params.shadowMapSize));
        m_shadowBuffer.SetGlobalFloat(m_cascadedShadowStrengthId, shadowLight.shadowStrength);

        bool hard = shadowLight.shadows == LightShadows.Hard;
        CoreUtils.SetKeyword(m_shadowBuffer, m_cascadedShadowsHardKeyword, hard);
        CoreUtils.SetKeyword(m_shadowBuffer, m_cascadedShadowsSoftKeyword, !hard);

        m_shadowBuffer.EndSample("Render Cascaded Shadows");
        m_context.ExecuteCommandBuffer(m_shadowBuffer);
        m_shadowBuffer.Clear();
    }

    /// <summary>
    /// 构建阴影
    /// </summary>
    /// <param name="_lightIndex"></param>
    /// <param name="_shadowLight"></param>
    /// <returns></returns>
    private Vector4 configureShadows(int _lightIndex, Light _shadowLight)
    {
        Vector4 shadow = Vector4.zero;
        Bounds shadowBounds;
        if (_shadowLight.shadows != LightShadows.None && m_cullingResults.GetShadowCasterBounds(_lightIndex, out shadowBounds))
        {
            m_shadowTileCount += 1;

            //设置阴影强度
            shadow.x = _shadowLight.shadowStrength;

            //设置阴影类型
            shadow.y = _shadowLight.shadows == LightShadows.Soft ? 1.0f : 0.0f;
        }
        return shadow;
    }

    /// <summary>
    /// 设置渲染阴影贴图对象
    /// </summary>
    /// <returns></returns>
    private RenderTexture setShadowRenderTarget()
    {
        RenderTexture texture = RenderTexture.GetTemporary(m_params.shadowMapSize, m_params.shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        m_shadowBuffer.SetRenderTarget(texture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        return texture;
    }

    /// <summary>
    /// 构建阴影贴图块
    /// </summary>
    /// <param name="_tileIndex"></param>
    /// <param name="_split"></param>
    /// <param name="_tileSize"></param>
    /// <returns></returns>
    private Vector2 configureShadowTile(int _tileIndex, int _split, float _tileSize)
    {
        Vector2 tileOffset;
        tileOffset.x = _tileIndex % _split;
        tileOffset.y = _tileIndex / _split;
        var tileViewport = new Rect(tileOffset.x * _tileSize, tileOffset.y * _tileSize, _tileSize, _tileSize);
        m_shadowBuffer.SetViewport(tileViewport);
        m_shadowBuffer.EnableScissorRect(new Rect(tileViewport.x + 4.0f, tileViewport.y + 4.0f, _tileSize - 8.0f, _tileSize - 8.0f));

        return tileOffset;
    }

    /// <summary>
    /// 计算世界到阴影空间的矩阵
    /// </summary>
    /// <param name="_viewMatrix"></param>
    /// <param name="_projectionMatrix"></param>
    /// <param name="_worldToShadowMatrix"></param>
    private void calculateWorldToShadowMatrix(ref Matrix4x4 _viewMatrix, ref Matrix4x4 _projectionMatrix, out Matrix4x4 _worldToShadowMatrix)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            _projectionMatrix.m20 = -_projectionMatrix.m20;
            _projectionMatrix.m21 = -_projectionMatrix.m21;
            _projectionMatrix.m22 = -_projectionMatrix.m22;
            _projectionMatrix.m23 = -_projectionMatrix.m23;
        }
        var scaleOffset = Matrix4x4.identity;
        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;
        _worldToShadowMatrix = scaleOffset * (_projectionMatrix * _viewMatrix);
    }

}
