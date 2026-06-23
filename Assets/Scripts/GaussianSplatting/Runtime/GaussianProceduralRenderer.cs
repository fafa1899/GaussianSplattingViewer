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

        private ComputeBuffer _gaussianBuffer;
        private int _gaussianCount;

        public void Build(GaussianData[] gaussians)
        {
            ReleaseBuffer();

            if (gaussians == null || gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data for procedural renderer.", this);
                return;
            }

            GaussianGpuData[] gpuData = new GaussianGpuData[gaussians.Length];

            for (int i = 0; i < gaussians.Length; i++)
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
                    Color = new Vector4(baseColor.r, baseColor.g, baseColor.b, alpha)
                };
            }

            _gaussianCount = gpuData.Length;
            _gaussianBuffer = new ComputeBuffer(_gaussianCount, 64);
            _gaussianBuffer.SetData(gpuData);

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