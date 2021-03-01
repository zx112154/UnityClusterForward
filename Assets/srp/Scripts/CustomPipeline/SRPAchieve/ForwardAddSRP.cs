/*****************************************************************************
*                                                                            *
*  @file     CameraRenderer.cs                                               *
*  @brief    Forward+ 渲染管线                                               *
*  Details                                                                   *
*                                                                            *
*  @author   zhangfan                                                        *
*                                                                            *
*----------------------------------------------------------------------------*
*  Change History :                                                          *
*  <Date>     | <Version> | <Author>    | <Description>                      *
*----------------------------------------------------------------------------*
*  2020/7/1 | 1.0.0.1   | zhangfan    | Create Forward+                      *
*----------------------------------------------------------------------------*
*                                                                            *
*****************************************************************************/


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ForwardAddSRP : CameraRenderBase
{
    /// <summary>
    /// 光源信息
    /// </summary>
    private struct SLightSource
    {
        public Vector3 position;
        public Vector3 direction;
        public Vector4 color;
        public float spotAngle;
        public float range;
        public uint type;
    }

    /// <summary>
    /// 每个分块的size
    /// </summary>
    private static readonly int m_clusterWidth = 32;
    private static readonly int m_clusterHeight = 32;

    /// <summary>
    /// 在Z空间划分的数量
    /// </summary>
    private static readonly int m_clusterZCount = 16;

    /// <summary>
    /// 最大灯光数
    /// </summary>
    private static readonly int m_maxLightCount = 2048;


    /// <summary>
    /// 根据屏幕空间划分出来空间的数量
    /// </summary>
    private int m_clusterXYZCount = 0;

    private ComputeShader m_clusterRenderingCS;
    private ComputeShader m_clusterLightCullingCS;
    private ComputeBuffer m_clusterBuffer;  //屏幕分块信息
    private ComputeBuffer m_lightBuffer;    //灯光信息
    private int m_clusterComputeKernel;
    private int m_lightCullingKernel;

    private ComputeShader m_ssaoCS;
    private ComputeBuffer m_ssaoBuffer;
    private int m_ssaoKernel;
    private ComputeBuffer m_ssaoNoise;

    private ComputeBuffer m_ssaoSamples;

    private ComputeBuffer m_pointLightIndexCounter;
    /// <summary>
    /// 光源索引信息
    /// </summary>
    private ComputeBuffer m_lightIndexList;
    /// <summary>
    /// 光源排布信息
    /// </summary>
    private ComputeBuffer m_lightGrid;


    private int m_lightCount;
    private int m_clusterXCount;
    private int m_clusterYCount;

    public ForwardAddSRP(SRenderPipelineParam _params)
    {
        m_params = _params;
        m_clusterRenderingCS = Resources.Load<ComputeShader>("ClusterRendering");
        m_clusterLightCullingCS = Resources.Load<ComputeShader>("ClusterLightCulling");
        m_ssaoCS = Resources.Load<ComputeShader>("SSAO");

        m_clusterComputeKernel = m_clusterRenderingCS.FindKernel("ClusterCompute");
        m_lightCullingKernel = m_clusterLightCullingCS.FindKernel("ClusterLightCulling");
        m_ssaoKernel = m_ssaoCS.FindKernel("SSAO");
        createNoise();
        createRandRotate();
    }

    private void renderSSAO()
    {
        
        m_cameraBuffer.SetComputeTextureParam(m_ssaoCS, m_ssaoKernel, "g_normalTex", s_sceneNormalTextureId);
        m_cameraBuffer.SetComputeTextureParam(m_ssaoCS, m_ssaoKernel, "g_depthTex", s_sceneDepthTextureId);
        m_cameraBuffer.SetComputeBufferParam(m_ssaoCS, m_ssaoKernel, "g_noiseTex", m_ssaoNoise);

        m_cameraBuffer.SetComputeBufferParam(m_ssaoCS, m_ssaoKernel, "g_samples", m_ssaoSamples);
        m_cameraBuffer.SetComputeFloatParams(m_ssaoCS, "screenSize", new float[] { Screen.width, Screen.height, 1.0f / Screen.width, 1.0f / Screen.height });


        m_cameraBuffer.SetComputeFloatParams(m_ssaoCS, "ProjectionParams", new float[] { 1, m_camera.nearClipPlane, m_camera.farClipPlane, 1 / m_camera.farClipPlane });
        m_cameraBuffer.SetComputeMatrixParam(m_ssaoCS, "projectionMatrix", m_camera.projectionMatrix);
        m_cameraBuffer.SetComputeMatrixParam(m_ssaoCS, "invProjectionMatrix", m_camera.projectionMatrix.inverse);

        m_cameraBuffer.SetComputeTextureParam(m_ssaoCS, m_ssaoKernel, "g_destTex", s_tempTextureId);
        m_cameraBuffer.DispatchCompute(m_ssaoCS, m_ssaoKernel, Mathf.CeilToInt(Screen.width / 8), Mathf.CeilToInt(Screen.height / 8), 1);
        executeBuffer(m_cameraBuffer);
    }

    /// <summary>
    /// 构建相机群组
    /// </summary>
    /// <param name="_camera"></param>
    private void setupCameraClusters(Camera _camera)
    {
        float near = _camera.nearClipPlane;
        float far = _camera.farClipPlane;

        //根据屏幕大小划分屏幕
        int clusterXCount = Mathf.CeilToInt((float)Screen.width / m_clusterWidth);
        int clusterYCount = Mathf.CeilToInt((float)Screen.height / m_clusterHeight);

        m_clusterXYZCount = clusterXCount * clusterYCount * m_clusterZCount;

        //CS不为空时释放
        m_clusterBuffer?.Release();

        //computershader：SFrustum的大小
        m_clusterBuffer = new ComputeBuffer(m_clusterXYZCount, 72);

        Matrix4x4 projectionMatrix;
        projectionMatrix = GL.GetGPUProjectionMatrix(_camera.projectionMatrix, _camera.cameraType == CameraType.SceneView);

        var projectionMatrixInverse = projectionMatrix.inverse;

        //设置 ClusterRendering.computerShader 的参数
        m_cameraBuffer.SetComputeIntParams(m_clusterRenderingCS, "clusterCount", new int[] { clusterXCount, clusterYCount, m_clusterZCount });
        m_cameraBuffer.SetComputeIntParams(m_clusterRenderingCS, "clusterSize", new int[] { m_clusterWidth, m_clusterHeight });
        m_cameraBuffer.SetComputeFloatParams(m_clusterRenderingCS, "nearFarPlane", new float[] { near, far });
        m_cameraBuffer.SetComputeFloatParams(m_clusterRenderingCS, "screenSize", new float[] { Screen.width, Screen.height, 1.0f / Screen.width, 1.0f / Screen.height });
        m_cameraBuffer.SetComputeMatrixParam(m_clusterRenderingCS, "inverseProjectionMatrix", projectionMatrixInverse);

        int threadGroupCountX = Mathf.CeilToInt((float)clusterXCount / 16);
        int threadGroupCountY = Mathf.CeilToInt((float)clusterYCount / 16);
        int threadGroupCountZ = Mathf.CeilToInt((float)m_clusterZCount / 16);

        //设置输出Buffer
        m_cameraBuffer.SetComputeBufferParam(m_clusterRenderingCS, m_clusterComputeKernel, "g_clusters", m_clusterBuffer);
        //执行computershader, 根据划分块设置Group数量
        m_cameraBuffer.DispatchCompute(m_clusterRenderingCS, m_clusterComputeKernel, threadGroupCountX, threadGroupCountY, threadGroupCountZ);
    }

    /// <summary>
    /// 将灯光信息保存到缓存中
    /// </summary>
    /// <param name="cullingResults"></param>
    private void updateLightBuffer(CullingResults _cullingResults)
    {
        m_lightBuffer?.Release();
        //设置光源BUFFER
        m_lightBuffer = new ComputeBuffer(m_maxLightCount, System.Runtime.InteropServices.Marshal.SizeOf<SLightSource>());

        List<SLightSource> lightPosRadius = new List<SLightSource>();

        int count = _cullingResults.visibleLights == null ? 0 : _cullingResults.visibleLights.Length;
        //保存场景中可见灯光信息
        for (int i = 0; i < count; ++i)
        {
            var light = _cullingResults.visibleLights[i];
            if (light.light.enabled)
            {
                SLightSource l = new SLightSource();
                l.position = light.light.transform.position;

                if (light.lightType == LightType.Directional || light.lightType == LightType.Spot)
                {
                    l.direction = light.light.transform.forward;
                    l.direction = light.light.transform.localToWorldMatrix.GetColumn(2);
                }

                l.color = light.finalColor;
                if (light.lightType == LightType.Spot)
                    l.spotAngle = light.spotAngle;

                l.range = light.range;
                l.type = (uint)light.lightType;
                lightPosRadius.Add(l);
            }
        }

        m_lightBuffer.SetData(lightPosRadius);
        m_lightCount = lightPosRadius.Count;
    }

    /// <summary>
    /// 设置灯光信息
    /// </summary>
    /// <param name="_camera"></param>
    private void lightCulling(Camera _camera)
    {
        m_pointLightIndexCounter?.Release();
        m_lightIndexList?.Release();
        m_lightGrid?.Release();

        m_pointLightIndexCounter = new ComputeBuffer(1, sizeof(uint));
        m_pointLightIndexCounter.SetData(new uint[] { 0 });
        m_lightIndexList = new ComputeBuffer(1024 * m_clusterXYZCount, sizeof(uint));
        m_lightGrid = new ComputeBuffer(m_clusterXYZCount, sizeof(uint) * 2);

        //设置LightCulling.computershader输出
        m_cameraBuffer.SetComputeBufferParam(m_clusterLightCullingCS, m_lightCullingKernel, "g_lights", m_lightBuffer);
        m_cameraBuffer.SetComputeBufferParam(m_clusterLightCullingCS, m_lightCullingKernel, "g_clusters", m_clusterBuffer);
        m_cameraBuffer.SetComputeBufferParam(m_clusterLightCullingCS, m_lightCullingKernel, "g_pointLightIndexCounter", m_pointLightIndexCounter);
        m_cameraBuffer.SetComputeBufferParam(m_clusterLightCullingCS, m_lightCullingKernel, "g_lightIndexList", m_lightIndexList);
        m_cameraBuffer.SetComputeBufferParam(m_clusterLightCullingCS, m_lightCullingKernel, "g_lightGrid", m_lightGrid);
        m_cameraBuffer.SetComputeMatrixParam(m_clusterLightCullingCS, "_CameraViewMatrix", _camera.transform.worldToLocalMatrix);
        m_cameraBuffer.SetComputeIntParam(m_clusterLightCullingCS, "g_lightCount", m_lightCount);

        m_cameraBuffer.SetComputeIntParams(m_clusterRenderingCS, "clusterCount", new int[] { m_clusterXCount, m_clusterYCount, m_clusterZCount });
        m_cameraBuffer.DispatchCompute(m_clusterLightCullingCS, m_lightCullingKernel, m_clusterXYZCount, 1, 1);
    }

    private void debugClusterRendering()
    {
        Material debugMat = Resources.Load<Material>("mt_debugClusterMat");
        debugMat.SetBuffer("ClusterAABBs", m_clusterBuffer);
        debugMat.SetMatrix("_CameraWorldMatrix", Camera.main.transform.localToWorldMatrix);
        m_cameraBuffer.DrawProcedural(Matrix4x4.identity, debugMat, 0, MeshTopology.Points, m_clusterXYZCount);
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

    private static int s_sceneNormalTextureId = Shader.PropertyToID("_SceneNormalTexture");
    private static int s_sceneDepthTextureId = Shader.PropertyToID("_SceneDepthTexture");
    private static int s_colorTextureId = Shader.PropertyToID("_CameraColorTexture");
    private static int s_tempTextureId = Shader.PropertyToID("_CameraTempTexture");


    private void setUpRenderTexture()
    {
        m_cameraBuffer.GetTemporaryRT(s_sceneNormalTextureId, m_camera.pixelWidth, m_camera.pixelHeight, 16, FilterMode.Bilinear, RenderTextureFormat.ARGB2101010,
            RenderTextureReadWrite.sRGB, 1, true);

        m_cameraBuffer.GetTemporaryRT(s_sceneDepthTextureId, m_camera.pixelWidth, m_camera.pixelHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Depth,
            RenderTextureReadWrite.Default, 1);

        m_cameraBuffer.GetTemporaryRT(s_colorTextureId, m_camera.pixelWidth, m_camera.pixelHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Default,
            RenderTextureReadWrite.Default, 1);

        m_cameraBuffer.GetTemporaryRT(s_tempTextureId, m_camera.pixelWidth, m_camera.pixelHeight, 16, FilterMode.Bilinear, RenderTextureFormat.Default,
            RenderTextureReadWrite.Default, 1, true);
    }

    private void clearRenderTexture()
    {
        m_cameraBuffer.ReleaseTemporaryRT(s_colorTextureId);
        m_cameraBuffer.ReleaseTemporaryRT(s_sceneDepthTextureId);
        m_cameraBuffer.ReleaseTemporaryRT(s_sceneNormalTextureId);
        m_cameraBuffer.ReleaseTemporaryRT(s_tempTextureId);
    }

    private void renderTexture()
    {
        m_cameraBuffer.BeginSample("SceneInfo");

        var sortSetting = new SortingSettings(m_camera)
        {
            criteria = SortingCriteria.QuantizedFrontToBack,
        };

        var sceneInfoDrawSetting = new DrawingSettings(new ShaderTagId("SceneInfo"), sortSetting);
        var sceneInfoFilterSettings = new FilteringSettings(RenderQueueRange.opaque);
        m_cameraBuffer.SetRenderTarget(s_sceneNormalTextureId, s_sceneDepthTextureId);

        m_cameraBuffer.ClearRenderTarget(true, true, Color.black);

        executeBuffer(m_cameraBuffer);

        m_context.DrawRenderers(m_cullingResults, ref sceneInfoDrawSetting, ref sceneInfoFilterSettings);

        m_cameraBuffer.EndSample("SceneInfo");
    }

    private void createNoise()
    {
        m_ssaoSamples?.Release();
        m_ssaoSamples = new ComputeBuffer(64, 16);

        float r = Random.Range(0, 1);
        List<Vector4> vectors = new List<Vector4>();
        for (int i = 0; i < 64; ++i)
        {
            //使其在法线半球随机采样
            float r1 = Random.Range(0.0f, 1.0f) * 2 - 1;
            float r2 = Random.Range(0.0f, 1.0f) * 2 - 1;
            float r3 = Random.Range(0.0f, 1.0f);

            Vector4 sample = new Vector4(r1, r2, r3, 0.0f);

            sample = sample.normalized;
            sample *= Random.Range(0.0f, 1.0f);
            float scale = (float)i / 64.0f;
            scale = Mathf.Lerp(0.1f, 1.0f, scale * scale);
            sample *= scale;
            vectors.Add(sample);
        }
        m_ssaoSamples.SetData<Vector4>(vectors);
    }

    private void createRandRotate()
    {
        m_ssaoNoise?.Release();
        m_ssaoNoise = new ComputeBuffer(16, 12);
        List<Vector3> vectors = new List<Vector3>();
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                float rX = Random.Range(0.0f, 1.0f) * 2 - 1;
                float rY = Random.Range(0.0f, 1.0f) * 2 - 1;
                Vector3 noise = new Vector3(rX, rY, 0);
                vectors.Add(noise);
            }
        }

        m_ssaoNoise.SetData<Vector3>(vectors);
    }

    /// <summary>
    /// 渲染主函数
    /// </summary>
    /// <param name="_context"></param>
    /// <param name="_camera"></param>
    public override void Render(ScriptableRenderContext _context, Camera _camera)
    {
        this.m_context = _context;
        this.m_camera = _camera;

        //进行相机剔除
        ScriptableCullingParameters cullingParameters;
        if (!_camera.TryGetCullingParameters(false, out cullingParameters))
            return;

        //设置灯光为线性空间
        GraphicsSettings.lightsUseLinearIntensity = true;

        //设置相机属性
        m_context.SetupCameraProperties(_camera);
        CameraClearFlags clearFlags = m_camera.clearFlags;

        //处理在场景视窗中的渲染
        prepareForSceneWindow();

        //进行相机剔除
        m_cullingResults = m_context.Cull(ref cullingParameters);

        //设置相机群组的信息
        setupCameraClusters(_camera);
        //更新灯光信息
        updateLightBuffer(m_cullingResults);
        //设置灯光剔除
        lightCulling(_camera);

        setUpRenderTexture();
        renderTexture();

        renderSSAO();

        m_clusterXCount = Mathf.CeilToInt((float)Screen.width / m_clusterWidth);
        m_clusterYCount = Mathf.CeilToInt((float)Screen.height / m_clusterHeight);

        m_cameraBuffer.SetGlobalBuffer("g_lights", m_lightBuffer);
        m_cameraBuffer.SetGlobalBuffer("g_lightIndexList", m_lightIndexList);
        m_cameraBuffer.SetGlobalBuffer("g_lightGrid", m_lightGrid);
        m_cameraBuffer.SetGlobalVector("clusterSize", new Vector2(m_clusterWidth, (float)m_clusterHeight));
        m_cameraBuffer.SetGlobalVector("cb_clusterCount", new Vector3(m_clusterXCount, m_clusterYCount, m_clusterZCount));
        m_cameraBuffer.SetGlobalVector("cb_clusterSize", new Vector3(m_clusterWidth, m_clusterHeight, Mathf.CeilToInt((_camera.farClipPlane - _camera.nearClipPlane) / m_clusterZCount)));
        m_cameraBuffer.SetGlobalVector("cb_screenSize", new Vector4(Screen.width, Screen.height, 1.0f / Screen.width, 1.0f / Screen.height));

        m_context.ExecuteCommandBuffer(m_cameraBuffer);
        m_cameraBuffer.Clear();

        m_cameraBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        //清除相机缓存
        m_cameraBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0, (clearFlags & CameraClearFlags.Color) != 0, _camera.backgroundColor, 1.0f);
        executeBuffer(m_cameraBuffer);

        //设置绘制设置
        var drawSettings = new DrawingSettings(new ShaderTagId("ForwardAddLit"), new SortingSettings(_camera)
        {
            criteria = SortingCriteria.QuantizedFrontToBack,
        });


        //绘制不透明物体
        var filterSetting = new FilteringSettings(RenderQueueRange.opaque);
        m_context.DrawRenderers(m_cullingResults, ref drawSettings, ref filterSetting);

        //绘制天空盒
        m_context.DrawSkybox(_camera);

        //绘制透明物体
        filterSetting.renderQueueRange = RenderQueueRange.transparent;
        m_context.DrawRenderers(m_cullingResults, ref drawSettings, ref filterSetting);

        drawGizmos();

        clearRenderTexture();
        submit();
    }

}
