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

        public Vector4 Sh01;
        public Vector4 Sh02;
        public Vector4 Sh03;
        public Vector4 Sh04;
        public Vector4 Sh05;
        public Vector4 Sh06;
        public Vector4 Sh07;
        public Vector4 Sh08;
        public Vector4 Sh09;
        public Vector4 Sh10;
        public Vector4 Sh11;
        public Vector4 Sh12;
        public Vector4 Sh13;
        public Vector4 Sh14;
        public Vector4 Sh15;
    }
}