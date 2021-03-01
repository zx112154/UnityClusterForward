/*****************************************************************************
*                                                                            *
*  @file     DoubleSidedMeshMenuItem.cs                                      *
*  @brief    创建双面渲染的网格模型                                          *
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

using UnityEditor;
using UnityEngine;

public static class DoubleSidedMeshMenuItem
{
    [MenuItem("Assets/Create/Double-Sided Mesh")]
    static void MakDoubleSideMeshAsset()
    {
        var sourceMesh = Selection.activeObject as Mesh;
        if (sourceMesh == null)
        {
            Debug.LogError("You must have a mesh asset selected.");
            return;
        }

        Mesh insideMesh = Object.Instantiate(sourceMesh);
        int[] triangles = insideMesh.triangles;
        System.Array.Reverse(triangles);
        insideMesh.triangles = triangles;

        insideMesh.triangles = triangles;
        Vector3[] normals = insideMesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = -normals[i];
        }
        insideMesh.normals = normals;

        var combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(
            new CombineInstance[] {
                new CombineInstance{ mesh = insideMesh},
                new CombineInstance{ mesh = insideMesh}
            },
            true, false, false
            );

        Object.DestroyImmediate(insideMesh);

        AssetDatabase.CreateAsset(combinedMesh, System.IO.Path.Combine("Assets", sourceMesh.name + " Double-Sided.asset"));
    }
}
