using System.Collections.Generic;
using TofuFog.Core;
using UnityEngine;

namespace TofuFog;
// One baseline per scene particle system, independent of repeated environment transitions.
public sealed class FogParticles : MonoBehaviour
{
    private static readonly HashSet<FogParticles> Tracked = new HashSet<FogParticles>();
    private ParticleSystem system;
    private ParticleSystem.MinMaxGradient original;
    private Layer layer;
    private bool captured;

    internal static void Track(ParticleSystem ps, Layer requested)
    {
        if (!ps || requested == Layer.None) return;
        var tracker = ps.GetComponent<FogParticles>() ?? ps.gameObject.AddComponent<FogParticles>();
        if (!tracker.captured)
        {
            tracker.system = ps; tracker.original = ps.main.startColor;
            tracker.layer = requested; tracker.captured = true;
            Tracked.Add(tracker); tracker.Apply();
        }
        else if (FogRules.Prefer(tracker.layer, requested) != tracker.layer)
        { tracker.layer = requested; tracker.Apply(); }
    }
    internal static void RefreshAll() { foreach (var tracker in Tracked) if (tracker) tracker.Apply(); }
    internal static void RestoreAll()
    {
        foreach (var tracker in new List<FogParticles>(Tracked))
            if (tracker) { tracker.Restore(); Destroy(tracker); }
        Tracked.Clear();
    }
    private void Apply()
    {
        if (!system || !captured) return;
        var main = system.main;
        main.startColor = Tint(original, FogRules.Multiplier(Plugin.Factor(layer)));
    }
    private void Restore() { if (captured && system) { var main = system.main; main.startColor = original; } captured = false; }
    private void OnDestroy() { Restore(); Tracked.Remove(this); }

    internal static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient source, float factor)
    {
        if (factor == 1f) return source;
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color: return new ParticleSystem.MinMaxGradient(Alpha(source.color, factor));
            case ParticleSystemGradientMode.TwoColors: return new ParticleSystem.MinMaxGradient(Alpha(source.colorMin, factor), Alpha(source.colorMax, factor));
            case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(TintGradient(source.gradientMin, factor), TintGradient(source.gradientMax, factor));
            default:
                var result = new ParticleSystem.MinMaxGradient(TintGradient(source.gradient, factor));
                result.mode = source.mode; return result;
        }
    }
    private static Color Alpha(Color color, float factor) { color.a *= factor; return color; }
    private static Gradient TintGradient(Gradient source, float factor)
    {
        // Clone the gradient: never mutate shared asset gradients used by other effects.
        var alpha = source.alphaKeys;
        for (int i = 0; i < alpha.Length; i++) alpha[i].alpha *= factor;
        var result = new Gradient(); result.SetKeys(source.colorKeys, alpha); result.mode = source.mode;
        return result;
    }
}
