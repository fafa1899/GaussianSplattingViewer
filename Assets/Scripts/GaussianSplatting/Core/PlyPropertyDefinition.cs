using System;

namespace GaussianSplatting.Core
{
    [Serializable]
    public struct PlyPropertyDefinition
    {
        public string Type;
        public string Name;

        public PlyPropertyDefinition(string type, string name)
        {
            Type = type;
            Name = name;
        }

        public override string ToString()
        {
            return $"{Type} {Name}";
        }
    }
}
