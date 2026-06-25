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

        private ComputeBuffer _gaussianBuffer;
        private ComputeBuffer _indexBuffer;
        private int _gaussianCount;


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

        public void UpdateIndices(int[] sortedIndices)
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
        }

        private void OnRenderObject()
        {
            if (_gaussianBuffer == null || _indexBuffer == null || _gaussianCount == 0 || proceduralMaterial == null)
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
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, _gaussianCount);
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

            _gaussianCount = 0;
        }
    }
}