/*****************************************************************************
*                                                                            *
*  @file     EditorUtils.cs                                                  *
*  @brief    Unity面板扩展19没有提供原先级联面板                             *
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

using System;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor;

public static class EditorUtils
{
    internal class Styles
    {
        public static float defaultLineSpace = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        public static float defaultIndentWidth = 12;
    }

    public static void DrawCascadeSplitGUI<T>(ref SerializedProperty shadowCascadeSplit)
    {
        float[] cascadePartitionSizes = null;
        Type type = typeof(T);
        if (type == typeof(float))
        {
            cascadePartitionSizes = new float[] { shadowCascadeSplit.floatValue };
        }
        else if (type == typeof(Vector3))
        {
            Vector3 splits = shadowCascadeSplit.vector3Value;
            cascadePartitionSizes = new float[]
                {
                    Mathf.Clamp(splits[0], 0.0f, 1.0f),
                    Mathf.Clamp(splits[1] - splits[0], 0.0f, 1.0f),
                    Mathf.Clamp(splits[2] - splits[1], 0.0f, 1.0f)
                };
        }
        if (cascadePartitionSizes != null)
        {
            EditorGUI.BeginChangeCheck();
            ShadowCascadeSplitGUI.HandleCascadeSliderGUI(ref cascadePartitionSizes);
            if (EditorGUI.EndChangeCheck())
            {
                if (type == typeof(float))
                    shadowCascadeSplit.floatValue = cascadePartitionSizes[0];
                else
                {
                    Vector3 updateValue = new Vector3();
                    updateValue[0] = cascadePartitionSizes[0];
                    updateValue[1] = updateValue[0] + cascadePartitionSizes[1];
                    updateValue[2] = updateValue[1] + cascadePartitionSizes[2];
                    shadowCascadeSplit.vector3Value = updateValue;
                }
            }
        }

    }
}

