using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ImageEffectAllowedInSceneView, RequireComponent(typeof(Camera))]
public class MyPipelineCamera : MonoBehaviour
{
    [SerializeField]
    MyPostProcessingStack m_postProcessingStack = null;

    public MyPostProcessingStack postProcessingStack
    {
        get
        {
            return m_postProcessingStack;
        }
    }

}
