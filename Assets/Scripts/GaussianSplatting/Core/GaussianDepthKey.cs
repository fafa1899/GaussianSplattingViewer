using System;

namespace GaussianSplatting.Core
{
    [Serializable]
    public struct GaussianDepthKey
    {
        public float Depth;
        public uint Index;
    }
}