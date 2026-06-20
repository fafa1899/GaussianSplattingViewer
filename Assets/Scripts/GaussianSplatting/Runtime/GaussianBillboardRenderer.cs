using GaussianSplatting.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace GaussianSplatting.Rendering
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GaussianBillboardRenderer : MonoBehaviour
    {
        [SerializeField]
        private Material billboardMaterial;

        [SerializeField]
        private float radiusMultiplier = 1.0f;

        [SerializeField]
        private float minRadius = 0.001f;

        [SerializeField]
        private float maxRadius = 0.05f;

        private Mesh _mesh;

        public void Build(GaussianData[] gaussians)
        {
            if (gaussians == null || gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data to build billboard mesh.", this);
                return;
            }

            int quadCount = gaussians.Length;
            int vertexCount = quadCount * 4;
            int indexCount = quadCount * 6;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            Vector2[] uv2 = new Vector2[vertexCount];
            Color[] colors = new Color[vertexCount];
            int[] indices = new int[indexCount];

            for (int i = 0; i < quadCount; i++)
            {
                GaussianData g = gaussians[i];

                Color baseColor = g.GetApproxColor();
                float alpha = g.GetOpacity();
                Color finalColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                Vector3 scale = g.GetScale();
                float radius = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * radiusMultiplier;
                radius = Mathf.Clamp(radius, minRadius, maxRadius);

                int v = i * 4;
                int t = i * 6;

                Vector3 center = g.Position;

                vertices[v + 0] = center;
                vertices[v + 1] = center;
                vertices[v + 2] = center;
                vertices[v + 3] = center;

                uv[v + 0] = new Vector2(-1f, -1f);
                uv[v + 1] = new Vector2(1f, -1f);
                uv[v + 2] = new Vector2(1f, 1f);
                uv[v + 3] = new Vector2(-1f, 1f);

                // uv2.x 用来传每个高斯自己的半径
                uv2[v + 0] = new Vector2(radius, 0f);
                uv2[v + 1] = new Vector2(radius, 0f);
                uv2[v + 2] = new Vector2(radius, 0f);
                uv2[v + 3] = new Vector2(radius, 0f);

                colors[v + 0] = finalColor;
                colors[v + 1] = finalColor;
                colors[v + 2] = finalColor;
                colors[v + 3] = finalColor;

                indices[t + 0] = v + 0;
                indices[t + 1] = v + 1;
                indices[t + 2] = v + 2;
                indices[t + 3] = v + 0;
                indices[t + 4] = v + 2;
                indices[t + 5] = v + 3;
            }

            if (_mesh != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(_mesh);
#else
                Destroy(_mesh);
#endif
            }

            _mesh = new Mesh
            {
                name = "GaussianBillboards",
                indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            _mesh.vertices = vertices;
            _mesh.uv = uv;
            _mesh.uv2 = uv2;
            _mesh.colors = colors;
            _mesh.triangles = indices;
            _mesh.RecalculateBounds();

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            meshFilter.sharedMesh = _mesh;
            meshRenderer.sharedMaterial = billboardMaterial;
        }

        private void LateUpdate()
        {
            if (billboardMaterial == null || Camera.main == null)
            {
                return;
            }

            Transform cam = Camera.main.transform;
            billboardMaterial.SetVector("_CameraRightWS", cam.right);
            billboardMaterial.SetVector("_CameraUpWS", cam.up);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(_mesh);
#else
                Destroy(_mesh);
#endif
            }
        }
    }
}