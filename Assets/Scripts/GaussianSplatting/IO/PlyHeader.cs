using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GaussianSplatting.Core;

namespace GaussianSplatting.IO
{
    [Serializable]
    public sealed class PlyHeader
    {
        public string Format { get; private set; }
        public string Version { get; private set; }
        public int VertexCount { get; private set; }
        public int HeaderByteSize { get; private set; }
        public IReadOnlyList<PlyPropertyDefinition> Properties => _properties;

        private readonly List<PlyPropertyDefinition> _properties = new();

        public int GetPropertyIndex(string propertyName)
        {
            for (int i = 0; i < _properties.Count; i++)
            {
                if (_properties[i].Name == propertyName)
                {
                    return i;
                }
            }

            return -1;
        }

        public int GetVertexStrideBytes()
        {
            int total = 0;

            for (int i = 0; i < _properties.Count; i++)
            {
                total += GetPropertySizeBytes(_properties[i].Type);
            }

            return total;
        }

        private static int GetPropertySizeBytes(string propertyType)
        {
            return propertyType switch
            {
                "char" => 1,
                "uchar" => 1,
                "int8" => 1,
                "uint8" => 1,
                "short" => 2,
                "ushort" => 2,
                "int16" => 2,
                "uint16" => 2,
                "int" => 4,
                "uint" => 4,
                "int32" => 4,
                "uint32" => 4,
                "float" => 4,
                "float32" => 4,
                "double" => 8,
                "float64" => 8,
                _ => throw new NotSupportedException($"Unsupported PLY property type: {propertyType}")
            };
        }

        public static PlyHeader Read(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("PLY path is null or empty.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PLY file not found.", filePath);
            }

            PlyHeader header = new PlyHeader();

            using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            List<byte> lineBuffer = new List<byte>(256);
            bool inVertexElement = false;

            while (true)
            {
                string line = ReadAsciiLine(stream, lineBuffer);
                if (line == null)
                {
                    throw new InvalidDataException("Unexpected end of file while reading PLY header.");
                }

                if (line == "ply")
                {
                    continue;
                }

                if (line.StartsWith("format "))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        header.Format = parts[1];
                        header.Version = parts[2];
                    }
                    continue;
                }

                if (line.StartsWith("comment "))
                {
                    continue;
                }

                if (line.StartsWith("element "))
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        string elementName = parts[1];
                        inVertexElement = elementName == "vertex";

                        if (inVertexElement && int.TryParse(parts[2], out int vertexCount))
                        {
                            header.VertexCount = vertexCount;
                        }
                    }
                    continue;
                }

                if (line.StartsWith("property ") && inVertexElement)
                {
                    string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 3)
                    {
                        header._properties.Add(new PlyPropertyDefinition(parts[1], parts[2]));
                    }
                    else if (parts.Length == 5 && parts[1] == "list")
                    {
                        header._properties.Add(new PlyPropertyDefinition($"{parts[1]} {parts[2]} {parts[3]}", parts[4]));
                    }

                    continue;
                }

                if (line == "end_header")
                {
                    header.HeaderByteSize = (int)stream.Position;
                    break;
                }
            }

            if (string.IsNullOrEmpty(header.Format))
            {
                throw new InvalidDataException("PLY header missing format definition.");
            }

            return header;
        }

        private static string ReadAsciiLine(FileStream stream, List<byte> lineBuffer)
        {
            lineBuffer.Clear();

            while (true)
            {
                int value = stream.ReadByte();
                if (value < 0)
                {
                    if (lineBuffer.Count == 0)
                    {
                        return null;
                    }

                    break;
                }

                byte b = (byte)value;

                if (b == (byte)'\n')
                {
                    break;
                }

                if (b != (byte)'\r')
                {
                    lineBuffer.Add(b);
                }
            }

            return Encoding.ASCII.GetString(lineBuffer.ToArray());
        }



        public string ToDebugString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("PLY Header");
            sb.AppendLine($"Format: {Format} {Version}");
            sb.AppendLine($"VertexCount: {VertexCount}");
            sb.AppendLine($"HeaderByteSize: {HeaderByteSize}");
            sb.AppendLine($"VertexStrideBytes: {GetVertexStrideBytes()}");
            sb.AppendLine("Properties:");

            for (int i = 0; i < _properties.Count; i++)
            {
                sb.AppendLine($"  [{i}] {_properties[i]}");
            }

            return sb.ToString();
        }
    }
}
