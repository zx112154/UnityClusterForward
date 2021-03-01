/*****************************************************************************
*                                                                            *
*  @file     MyPostProcessingStack.cs                                        *
*  @brief    后处理自定义渲染脚本                                            *
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

public struct SPostProcessingParam
{
    public CommandBuffer cb;
    public int cameraColorId;
    public int cameraDepthId;
    public int width;
    public int height;
    public int samples;
    public RenderTextureFormat format;
}

[CreateAssetMenu(menuName = "SRP-Demo/02-My Post-Processing Stack")]
public class MyPostProcessingStack : ScriptableObject
{
    enum EPass
    {
        Copy = 0,
        Blur = 1,
        DepthStripes = 2,
        ToneMapping = 3,
        Bright = 4,
        TexAdd = 5
    };

    [SerializeField, Range(0, 10)]
    private int m_blurStrength = 0;

    [SerializeField]
    private bool m_depthStripes = false;

    [SerializeField]
    private bool m_toneMapping = false;

    [SerializeField, Range(1.0f, 100.0f)]
    private float m_toneMappingRange = 100.0f;

    [SerializeField]
    private bool m_bloom = false;

    public bool needsDepth
    {
        get
        {
            return m_depthStripes;
        }
    }

    private static Mesh s_fullScreenTriangle;
    private static Material s_material;

    private static int s_tempTexId = Shader.PropertyToID("_MyPostProcessingStackTempTex");
    private static int s_mainTexId = Shader.PropertyToID("_MainTex");
    private static int s_depthTexId = Shader.PropertyToID("_DepthTex");
    private static int s_resolvedTexId = Shader.PropertyToID("_MyPostProcessingStackResolvedTex");

    private SPostProcessingParam m_param;

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

        s_material = new Material(Shader.Find("Hidden/My Pipeline/PostEffectStack"))
        {
            name = "My Post-Processing Stack material",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    public void RenderAfterOpaque(SPostProcessingParam param)
    {
        m_param = param;
        initializeStatic();
        if (m_depthStripes)
        {
            depthStripes();
        }
    }

    public void RenderAfterTransparent()
    {
        if (m_blurStrength > 0)
        {
            if (m_toneMapping || m_param.samples > 1)
            {
                m_param.cb.GetTemporaryRT(s_resolvedTexId, m_param.width, m_param.height, 0, FilterMode.Bilinear);

                if (m_toneMapping)
                    toneMapping(m_param.cameraColorId, s_resolvedTexId);
                else
                    blit(m_param.cameraColorId, s_resolvedTexId);

                blur(s_resolvedTexId, BuiltinRenderTextureType.CameraTarget);
                m_param.cb.ReleaseTemporaryRT(s_resolvedTexId);
            }
            else
            {
                blur(m_param.cameraColorId, BuiltinRenderTextureType.CameraTarget);
            }
        }
        else if (m_toneMapping)
        {
            toneMapping(m_param.cameraColorId, BuiltinRenderTextureType.CameraTarget);
        }
        else
            blit(m_param.cameraColorId, BuiltinRenderTextureType.CameraTarget);

        if (m_bloom)
            bloom(m_param.cameraColorId, BuiltinRenderTextureType.CameraTarget);
    }

    private void blur(RenderTargetIdentifier srcTex, RenderTargetIdentifier dstTex)
    {
        m_param.cb.BeginSample("Blur");

        m_param.cb.GetTemporaryRT(s_tempTexId, m_param.width, m_param.height, 0, FilterMode.Bilinear);
        int passesLeft;
        for (passesLeft = m_blurStrength; passesLeft > 2; passesLeft -= 2)
        {
            blit(srcTex, s_tempTexId, EPass.Blur);
            blit(s_tempTexId, srcTex, EPass.Blur);
        }

        if (passesLeft > 1)
        {
            blit(srcTex, s_tempTexId, EPass.Blur);
            blit(s_tempTexId, dstTex, EPass.Blur);
        }

        if (passesLeft == 1)
        {
            blit(srcTex, dstTex, EPass.Blur);
        }

        m_param.cb.ReleaseTemporaryRT(s_tempTexId);

        m_param.cb.EndSample("Blur");
    }

    private void blit(RenderTargetIdentifier sourceId, RenderTargetIdentifier destinationId, EPass pass = EPass.Copy)
    {
        m_param.cb.SetGlobalTexture(s_mainTexId, sourceId);
        m_param.cb.SetRenderTarget(destinationId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_param.cb.DrawMesh(s_fullScreenTriangle, Matrix4x4.identity, s_material, 0, (int)pass);
    }

    private void depthStripes()
    {
        m_param.cb.BeginSample("Depth Stripes");

        m_param.cb.GetTemporaryRT(s_tempTexId, m_param.width, m_param.height, 0, FilterMode.Point, m_param.format);

        m_param.cb.SetGlobalTexture(s_depthTexId, m_param.cameraDepthId);
        blit(m_param.cameraColorId, s_tempTexId, EPass.DepthStripes);
        blit(s_tempTexId, m_param.cameraColorId);

        m_param.cb.ReleaseTemporaryRT(s_tempTexId);

        m_param.cb.EndSample("Depth Stripes");
    }

    private void bloom(RenderTargetIdentifier sourceId, RenderTargetIdentifier destinationId)
    {
        //开始处理泛光
        m_param.cb.BeginSample("Bloom");

        m_param.cb.GetTemporaryRT(s_tempTexId, m_param.width, m_param.height, 0, FilterMode.Bilinear);

        //提取亮光颜色
        blit(sourceId, s_tempTexId, EPass.Bright);

        blit(s_tempTexId, destinationId, EPass.Blur);

        m_param.cb.SetGlobalFloat("_ReinhardModifier", 1.0f / (m_toneMappingRange * m_toneMappingRange));
        m_param.cb.SetGlobalTexture("_BrightTex", s_tempTexId);
        blit(sourceId, destinationId, EPass.TexAdd);

        m_param.cb.ReleaseTemporaryRT(s_tempTexId);

        m_param.cb.EndSample("Bloom");
    }

    private void toneMapping(RenderTargetIdentifier sourceId, RenderTargetIdentifier destinationId)
    {
        m_param.cb.BeginSample("Tone Mapping");
        m_param.cb.SetGlobalFloat("_ReinhardModifier", 1.0f / (m_toneMappingRange * m_toneMappingRange));

        blit(sourceId, destinationId, EPass.ToneMapping);
        m_param.cb.EndSample("Tone Mapping");
    }

}
