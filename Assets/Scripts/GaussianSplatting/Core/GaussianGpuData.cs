using System;
using UnityEngine;

namespace GaussianSplatting.Core
{
    [Serializable]
    public struct GaussianGpuData
    {
        public Vector3 Position;
        public float Radius;

        public Vector4 Color;
    }
}