using System;
using UnityEngine;

namespace GaussianSplatting.Core
{
    [Serializable]
    public struct GaussianGpuData
    {
        public Vector3 Position;
        public float Padding0;

        public Vector3 Scale;
        public float Padding1;

        public Vector4 Rotation;
        public Vector4 Color;
    }
}