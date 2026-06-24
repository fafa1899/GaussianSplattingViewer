using System;
using UnityEngine;

[Serializable]
public struct GaussianData
{
    public Vector3 Position;
    public Vector3 Fdc;
    public float OpacityRaw;
    public Vector3 ScaleLog;
    public Quaternion Rotation;

    // 15 个高阶 SH 基底，每个 Vector3 表示 RGB
    public Vector3 Sh01;
    public Vector3 Sh02;
    public Vector3 Sh03;
    public Vector3 Sh04;
    public Vector3 Sh05;
    public Vector3 Sh06;
    public Vector3 Sh07;
    public Vector3 Sh08;
    public Vector3 Sh09;
    public Vector3 Sh10;
    public Vector3 Sh11;
    public Vector3 Sh12;
    public Vector3 Sh13;
    public Vector3 Sh14;
    public Vector3 Sh15;

    public Color GetApproxColor()
    {
        const float c0 = 0.28209479177387814f;
        return new Color(
            Fdc.x * c0 + 0.5f,
            Fdc.y * c0 + 0.5f,
            Fdc.z * c0 + 0.5f,
            1f
        );
    }

    public float GetOpacity()
    {
        return 1f / (1f + Mathf.Exp(-OpacityRaw));
    }

    public Vector3 GetScale()
    {
        return new Vector3(
            Mathf.Exp(ScaleLog.x),
            Mathf.Exp(ScaleLog.y),
            Mathf.Exp(ScaleLog.z)
        );
    }

    public override string ToString()
    {
        Color color = GetApproxColor();
        Vector3 scale = GetScale();
        float alpha = GetOpacity();

        return
            $"Position: {Position}\n" +
            $"Fdc: {Fdc}\n" +
            $"ApproxColor: ({color.r:F4}, {color.g:F4}, {color.b:F4})\n" +
            $"OpacityRaw: {OpacityRaw:F6}\n" +
            $"Opacity: {alpha:F6}\n" +
            $"ScaleLog: {ScaleLog}\n" +
            $"Scale: ({scale.x:E4}, {scale.y:E4}, {scale.z:E4})\n" +
            $"Rotation: ({Rotation.x:F6}, {Rotation.y:F6}, {Rotation.z:F6}, {Rotation.w:F6})";
    }
}
