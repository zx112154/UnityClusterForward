using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public ComputeShader m_shader;

    struct SData
    {
        public float A;
        public float B;
        public float C;
    }

    void Start()
    {
        SData[] inputData = new SData[3];
        SData[] outputData = new SData[3];

        Debug.Log("输入------------------------");
        for (int i = 0; i < inputData.Length; i++)
        {
            inputData[i].A = i * 3 + 1;
            inputData[i].B = i * 3 + 2;
            inputData[i].C = i * 3 + 3;
            Debug.Log(inputData[i].A + "," + inputData[i].B + "," + inputData[i].C);
        }

        ComputeBuffer inputBuffer = new ComputeBuffer(outputData.Length, 12);
        ComputeBuffer outputBuffer = new ComputeBuffer(outputData.Length, 12);

        int k = m_shader.FindKernel("CSMain");

        //写入GPU
        inputBuffer.SetData(inputData);
        m_shader.SetBuffer(k, "inputData", inputBuffer);

        //计算，并输出到GPU
        m_shader.SetBuffer(k, "outputData", outputBuffer);
        m_shader.Dispatch(k, outputData.Length, 1, 1);
        outputBuffer.GetData(outputData);

        Debug.Log("输出------------------------");

        for (int i = 0; i < outputData.Length; i++)
        {
            Debug.Log(outputData[i].A + "," + outputData[i].B + "," + outputData[i].C);
        }

        //释放
        inputBuffer.Dispose();
        outputBuffer.Dispose();
    }

}
