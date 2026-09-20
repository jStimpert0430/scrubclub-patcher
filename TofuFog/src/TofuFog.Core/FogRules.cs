using System;

namespace TofuFog.Core;
public enum Layer { None, Ground, Mistlands }
public static class FogRules
{
    public static float Multiplier(float value) => float.IsNaN(value) || float.IsInfinity(value)
        ? 1f : Math.Max(0f, Math.Min(1f, value));
    public static float Scale(float original, float multiplier) => original * Multiplier(multiplier);

    // Called only on particle systems under EnvSetup roots, never arbitrary world VFX.
    public static Layer EnvironmentLayer(string path)
    {
        // Prefer the nearest named effect. A snow child beneath a fog-named root
        // is still snow, while a fog child beneath a rain root is still fog.
        var parts = path.ToLowerInvariant().Split('/');
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            string name = parts[i];
            if (name.Contains("mist") || name.Contains("fog")) return Layer.Ground;
            if (name.Contains("snow") || name.Contains("blizzard") || name.Contains("rain")) return Layer.None;
        }
        return Layer.None;
    }
    public static float GatedMultiplier(float requested, bool mistlandsLayer, bool inMistlands, bool wisplightKnown)
        => !wisplightKnown && (mistlandsLayer || inMistlands) ? 1f : Multiplier(requested);
    public static Layer Prefer(Layer existing, Layer candidate) => (Layer)Math.Max((int)existing, (int)candidate);
}
