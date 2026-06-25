using System;
using System.Collections.Generic;
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

        [Header("Visible Subset Filter")]
        [SerializeField]
        private float maxViewDistance = 8.0f;

        [SerializeField]
        private float viewportMargin = 0.15f;

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

        [ContextMenu("Resort Procedural Gaussians")]
        public void ResortProceduralGaussians()
        {
            if (_gaussians == null || _gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data loaded.", this);
                return;
            }

            if (proceduralRenderer == null)
            {
                Debug.LogError("ProceduralRenderer reference is missing.", this);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("Main Camera not found.", this);
                return;
            }

            Debug.Log("Sorting gaussians by current camera depth...", this);
            int[] sortedIndices = BuildDepthSortedIndices(_gaussians, cam,
                proceduralRenderer.transform.localToWorldMatrix);

            Debug.Log("Rebuilding procedural chunks with sorted indices...", this);
            proceduralRenderer.UpdateIndices(sortedIndices);

            Debug.Log("Resort complete.", this);
        }

        [ContextMenu("Reset Procedural Indices")]
        public void ResetProceduralIndices()
        {
            if (proceduralRenderer == null)
            {
                Debug.LogError("ProceduralRenderer reference is missing.", this);
                return;
            }

            proceduralRenderer.ResetIdentityIndices();
            Debug.Log("Procedural indices reset to identity.", this);
        }

        [ContextMenu("Resort Procedural Gaussians (GPU Visible Subset + CPU Sort)")]
        public void ResortProceduralGaussiansGpuVisibleSubsetCpuSort()
        {
            if (_gaussians == null || _gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data loaded.", this);
                return;
            }

            if (proceduralRenderer == null)
            {
                Debug.LogError("ProceduralRenderer reference is missing.", this);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("Main Camera not found.", this);
                return;
            }

            Debug.Log("Generating GPU depth keys with visible flags...", this);
            proceduralRenderer.GenerateDepthKeys(cam);

            Debug.Log("Reading back depth keys...", this);
            GaussianDepthKey[] keys = proceduralRenderer.ReadBackDepthKeys();
            if (keys == null || keys.Length == 0)
            {
                Debug.LogWarning("No depth keys read back.", this);
                return;
            }

            List<GaussianDepthKey> visible = new List<GaussianDepthKey>(keys.Length / 4);
            List<GaussianDepthKey> invisible = new List<GaussianDepthKey>(keys.Length);

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].Visible != 0)
                {
                    visible.Add(keys[i]);
                }
                else
                {
                    invisible.Add(keys[i]);
                }
            }

            Debug.Log($"Visible subset count: {visible.Count} / {keys.Length}", this);

            visible.Sort((a, b) =>
            {
                // 远 -> 近
                return b.Depth.CompareTo(a.Depth);
            });

            int[] sortedIndices = new int[keys.Length];
            int cursor = 0;

            for (int i = 0; i < visible.Count; i++)
            {
                sortedIndices[cursor++] = (int)visible[i].Index;
            }

            for (int i = 0; i < invisible.Count; i++)
            {
                sortedIndices[cursor++] = (int)invisible[i].Index;
            }

            Debug.Log("Updating index buffer...", this);
            proceduralRenderer.UpdateIndices(sortedIndices, visible.Count);

            int previewCount = Mathf.Min(10, visible.Count);
            for (int i = 0; i < previewCount; i++)
            {
                Debug.Log($"VisibleSortedKey[{i}] => depth={visible[i].Depth}, index={visible[i].Index}", this);
            }

            Debug.Log("GPU visible subset + CPU sort resort complete.", this);
        }

        [ContextMenu("Resort Procedural Gaussians (GPU Compacted Visible + CPU Sort)")]
        public void ResortProceduralGaussiansGpuCompactedVisibleCpuSort()
        {
            if (_gaussians == null || _gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data loaded.", this);
                return;
            }

            if (proceduralRenderer == null)
            {
                Debug.LogError("ProceduralRenderer reference is missing.", this);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("Main Camera not found.", this);
                return;
            }

            Debug.Log("Generating GPU depth keys with compacted visible output...", this);
            proceduralRenderer.GenerateDepthKeys(cam);

            Debug.Log("Reading back compacted visible depth keys...", this);
            GaussianDepthKey[] visibleKeys = proceduralRenderer.ReadBackVisibleDepthKeys(out int visibleCount);
            if (visibleKeys == null)
            {
                Debug.LogWarning("Visible depth key readback returned null.", this);
                return;
            }

            Debug.Log($"Visible compacted subset count: {visibleCount} / {_gaussians.Length}", this);

            if (visibleCount == 0)
            {
                Debug.LogWarning("Visible compacted subset is empty.", this);
                return;
            }

            Debug.Log("Sorting compacted visible keys on CPU...", this);
            Array.Sort(visibleKeys, (a, b) =>
            {
                // 远 -> 近
                return b.Depth.CompareTo(a.Depth);
            });

            int[] visibleSortedIndices = new int[visibleCount];
            for (int i = 0; i < visibleCount; i++)
            {
                visibleSortedIndices[i] = (int)visibleKeys[i].Index;
            }

            Debug.Log("Updating visible prefix indices...", this);
            proceduralRenderer.UpdateVisiblePrefixIndices(visibleSortedIndices);

            int previewCount = Mathf.Min(10, visibleCount);
            for (int i = 0; i < previewCount; i++)
            {
                Debug.Log($"CompactedVisibleKey[{i}] => depth={visibleKeys[i].Depth}, index={visibleKeys[i].Index}", this);
            }

            Debug.Log("GPU compacted visible + CPU sort resort complete.", this);
        }

        private static int[] BuildDepthSortedIndices(
            GaussianData[] gaussians,
            Camera camera,
            Matrix4x4 localToWorld)
        {
            int count = gaussians.Length;
            int[] sortedIndices = new int[count];

            for (int i = 0; i < count; i++)
            {
                sortedIndices[i] = i;
            }

            Transform camTransform = camera.transform;
            Vector3 camPosition = camTransform.position;
            Vector3 camForward = camTransform.forward;

            Array.Sort(sortedIndices, (a, b) =>
            {
                Vector3 worldA = localToWorld.MultiplyPoint3x4(gaussians[a].Position);
                Vector3 worldB = localToWorld.MultiplyPoint3x4(gaussians[b].Position);

                float depthA = Vector3.Dot(worldA - camPosition, camForward);
                float depthB = Vector3.Dot(worldB - camPosition, camForward);

                // 远 -> 近
                return depthB.CompareTo(depthA);
            });

            return sortedIndices;
        }

        //[ContextMenu("Resort Visible Subset Procedural Gaussians")]
        //public void ResortVisibleSubsetProceduralGaussians()
        //{
        //    if (_gaussians == null || _gaussians.Length == 0)
        //    {
        //        Debug.LogWarning("No gaussian data loaded.", this);
        //        return;
        //    }

        //    if (proceduralRenderer == null)
        //    {
        //        Debug.LogError("ProceduralRenderer reference is missing.", this);
        //        return;
        //    }

        //    Camera cam = Camera.main;
        //    if (cam == null)
        //    {
        //        Debug.LogError("Main Camera not found.", this);
        //        return;
        //    }

        //    Debug.Log("Filtering visible subset...", this);

        //    int[] visibleIndices = BuildVisibleSubsetIndices(
        //        _gaussians,
        //        cam,
        //        proceduralRenderer.transform.localToWorldMatrix,
        //        maxViewDistance,
        //        viewportMargin
        //    );

        //    Debug.Log($"Visible subset count: {visibleIndices.Length}", this);

        //    if (visibleIndices.Length == 0)
        //    {
        //        Debug.LogWarning("Visible subset is empty.", this);
        //        return;
        //    }

        //    Debug.Log("Sorting visible subset by depth...", this);
        //    SortIndicesByDepth(_gaussians, visibleIndices, cam);

        //    Debug.Log("Rebuilding procedural chunks with visible sorted subset...", this);
        //    proceduralRenderer.Build(_gaussians, visibleIndices);

        //    Debug.Log("Visible subset resort complete.", this);
        //}

        //private static void SortIndicesByDepth(GaussianData[] gaussians, int[] indices, Camera camera)
        //{
        //    Transform camTransform = camera.transform;
        //    Vector3 camPosition = camTransform.position;
        //    Vector3 camForward = camTransform.forward;

        //    Array.Sort(indices, (a, b) =>
        //    {
        //        float depthA = Vector3.Dot(gaussians[a].Position - camPosition, camForward);
        //        float depthB = Vector3.Dot(gaussians[b].Position - camPosition, camForward);

        //        // 远 -> 近
        //        return depthB.CompareTo(depthA);
        //    });
        //}

        //private static int[] BuildVisibleSubsetIndices(
        //    GaussianData[] gaussians,
        //    Camera camera,
        //    Matrix4x4 localToWorld,
        //    float maxViewDistance,
        //    float viewportMargin)
        //{
        //    List<int> visible = new List<int>(gaussians.Length / 4);

        //    Transform camTransform = camera.transform;
        //    Vector3 camPosition = camTransform.position;
        //    Vector3 camForward = camTransform.forward;
        //    float maxDistSqr = maxViewDistance * maxViewDistance;

        //    float minViewport = -viewportMargin;
        //    float maxViewport = 1f + viewportMargin;

        //    for (int i = 0; i < gaussians.Length; i++)
        //    {
        //        Vector3 worldPos = localToWorld.MultiplyPoint3x4(gaussians[i].Position);

        //        Vector3 toPoint = worldPos - camPosition;

        //        // 1. 在相机前方
        //        float forwardDepth = Vector3.Dot(toPoint, camForward);
        //        if (forwardDepth <= 0f)
        //        {
        //            continue;
        //        }

        //        // 2. 距离不过远
        //        if (toPoint.sqrMagnitude > maxDistSqr)
        //        {
        //            continue;
        //        }

        //        // 3. 在视口附近
        //        Vector3 viewport = camera.WorldToViewportPoint(worldPos);

        //        if (viewport.z <= 0f)
        //        {
        //            continue;
        //        }

        //        if (viewport.x < minViewport || viewport.x > maxViewport ||
        //            viewport.y < minViewport || viewport.y > maxViewport)
        //        {
        //            continue;
        //        }

        //        visible.Add(i);
        //    }

        //    return visible.ToArray();
        //}
    }
}
