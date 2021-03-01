/*****************************************************************************
*                                                                            *
*  @file     CustomRenderPipelineAssetEditor.cs                              *
*  @brief    扩展级联阴影在编辑器上的显示                                    *
*  Details   设置级联区域                                                    *
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
using UnityEditor.Experimental.Rendering;
using UnityEditor.Rendering;

[CustomEditor(typeof(CustomRenderPipelineAsset))]
public class CustomRenderPipelineAssetEditor : Editor
{
    private SerializedProperty m_shadowCascades;
    private SerializedProperty m_twoCascadesSplit;
    private SerializedProperty m_fourCascadesSplit;

    private void OnEnable()
    {
        m_shadowCascades = serializedObject.FindProperty("m_shadowCascades");
        m_twoCascadesSplit = serializedObject.FindProperty("m_twoCascadesSplit");
        m_fourCascadesSplit = serializedObject.FindProperty("m_fourCascadesSplit");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();


        switch (m_shadowCascades.enumValueIndex)
        {
            case 0: return;
            case 1:
                EditorUtils.DrawCascadeSplitGUI<float>(ref m_twoCascadesSplit);
                break;
            case 2:
                EditorUtils.DrawCascadeSplitGUI<Vector3>(ref m_fourCascadesSplit);
                break;
        }
        serializedObject.ApplyModifiedProperties();
    }
}

