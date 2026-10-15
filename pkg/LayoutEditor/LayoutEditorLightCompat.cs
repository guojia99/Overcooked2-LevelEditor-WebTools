using System.Reflection;
using UnityEngine;

/// <summary>Unity 5.x Light 无 innerSpotAngle；较新版本才有。读写经反射，旧版导出用 spotAngle 估算。</summary>
internal static class LayoutEditorLightCompat
{
    private static readonly PropertyInfo InnerSpotProp;
    private static readonly bool HasInnerSpot;

    static LayoutEditorLightCompat()
    {
        InnerSpotProp = typeof(Light).GetProperty(
            "innerSpotAngle",
            BindingFlags.Public | BindingFlags.Instance);
        HasInnerSpot = InnerSpotProp != null && InnerSpotProp.PropertyType == typeof(float);
    }

    public static float ReadInnerSpotAngle(Light light)
    {
        if (light == null)
            return 21f;
        if (!HasInnerSpot)
            return Mathf.Clamp(light.spotAngle * 0.85f, 0f, light.spotAngle);
        return (float)InnerSpotProp.GetValue(light, null);
    }

    public static void ApplyInnerSpotAngle(Light light, float innerSpotAngle)
    {
        if (light == null || !HasInnerSpot)
            return;
        float max = light.spotAngle;
        float v = Mathf.Clamp(innerSpotAngle, 0f, max > 0f ? max : 179f);
        InnerSpotProp.SetValue(light, v, null);
    }
}
