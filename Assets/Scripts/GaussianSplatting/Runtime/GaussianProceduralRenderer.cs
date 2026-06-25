using System;
using GaussianSplatting.Core;
using UnityEngine;

namespace GaussianSplatting.Rendering
{
    public sealed class GaussianProceduralRenderer : MonoBehaviour
    {

        [SerializeField]
        private Material proceduralMaterial;

        [SerializeField]
        private float scaleMultiplier = 1.5f;

        [SerializeField]
        private float minScale = 0.0005f;

        [SerializeField]
        private float maxScale = 0.05f;

        [Header("Gaussian Shape")]
        [SerializeField]
        private float sigmaExtent = 3.0f;

        [SerializeField]
        private float r2Cutoff = 9.0f;

        [SerializeField]
        private ComputeShader depthKeyCompute;

        private ComputeBuffer _gaussianBuffer;
        private ComputeBuffer _indexBuffer;
        private int _gaussianCount;

        private ComputeBuffer _depthKeyBuffer;

        private int _activeIndexCount;

        private ComputeBuffer _visibleDepthKeyBuffer;
        private ComputeBuffer _visibleCountReadbackBuffer;

        public void Build(GaussianData[] gaussians)
        {
            ReleaseBuffers();

            if (gaussians == null || gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data for procedural renderer.", this);
                return;
            }

            _gaussianCount = gaussians.Length;

            GaussianGpuData[] gpuData = new GaussianGpuData[_gaussianCount];
            uint[] identityIndices = new uint[_gaussianCount];


            for (int i = 0; i < _gaussianCount; i++)
            {
                GaussianData g = gaussians[i];

                Color baseColor = g.GetApproxColor();
                float alpha = g.GetOpacity();

                Vector3 scale = g.GetScale() * scaleMultiplier;
                scale.x = Mathf.Clamp(scale.x, minScale, maxScale);
                scale.y = Mathf.Clamp(scale.y, minScale, maxScale);
                scale.z = Mathf.Clamp(scale.z, minScale, maxScale);

                Quaternion q = Quaternion.Normalize(g.Rotation);

                gpuData[i] = new GaussianGpuData
                {
                    Position = g.Position,
                    Padding0 = 0f,
                    Scale = scale,
                    Padding1 = 0f,
                    Rotation = new Vector4(q.x, q.y, q.z, q.w),
                    Color = new Vector4(baseColor.r, baseColor.g, baseColor.b, alpha),

                    Sh01 = new Vector4(g.Sh01.x, g.Sh01.y, g.Sh01.z, 0f),
                    Sh02 = new Vector4(g.Sh02.x, g.Sh02.y, g.Sh02.z, 0f),
                    Sh03 = new Vector4(g.Sh03.x, g.Sh03.y, g.Sh03.z, 0f),
                    Sh04 = new Vector4(g.Sh04.x, g.Sh04.y, g.Sh04.z, 0f),
                    Sh05 = new Vector4(g.Sh05.x, g.Sh05.y, g.Sh05.z, 0f),
                    Sh06 = new Vector4(g.Sh06.x, g.Sh06.y, g.Sh06.z, 0f),
                    Sh07 = new Vector4(g.Sh07.x, g.Sh07.y, g.Sh07.z, 0f),
                    Sh08 = new Vector4(g.Sh08.x, g.Sh08.y, g.Sh08.z, 0f),
                    Sh09 = new Vector4(g.Sh09.x, g.Sh09.y, g.Sh09.z, 0f),
                    Sh10 = new Vector4(g.Sh10.x, g.Sh10.y, g.Sh10.z, 0f),
                    Sh11 = new Vector4(g.Sh11.x, g.Sh11.y, g.Sh11.z, 0f),
                    Sh12 = new Vector4(g.Sh12.x, g.Sh12.y, g.Sh12.z, 0f),
                    Sh13 = new Vector4(g.Sh13.x, g.Sh13.y, g.Sh13.z, 0f),
                    Sh14 = new Vector4(g.Sh14.x, g.Sh14.y, g.Sh14.z, 0f),
                    Sh15 = new Vector4(g.Sh15.x, g.Sh15.y, g.Sh15.z, 0f)
                };

                identityIndices[i] = (uint)i;
            }

            // 304 bytes = 16 + 16 + 16 + 16 + 15*16
            _gaussianBuffer = new ComputeBuffer(_gaussianCount, 304);
            _gaussianBuffer.SetData(gpuData);

            // uint index buffer, 4 bytes per index
            _indexBuffer = new ComputeBuffer(_gaussianCount, sizeof(uint));
            _indexBuffer.SetData(identityIndices);

            _depthKeyBuffer = new ComputeBuffer(_gaussianCount, 16);

            _visibleDepthKeyBuffer = new ComputeBuffer(_gaussianCount, 16, ComputeBufferType.Append);
            _visibleDepthKeyBuffer.SetCounterValue(0);

            // CopyCount 的目标缓冲，DX11 下用 Raw 最稳
            _visibleCountReadbackBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);

