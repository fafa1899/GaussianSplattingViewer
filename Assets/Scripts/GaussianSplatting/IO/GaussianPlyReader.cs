using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using UnityEngine;

namespace GaussianSplatting.IO
{
    public static class GaussianPlyReader
    {
        public static GaussianData[] ReadFirstVertices(string filePath, out PlyHeader header)
        {
            header = PlyHeader.Read(filePath);

            if (header.Format != "binary_little_endian")
            {
                throw new NotSupportedException($"Unsupported PLY format: {header.Format}");
            }

            int count = header.VertexCount;
            GaussianData[] result = new GaussianData[count];

            int xIndex = header.GetPropertyIndex("x");
            int yIndex = header.GetPropertyIndex("y");
            int zIndex = header.GetPropertyIndex("z");
            int fdc0Index = header.GetPropertyIndex("f_dc_0");
            int fdc1Index = header.GetPropertyIndex("f_dc_1");
            int fdc2Index = header.GetPropertyIndex("f_dc_2");
            int opacityIndex = header.GetPropertyIndex("opacity");
            int scale0Index = header.GetPropertyIndex("scale_0");
            int scale1Index = header.GetPropertyIndex("scale_1");
            int scale2Index = header.GetPropertyIndex("scale_2");
            int rot0Index = header.GetPropertyIndex("rot_0");
            int rot1Index = header.GetPropertyIndex("rot_1");
            int rot2Index = header.GetPropertyIndex("rot_2");
            int rot3Index = header.GetPropertyIndex("rot_3");

            int[] fRestIndex = new int[45];
            for (int i = 0; i < 45; i++)
            {
                fRestIndex[i] = header.GetPropertyIndex($"f_rest_{i}");
                ValidateRequiredProperty(fRestIndex[i], $"f_rest_{i}");
            }

            ValidateRequiredProperty(xIndex, "x");
            ValidateRequiredProperty(yIndex, "y");
            ValidateRequiredProperty(zIndex, "z");
            ValidateRequiredProperty(fdc0Index, "f_dc_0");
            ValidateRequiredProperty(fdc1Index, "f_dc_1");
            ValidateRequiredProperty(fdc2Index, "f_dc_2");
            ValidateRequiredProperty(opacityIndex, "opacity");
            ValidateRequiredProperty(scale0Index, "scale_0");
            ValidateRequiredProperty(scale1Index, "scale_1");
            ValidateRequiredProperty(scale2Index, "scale_2");
            ValidateRequiredProperty(rot0Index, "rot_0");
            ValidateRequiredProperty(rot1Index, "rot_1");
            ValidateRequiredProperty(rot2Index, "rot_2");
            ValidateRequiredProperty(rot3Index, "rot_3");

            // 使用内存映射文件，直接映射到虚拟地址空间，零拷贝
            using var mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            int propertyCount = header.Properties.Count;
            int recordSize = propertyCount * sizeof(float);
            using var accessor = mmf.CreateViewAccessor(header.HeaderByteSize, count * recordSize, MemoryMappedFileAccess.Read);

            // 获取指向数据的原始指针
            unsafe
            {
                byte* basePtr = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref basePtr);

                for (int i = 0; i < count; i++)
                {
                    // 直接通过指针偏移定位当前记录的起始位置
                    float* recordPtr = (float*)(basePtr + i * recordSize);

                    // 直接使用指针解引用
                    float px = recordPtr[xIndex];
                    float py = recordPtr[yIndex];
                    float pz = recordPtr[zIndex];
                    float fdc0 = recordPtr[fdc0Index];
                    float fdc1 = recordPtr[fdc1Index];
                    float fdc2 = recordPtr[fdc2Index];
                    float opacity = recordPtr[opacityIndex];
                    float scale0 = recordPtr[scale0Index];
                    float scale1 = recordPtr[scale1Index];
                    float scale2 = recordPtr[scale2Index];
                    float rot0 = recordPtr[rot0Index];
                    float rot1 = recordPtr[rot1Index];
                    float rot2 = recordPtr[rot2Index];
                    float rot3 = recordPtr[rot3Index];

                    float[] fr = new float[45];
                    for (int j = 0; j < 45; j++)
                    {
                        fr[j] = recordPtr[fRestIndex[j]];
                    }

                    result[i] = new GaussianData
                    {
                        Position = new Vector3(px, py, pz),
                        Fdc = new Vector3(fdc0, fdc1, fdc2),
                        OpacityRaw = opacity,
                        ScaleLog = new Vector3(scale0, scale1, scale2),
                        Rotation = new Quaternion(rot1, rot2, rot3, rot0),
                        //Rotation = new Quaternion(rot0, rot1, rot2, rot3)
                        //Rotation = new Quaternion(rot3, rot0, rot1, rot2)

                        Sh01 = new Vector3(fr[0], fr[15], fr[30]),
                        Sh02 = new Vector3(fr[1], fr[16], fr[31]),
                        Sh03 = new Vector3(fr[2], fr[17], fr[32]),
                        Sh04 = new Vector3(fr[3], fr[18], fr[33]),
                        Sh05 = new Vector3(fr[4], fr[19], fr[34]),
                        Sh06 = new Vector3(fr[5], fr[20], fr[35]),
                        Sh07 = new Vector3(fr[6], fr[21], fr[36]),
                        Sh08 = new Vector3(fr[7], fr[22], fr[37]),
                        Sh09 = new Vector3(fr[8], fr[23], fr[38]),
                        Sh10 = new Vector3(fr[9], fr[24], fr[39]),
                        Sh11 = new Vector3(fr[10], fr[25], fr[40]),
                        Sh12 = new Vector3(fr[11], fr[26], fr[41]),
                        Sh13 = new Vector3(fr[12], fr[27], fr[42]),
                        Sh14 = new Vector3(fr[13], fr[28], fr[43]),
                        Sh15 = new Vector3(fr[14], fr[29], fr[44])
                    };
                }
            }

            accessor.SafeMemoryMappedViewHandle.ReleasePointer();

            return result;
        }

        private static void ValidateRequiredProperty(int propertyIndex, string propertyName)
        {
            if (propertyIndex < 0)
            {
                throw new InvalidDataException($"Required property not found in PLY header: {propertyName}");
            }
        }
    }
}
