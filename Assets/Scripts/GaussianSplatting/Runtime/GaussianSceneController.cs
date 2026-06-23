using System;
using System.Text;
using GaussianSplatting.Core;
using GaussianSplatting.IO;
using GaussianSplatting.Rendering;
using UnityEngine;

namespace GaussianSplatting.Runtime
{
    public sealed class GaussianSceneController : MonoBehaviour
    {
        [Header("PLY Source")]
        [SerializeField]
        private string plyFilePath = @"D:\3DGS\Unity3DGS\models\bicycle\point_cloud\iteration_30000\point_cloud.ply";

        [Header("Scene References")]
        [SerializeField]
        private GaussianProceduralRenderer proceduralRenderer;

        private GaussianData[] _gaussians;
        //private int[] _sortedIndices;

        private void Start()
        {
            LoadAndRenderProcedural();
        }

        [ContextMenu("Load And Render Procedural")]
        public void LoadAndRenderProcedural()
        {
            try
            {
                Debug.Log("Reading gaussian data...", this);

                _gaussians = GaussianPlyReader.ReadFirstVertices(
                    plyFilePath,
                    out PlyHeader header
                );

                Debug.Log(
                    $"Read complete.\n" +
                    $"VertexCount: {_gaussians.Length}\n" +
                    $"HeaderByteSize: {header.HeaderByteSize}\n" +
                    $"VertexStrideBytes: {header.GetVertexStrideBytes()}",
                    this
                );

                int previewCount = Mathf.Min(5, _gaussians.Length);
                for (int i = 0; i < previewCount; i++)
                {
                    Debug.Log($"--- Vertex [{i}] ---\n{_gaussians[i]}", this);
                }

                if (proceduralRenderer == null)
                {
                    Debug.LogError("ProceduralRenderer reference is missing.", this);
                    return;
                }

                Debug.Log("Uploading gaussian buffer...", this);
                proceduralRenderer.Build(_gaussians);
                Debug.Log("Procedural renderer ready.", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load procedural gaussian render.\nPath: {plyFilePath}\nError: {ex}", this);
            }
        }
    }
}