            _activeIndexCount = _gaussianCount;

            if (proceduralMaterial != null)
            {
                proceduralMaterial.SetBuffer("_Gaussians", _gaussianBuffer);
                proceduralMaterial.SetBuffer("_Indices", _indexBuffer);
                proceduralMaterial.SetFloat("_SigmaExtent", sigmaExtent);
                proceduralMaterial.SetFloat("_R2Cutoff", r2Cutoff);
            }

            Debug.Log(
                $"Procedural renderer built.\n" +
                $"Total Gaussians: {_gaussianCount}\n" +
                $"Gaussian Buffer Bytes: {(long)_gaussianCount * 304}\n" +
                $"Index Buffer Bytes: {(long)_gaussianCount * 4}",
                this
            );
        }

        public void UpdateIndices(int[] sortedIndices, int activeCount = -1)
        {
            if (_indexBuffer == null || _gaussianCount == 0)
            {
                Debug.LogWarning("Index buffer is not initialized.", this);
                return;
            }

            if (sortedIndices == null || sortedIndices.Length != _gaussianCount)
            {
                Debug.LogError("Sorted index array length mismatch.", this);
                return;
            }

            uint[] gpuIndices = new uint[_gaussianCount];
            for (int i = 0; i < _gaussianCount; i++)
            {
                gpuIndices[i] = (uint)sortedIndices[i];
            }

            _indexBuffer.SetData(gpuIndices);

            if (activeCount < 0)
            {
                _activeIndexCount = _gaussianCount;
            }
            else
            {
                _activeIndexCount = Mathf.Clamp(activeCount, 0, _gaussianCount);
            }
        }

        public void ResetIdentityIndices()
        {
            if (_indexBuffer == null || _gaussianCount == 0)
            {
                return;
            }

            uint[] identity = new uint[_gaussianCount];
            for (int i = 0; i < _gaussianCount; i++)
            {
                identity[i] = (uint)i;
            }

            _indexBuffer.SetData(identity);
            _activeIndexCount = _gaussianCount;
        }

        public void GenerateDepthKeys(Camera camera)
        {
            if (depthKeyCompute == null)
            {
                Debug.LogError("Depth key compute shader is missing.", this);
                return;
            }

            if (_gaussianBuffer == null || _depthKeyBuffer == null || _gaussianCount == 0)
            {
                Debug.LogWarning("Gaussian buffers are not initialized.", this);
                return;
            }

            if (camera == null)
            {
                Debug.LogError("Camera is null.", this);
                return;
            }

            int kernel = depthKeyCompute.FindKernel("CSMain");

            _visibleDepthKeyBuffer.SetCounterValue(0);

            depthKeyCompute.SetBuffer(kernel, "_Gaussians", _gaussianBuffer);
            depthKeyCompute.SetBuffer(kernel, "_DepthKeys", _depthKeyBuffer);
            depthKeyCompute.SetBuffer(kernel, "_VisibleDepthKeys", _visibleDepthKeyBuffer);
            depthKeyCompute.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
            depthKeyCompute.SetVector("_CameraPositionWS", camera.transform.position);
            depthKeyCompute.SetVector("_CameraForwardWS", camera.transform.forward);
            depthKeyCompute.SetInt("_GaussianCount", _gaussianCount);
            depthKeyCompute.SetMatrix("_ViewProj", camera.projectionMatrix * camera.worldToCameraMatrix);
            depthKeyCompute.SetFloat("_MaxViewDistance", 8.0f);
            depthKeyCompute.SetFloat("_ViewportMargin", 0.15f);

            int threadGroupCount = Mathf.CeilToInt(_gaussianCount / 256.0f);
            depthKeyCompute.Dispatch(kernel, threadGroupCount, 1, 1);
        }

