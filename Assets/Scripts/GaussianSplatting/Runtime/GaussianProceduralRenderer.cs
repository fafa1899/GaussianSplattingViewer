using GaussianSplatting.Core;
using UnityEngine;

namespace GaussianSplatting.Rendering
{
    public sealed class GaussianProceduralRenderer : MonoBehaviour
    {
        [SerializeField]
        private Material proceduralMaterial;

        [SerializeField]
        private float radiusMultiplier = 1.5f;

        [SerializeField]
        private float minRadius = 0.001f;

        [SerializeField]
        private float maxRadius = 0.03f;

        private ComputeBuffer _gaussianBuffer;
        private int _gaussianCount;
        private Bounds _bounds;

        public void Build(GaussianData[] gaussians)
        {
            ReleaseBuffer();

            if (gaussians == null || gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data for procedural renderer.", this);
                return;
            }

            GaussianGpuData[] gpuData = new GaussianGpuData[gaussians.Length];

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < gaussians.Length; i++)
            {
                GaussianData g = gaussians[i];

                Color baseColor = g.GetApproxColor();
                float alpha = g.GetOpacity();

                Vector3 scale = g.GetScale();
                float radius = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * radiusMultiplier;
                radius = Mathf.Clamp(radius, minRadius, maxRadius);

                gpuData[i] = new GaussianGpuData
                {
                    Position = g.Position,
                    Radius = radius,
                    Color = new Vector4(baseColor.r, baseColor.g, baseColor.b, alpha)
                };

                min = Vector3.Min(min, g.Position);
                max = Vector3.Max(max, g.Position);
            }

            _gaussianCount = gpuData.Length;
            _gaussianBuffer = new ComputeBuffer(_gaussianCount, 32);
            _gaussianBuffer.SetData(gpuData);

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            size += Vector3.one * 2f;
            _bounds = new Bounds(center, size);

            if (proceduralMaterial != null)
            {
                proceduralMaterial.SetBuffer("_Gaussians", _gaussianBuffer);
            }
        }

        private void OnRenderObject()
        {
            if (_gaussianBuffer == null || _gaussianCount == 0 || proceduralMaterial == null)
            {
                return;
            }

            Camera currentCamera = Camera.main;
            if (currentCamera == null)
            {
                return;
            }

            proceduralMaterial.SetBuffer("_Gaussians", _gaussianBuffer);
            proceduralMaterial.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
            proceduralMaterial.SetVector("_CameraRightWS", currentCamera.transform.right);
            proceduralMaterial.SetVector("_CameraUpWS", currentCamera.transform.up);

            proceduralMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, _gaussianCount);
        }

        private void OnDisable()
        {
            ReleaseBuffer();
        }

        private void OnDestroy()
        {
            ReleaseBuffer();
        }

        private void ReleaseBuffer()
        {
            if (_gaussianBuffer != null)
            {
                _gaussianBuffer.Release();
                _gaussianBuffer = null;
            }

            _gaussianCount = 0;
        }

    }
}