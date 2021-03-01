using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DeferredCameraRenderer : CameraRenderBase
{
    private const int m_maxVisibleLights = 8;

    private static ShaderTagId s_positionShaderTagId = new ShaderTagId("DeferredShadingData");

    /// <summary>
    /// 多重采样延迟渲染需要的数据
    /// </summary>
    private static RenderTargetIdentifier[] s_mrt = new RenderTargetIdentifier[4];

    private static int s_tex_position = Shader.PropertyToID("_DeferredCameraRendererPosTex");
    private static int s_tex_normal = Shader.PropertyToID("_DeferredCameraRendererNormalTex");
    private static int s_tex_albedoSpec = Shader.PropertyToID("_DeferredCameraRendererColorTex");
    private static int s_tex_lightParam = Shader.PropertyToID("_DeferredCameraRendererLightParamTex");
    private static int s_tex_depth = Shader.PropertyToID("_DeferredCameraRendererDepthTexture");


    private static int s_posTexId = Shader.PropertyToID("_PosTex");
    private static int s_normalTexId = Shader.PropertyToID("_NormalTex");
    private static int s_colorTexId = Shader.PropertyToID("_AlbedoSpecTex");
    private static int s_lightParamTexId = Shader.PropertyToID("_LightParamTex");
    private static int s_screenTexId = Shader.PropertyToID("_SrceenTex");

    private static int s_visibleLightCountId = Shader.PropertyToID("_VisibleLightCount");
    private static int s_visibleLightColorId = Shader.PropertyToID("_VisibleLightColors");
    private static int s_visibleLightDirectionsId = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    private static int s_visibleLightAttenuationId = Shader.PropertyToID("_VisibleLightAttenuations");
    private static int s_visibleLightSpotDirectionId = Shader.PropertyToID("_VisibleLightSpotDirections");

    private Vector4[] m_lightCol = new Vector4[m_maxVisibleLights];
    private Vector4[] m_lightDirAndPos = new Vector4[m_maxVisibleLights];
    private Vector4[] m_lightAttenuation = new Vector4[m_maxVisibleLights];
    private Vector4[] m_lightSpotDirections = new Vector4[m_maxVisibleLights];

    private static Mesh s_fullScreenTriangle;
    private static Material s_fullScreen_material;
    public Mesh FullScreenTriangle
    {
        get
        {
            if (s_fullScreenTriangle == null)
                initializeStatic();
            return s_fullScreenTriangle;
        }
    }

    private static Material s_deferredMat;

    private RenderTexture m_tex_pos;
    private RenderTexture m_tex_normal;
    private RenderTexture m_tex_color;
    private RenderTexture m_tex_lighting;

    /// <summary>
    /// 屏幕分辩率
    /// </summary>
    private Vector2Int m_renderSize;

    /// <summary>
    /// 是否修改屏幕分辨率
    /// </summary>
    private bool m_scaledRendering = false;

    public DeferredCameraRenderer(SRenderPipelineParam _params)
    {
        //initializeStatic();
        m_params = _params;
    }

    public override void Render(ScriptableRenderContext _context, Camera _camera)
    {
        this.m_context = _context;
        this.m_camera = _camera;

        m_cameraBuffer.BeginSample(sampleName);

        //准备渲染前的数据
        perpareData();

        //设置相机BUFFER名称
        prepareBuffer();

        //处理场景视窗渲染
        prepareForSceneWindow();

        //进行相机剔除
        if (!cull())
            return;

        //获取当前相机物体的信息
        setUp();

        m_cameraBuffer.BeginSample("Deferred Shading");

        drawVisibleGeometry();

        m_cameraBuffer.EndSample("Deferred Shading");
        executeBuffer(m_cameraBuffer);

        m_cameraBuffer.BeginSample("Deferred Lighting");

        deferredLighting();

        m_cameraBuffer.EndSample("Deferred Lighting");
        executeBuffer(m_cameraBuffer);

        drawUnsupportedShaders();

        drawGizmos();

        m_cameraBuffer.EndSample(sampleName);
        executeBuffer(m_cameraBuffer);

        submit();
    }

    private static void initializeStatic()
    {
        if (s_fullScreenTriangle)
            return;
        s_fullScreenTriangle = new Mesh
        {
            name = "My Post-Processing Stack Full-Screen Triangle",
            vertices = new Vector3[] {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f,  3.0f, 0.0f),
                new Vector3( 3.0f, -1.0f, 0.0f)
            },
            triangles = new int[] { 0, 1, 2 },
        };
        s_fullScreenTriangle.subMeshCount = 1;
        s_fullScreenTriangle.UploadMeshData(true);

        s_fullScreen_material = new Material(Shader.Find("MyPipeline/DeferredLit"))
        {
            name = "DeferredLit",
            hideFlags = HideFlags.HideAndDontSave
        };

        s_deferredMat = new Material(Shader.Find("MyPipeline/DeferredLit"))
        {
            name = "DeferredShading",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    /// <summary>
    /// 准备数据
    /// </summary>
    private void perpareData()
    {
        //处理屏幕分辨率
        m_scaledRendering = (m_params.renderScale < 1.0f || m_params.renderScale > 1.0f) && m_camera.cameraType == CameraType.Game;
        m_renderSize.x = m_camera.pixelWidth;
        m_renderSize.y = m_camera.pixelHeight;
        if (m_scaledRendering)
        {
            m_renderSize.x = (int)(m_renderSize.x * m_params.renderScale);
            m_renderSize.y = (int)(m_renderSize.y * m_params.renderScale);
        }

    }

    /// <summary>
    /// 对相机进行剔除
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

    private void setUp()
    {
        m_context.SetupCameraProperties(m_camera);

        m_cameraBuffer.GetTemporaryRT(s_tex_position, m_renderSize.x, m_renderSize.y, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
        m_cameraBuffer.GetTemporaryRT(s_tex_normal, m_renderSize.x, m_renderSize.y, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
        m_cameraBuffer.GetTemporaryRT(s_tex_albedoSpec, m_renderSize.x, m_renderSize.y, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
        m_cameraBuffer.GetTemporaryRT(s_tex_lightParam, m_renderSize.x, m_renderSize.y, 0, FilterMode.Point, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 1);
        m_cameraBuffer.GetTemporaryRT(s_tex_depth, m_renderSize.x, m_renderSize.y, 24, FilterMode.Point, RenderTextureFormat.Depth,
            RenderTextureReadWrite.Linear, 1);

        s_mrt[0] = s_tex_position;
        s_mrt[1] = s_tex_normal;
        s_mrt[2] = s_tex_albedoSpec;
        s_mrt[3] = s_tex_lightParam;

        executeBuffer(m_cameraBuffer);
    }

    private void drawVisibleGeometry()
    {
        //进行渲染设置
        var sortingSettings = new SortingSettings(m_camera) { criteria = SortingCriteria.CommonOpaque };
        var drawingSettings = new DrawingSettings(s_positionShaderTagId, sortingSettings);
        var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        //是否开启动态批处理
        drawingSettings.enableDynamicBatching = m_params.bDynamicBatching;

        //是否开启GPU Instancing
        drawingSettings.enableInstancing = m_params.bInsancing;

        //设置多重渲染目标
        m_cameraBuffer.SetRenderTarget(s_mrt, BuiltinRenderTextureType.CameraTarget, 0, 0, 0);

        //设置清理状态
        m_cameraBuffer.ClearRenderTarget(true, true, m_params.clearColor);
        executeBuffer(m_cameraBuffer);

        m_context.DrawRenderers(m_cullingResults, ref drawingSettings, ref filteringSettings);
    }

    private void deferredLighting()
    {
        m_cameraBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
            BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_cameraBuffer.ClearRenderTarget(true, true, m_params.clearColor);
        executeBuffer(m_cameraBuffer);

        m_context.DrawSkybox(m_camera);

        int lightCount = m_cullingResults.visibleLights.Length;

        for (int i = 0; i < m_maxVisibleLights; i++)
        {
            m_lightCol[i] = Vector4.zero;
            m_lightDirAndPos[i] = Vector4.zero;
            m_lightAttenuation[i] = Vector4.zero;
            m_lightSpotDirections[i] = Vector4.zero;
        }

        for (int i = 0; i < lightCount; i++)
        {
            if (i == m_maxVisibleLights)
                break;

            VisibleLight light = m_cullingResults.visibleLights[i];

            //构建光照信息
            m_lightCol[i] = light.finalColor;

            //灯光衰减信息
            m_lightAttenuation[i] = Vector4.zero;
            m_lightAttenuation[i].w = 1.0f;

            m_lightDirAndPos[i] = Vector4.zero;

            if (light.lightType == LightType.Directional)
            {
                //灯光方向
                m_lightDirAndPos[i] = light.localToWorldMatrix.GetColumn(2);
                m_lightDirAndPos[i].x = -m_lightDirAndPos[i].x;
                m_lightDirAndPos[i].y = -m_lightDirAndPos[i].y;
                m_lightDirAndPos[i].z = -m_lightDirAndPos[i].z;
            }
            else
            {
                m_lightDirAndPos[i] = light.localToWorldMatrix.GetColumn(3);
                m_lightAttenuation[i].x = 1.0f / Mathf.Max(light.range * light.range, 0.00001f);

                if (light.lightType == LightType.Spot)
                {
                    m_lightDirAndPos[i] = light.localToWorldMatrix.GetColumn(2);
                    m_lightDirAndPos[i].x = -m_lightDirAndPos[i].x;
                    m_lightDirAndPos[i].y = -m_lightDirAndPos[i].y;
                    m_lightDirAndPos[i].z = -m_lightDirAndPos[i].z;

                    //聚光灯方向
                    m_lightSpotDirections[i] = m_lightDirAndPos[i];

                    //聚光灯半角余弦
                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);

                    //定义聚光灯内外角关系，计算内角余弦值
                    float innerCos = Mathf.Cos(Mathf.Atan((46f / 64f) * outerTan));
                    //计算内外角余弦差值，避免为0
                    //cos(ri) - cos(r0)
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    m_lightAttenuation[i].z = 1.0f / angleRange;
                    // - cos(r0) / cos(ri) - cos(r0)
                    m_lightAttenuation[i].w = -outerCos * m_lightAttenuation[i].z;
                }
            }
        }

        m_cameraBuffer.SetGlobalTexture(s_posTexId, s_tex_position);
        m_cameraBuffer.SetGlobalTexture(s_normalTexId, s_tex_normal);
        m_cameraBuffer.SetGlobalTexture(s_colorTexId, s_tex_albedoSpec);
        m_cameraBuffer.SetGlobalTexture(s_lightParamTexId, s_tex_lightParam);

        m_cameraBuffer.SetGlobalInt(s_visibleLightCountId, lightCount);
        m_cameraBuffer.SetGlobalVectorArray(s_visibleLightColorId, m_lightCol);
        m_cameraBuffer.SetGlobalVectorArray(s_visibleLightDirectionsId, m_lightDirAndPos);
        m_cameraBuffer.SetGlobalVectorArray(s_visibleLightAttenuationId, m_lightAttenuation);
        m_cameraBuffer.SetGlobalVectorArray(s_visibleLightSpotDirectionId, m_lightSpotDirections);
        executeBuffer(m_cameraBuffer);

        m_cameraBuffer.DrawMesh(FullScreenTriangle, Matrix4x4.identity, s_fullScreen_material, 0, 1);

        m_cameraBuffer.ReleaseTemporaryRT(s_tex_position);
        m_cameraBuffer.ReleaseTemporaryRT(s_tex_normal);
        m_cameraBuffer.ReleaseTemporaryRT(s_tex_albedoSpec);
        m_cameraBuffer.ReleaseTemporaryRT(s_tex_lightParam);
        m_cameraBuffer.ReleaseTemporaryRT(s_tex_depth);

        executeBuffer(m_cameraBuffer);
    }

    private void submit()
    {
        m_context.Submit();
    }

}