        public GaussianDepthKey[] ReadBackVisibleDepthKeys(out int visibleCount)
        {
            visibleCount = 0;

            if (_visibleDepthKeyBuffer == null || _visibleCountReadbackBuffer == null || _gaussianCount == 0)
            {
                Debug.LogWarning("Visible depth key buffer is not initialized.", this);
                return null;
            }

            ComputeBuffer.CopyCount(_visibleDepthKeyBuffer, _visibleCountReadbackBuffer, 0);

            uint[] countData = new uint[1];
            _visibleCountReadbackBuffer.GetData(countData);
            visibleCount = (int)countData[0];

            if (visibleCount <= 0)
            {
                return Array.Empty<GaussianDepthKey>();
            }

            GaussianDepthKey[] result = new GaussianDepthKey[visibleCount];
            _visibleDepthKeyBuffer.GetData(result, 0, 0, visibleCount);
            return result;
        }

        public GaussianDepthKey[] ReadBackDepthKeys()
        {
            if (_depthKeyBuffer == null || _gaussianCount == 0)
            {
                Debug.LogWarning("Depth key buffer is not initialized.", this);
                return null;
            }

            GaussianDepthKey[] result = new GaussianDepthKey[_gaussianCount];
            _depthKeyBuffer.GetData(result);
            return result;
        }

        public void UpdateVisiblePrefixIndices(int[] visibleSortedIndices)
        {
            if (_indexBuffer == null || _gaussianCount == 0)
            {
                Debug.LogWarning("Index buffer is not initialized.", this);
                return;
            }

            if (visibleSortedIndices == null)
            {
                Debug.LogError("Visible sorted index array is null.", this);
                return;
            }

            int visibleCount = visibleSortedIndices.Length;
            if (visibleCount == 0)
            {
                _activeIndexCount = 0;
                return;
            }

            uint[] gpuIndices = new uint[visibleCount];
            for (int i = 0; i < visibleCount; i++)
            {
                gpuIndices[i] = (uint)visibleSortedIndices[i];
            }

            // 只更新 buffer 前 visibleCount 个元素
            _indexBuffer.SetData(gpuIndices, 0, 0, visibleCount);

            _activeIndexCount = Mathf.Clamp(visibleCount, 0, _gaussianCount);
        }

        private void OnRenderObject()
        {
            if (_gaussianBuffer == null || _indexBuffer == null || _activeIndexCount == 0 || proceduralMaterial == null)
            {
                return;
            }

            Camera currentCamera = Camera.main;
            if (currentCamera == null)
            {
                return;
            }


            proceduralMaterial.SetBuffer("_Gaussians", _gaussianBuffer);
            proceduralMaterial.SetBuffer("_Indices", _indexBuffer);
            proceduralMaterial.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
            proceduralMaterial.SetVector("_CameraRightWS", currentCamera.transform.right);
            proceduralMaterial.SetVector("_CameraUpWS", currentCamera.transform.up);
            proceduralMaterial.SetFloat("_SigmaExtent", sigmaExtent);
            proceduralMaterial.SetFloat("_R2Cutoff", r2Cutoff);

            proceduralMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, _activeIndexCount);
        }

        private void OnDisable()
        {
            ReleaseBuffers();
        }

        private void OnDestroy()
        {
            ReleaseBuffers();
        }

        private void ReleaseBuffers()
        {
            if (_gaussianBuffer != null)
            {
                _gaussianBuffer.Release();
                _gaussianBuffer = null;
            }

            if (_indexBuffer != null)
            {
                _indexBuffer.Release();
                _indexBuffer = null;
            }

            if (_depthKeyBuffer != null)
            {
                _depthKeyBuffer.Release();
                _depthKeyBuffer = null;
            }

            if (_visibleDepthKeyBuffer != null)
            {
                _visibleDepthKeyBuffer.Release();
                _visibleDepthKeyBuffer = null;
            }

            if (_visibleCountReadbackBuffer != null)
            {
                _visibleCountReadbackBuffer.Release();
                _visibleCountReadbackBuffer = null;
            }

            _gaussianCount = 0;
            _activeIndexCount = 0;
        }
    }
}