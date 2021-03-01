using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class InstancedMaterialProperties : MonoBehaviour
{
    [SerializeField]
    private Color m_color = Color.white;
    [SerializeField, Range(0.0f, 1.0f)]
    private float m_metallic = 0.0f;
    [SerializeField, Range(0, 1)]
    private float m_smoothness = 0.5f;

    private static MaterialPropertyBlock s_propertyBlock;

    static int s_colorId = Shader.PropertyToID("_Color");
    static int s_smoothnessId = Shader.PropertyToID("_Smoothness");
    static int s_metallicId = Shader.PropertyToID("_Metallic");

    private void Awake()
    {
        OnValidate();
    }

    private void OnValidate()
    {
        if (s_propertyBlock == null)
            s_propertyBlock = new MaterialPropertyBlock();
        s_propertyBlock.SetFloat(s_metallicId, m_metallic);
        s_propertyBlock.SetColor(s_colorId, m_color);
        s_propertyBlock.SetFloat(s_smoothnessId, m_smoothness);
        GetComponent<MeshRenderer>().SetPropertyBlock(s_propertyBlock);
    }

}
