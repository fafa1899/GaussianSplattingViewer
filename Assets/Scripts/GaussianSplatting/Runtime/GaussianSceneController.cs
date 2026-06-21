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
        private GaussianBillboardRenderer billboardRenderer;

        private GaussianData[] _gaussians;
        private int[] _sortedIndices;

        private void Start()
        {
            LoadAndRenderPointCloud();
        }

        public void LoadAndRenderPointCloud()
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

                RebuildBillboards();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to load and render point cloud.\nPath: {plyFilePath}\nError: {ex}", this);
            }
        }

        private void RebuildBillboards()
        {
            if (billboardRenderer == null)
            {
                Debug.LogError("BillboardRenderer reference is missing.", this);
                return;
            }

            Debug.Log("Sorting gaussians by camera depth...", this);
            _sortedIndices = BuildDepthSortedIndices(_gaussians, Camera.main);
            Debug.Log("Depth sort complete. Building billboard mesh...", this);
            billboardRenderer.Build(_gaussians, _sortedIndices);
        }

        private static int[] BuildDepthSortedIndices(GaussianData[] gaussians, Camera camera)
        {
            int count = gaussians.Length;
            int[] sortedIndices = new int[count];

            for (int i = 0; i < count; i++)
            {
                sortedIndices[i] = i;
            }

            if (camera == null)
            {
                return sortedIndices;
            }

            Transform camTransform = camera.transform;
            Vector3 camPosition = camTransform.position;
            Vector3 camForward = camTransform.forward;

            Array.Sort(sortedIndices, (a, b) =>
            {
                float depthA = Vector3.Dot(gaussians[a].Position - camPosition, camForward);
                float depthB = Vector3.Dot(gaussians[b].Position - camPosition, camForward);

                // 远 -> 近
                return depthB.CompareTo(depthA);
            });

            return sortedIndices;
        }

        [ContextMenu("Resort And Rebuild Billboards")]
        public void ResortAndRebuildBillboards()
        {
            if (_gaussians == null || _gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data loaded.", this);
                return;
            }

            RebuildBillboards();
        }
    }
}
