using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GaussianSplatting.Core;
using UnityEngine;

namespace GaussianSplatting.Rendering
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GaussianPointCloudRenderer : MonoBehaviour
    {
        [SerializeField]
        private Material pointMaterial;

        private Mesh _mesh;

        public void Build(GaussianData[] gaussians)
        {
            if (gaussians == null || gaussians.Length == 0)
            {
                Debug.LogWarning("No gaussian data to render.", this);
                return;
            }

            Vector3[] vertices = new Vector3[gaussians.Length];
            Color[] colors = new Color[gaussians.Length];
            int[] indices = new int[gaussians.Length];

            for (int i = 0; i < gaussians.Length; i++)
            {
                vertices[i] = gaussians[i].Position;
                colors[i] = gaussians[i].GetApproxColor();
                indices[i] = i;
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
                name = "GaussianPointCloud"
            };

            if (gaussians.Length > 65535)
            {
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            _mesh.vertices = vertices;
            _mesh.colors = colors;
            _mesh.SetIndices(indices, MeshTopology.Points, 0, false);

            _mesh.RecalculateBounds();

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            meshFilter.sharedMesh = _mesh;
            meshRenderer.sharedMaterial = pointMaterial;
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


