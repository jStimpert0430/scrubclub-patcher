using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using TofuFog.Core;
using Xunit;

namespace TofuFog.Tests;
public class FogTests
{
    [Theory]
    [InlineData(.02f, .5f, .01f)][InlineData(.02f, .25f, .005f)]
    [InlineData(.02f, 1f, .02f)][InlineData(.02f, 0f, 0f)]
    [InlineData(0f, .5f, 0f)][InlineData(.02f, -2f, 0f)][InlineData(.02f, 2f, .02f)]
    public void ScalesDensityWithBoundedSettings(float density, float factor, float expected)
        => Assert.Equal(expected, FogRules.Scale(density, factor));
    [Theory][InlineData(float.NaN)][InlineData(float.PositiveInfinity)][InlineData(float.NegativeInfinity)]
    public void InvalidSettingsFallBackToVanilla(float value) => Assert.Equal(.02f, FogRules.Scale(.02f, value));
    [Theory]
    [InlineData("Rain/Particle System", Layer.None)]
    [InlineData("SnowStorm/snow", Layer.None)]
    [InlineData("Blizzard(Clone)/Particle System", Layer.None)]
    [InlineData("GroundFog/Particles", Layer.Ground)]
    [InlineData("Rain/Mist", Layer.Ground)]
    [InlineData("SnowStorm/Fog/Particle System", Layer.Ground)]
    [InlineData("Fog/Snowflakes/Particle System", Layer.None)]
    [InlineData("Fog/Rain/Particle System", Layer.None)]
    [InlineData("Mist/RainDrops", Layer.None)]
    [InlineData("Lightning/Flash", Layer.None)]
    [InlineData("Fire/Smoke", Layer.None)]
    [InlineData("Nimbus white cloud/trail", Layer.None)]
    [InlineData("Wisplight/Glow", Layer.None)]
    public void OnlyKnownEnvironmentParticleNamesAreIncluded(string path, Layer expected)
        => Assert.Equal(expected, FogRules.EnvironmentLayer(path));
    [Fact] public void MistlandsClassificationSurvivesGenericEnvironmentDiscovery()
    {
        Assert.Equal(Layer.Mistlands, FogRules.Prefer(Layer.Mistlands, Layer.Ground));
        Assert.Equal(Layer.Mistlands, FogRules.Prefer(Layer.None, Layer.Mistlands));
    }
    [Theory]
    [InlineData(true, false, false, 1f)] // Mistlands particles stay vanilla even viewed from outside.
    [InlineData(true, true, false, 1f)]
    [InlineData(false, true, false, 1f)] // Ordinary fog is gated inside Mistlands too.
    [InlineData(false, false, false, .5f)]
    [InlineData(true, false, true, .5f)]
    [InlineData(true, true, true, .5f)]
    [InlineData(false, true, true, .5f)]
    [InlineData(false, false, true, .5f)]
    public void MistlandsReliefRequiresPersonalWisplightDiscovery(bool layer, bool biome, bool known, float expected)
        => Assert.Equal(expected, FogRules.GatedMultiplier(.5f, layer, biome, known));
    [Fact] public void ZeroFogSettingCannotBypassProgressionGate()
        => Assert.Equal(1f, FogRules.GatedMultiplier(0f, true, true, false));
    [Fact] public void LosingCharacterContextRelocksMistlands()
    {
        Assert.Equal(.25f, FogRules.GatedMultiplier(.25f, true, true, true));
        Assert.Equal(1f, FogRules.GatedMultiplier(.25f, true, true, false));
    }
    [Fact] public void NativeKnownItemHistoryIncludesObtainedEquipmentAndIsPersisted()
    {
        using var game = AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        var player = game.MainModule.Types.Single(t => t.Name == "Player");
        Assert.Single(player.Methods, m => m.Name == "IsMaterialKnown" && m.IsPublic && m.Parameters.Count == 1);
        foreach (string name in new[] { "AddKnownItem", "Save", "Load" })
            Assert.Contains(player.Methods.Where(m => m.Name == name && m.HasBody).SelectMany(m => m.Body.Instructions), i => i.Operand is FieldReference f && f.Name == "m_knownMaterial");
    }
    [Fact] public void NativeHooksStillWriteFreshFogAndUseParticleStartColors()
    {
        using var game = AssemblyDefinition.ReadAssembly("/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed/assembly_valheim.dll");
        var env = game.MainModule.Types.Single(t => t.Name == "EnvMan");
        var method = env.Methods.Single(m => m.Name == "SetEnv");
        var il = method.Body.Instructions;
        var firstWrite = il.First(i => i.Operand is MethodReference m && m.Name == "set_fogDensity");
        Assert.Equal(Mono.Cecil.Cil.Code.Ldc_R4, firstWrite.Previous.OpCode.Code);
        Assert.Equal(0f, (float)firstWrite.Previous.Operand);
        Assert.Contains(il, i => i.Operand is MethodReference m && m.Name == "GetMainCamera");
        Assert.Single(env.Methods, m => m.Name == "SetParticleArrayEnabled");
        foreach (var pair in new[] { ("MistEmitter", "PlaceOne"), ("DistantFogEmitter", "PlaceOne"), ("ParticleMist", "Awake") })
            Assert.Single(game.MainModule.Types.Single(t => t.Name == pair.Item1).Methods, m => m.Name == pair.Item2);
        foreach (var type in game.MainModule.Types.Where(t => t.Name == "ParticleMist" || t.Name == "MistEmitter" || t.Name == "DistantFogEmitter"))
        {
            var all = type.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).ToArray();
            Assert.Contains(all, i => i.Operand is MethodReference m && m.Name == "Emit" && m.DeclaringType.Name == "ParticleSystem");
            Assert.DoesNotContain(all, i => i.Operand is MethodReference m && (m.Name == "set_startColor" || m.Name == "set_startColor32"));
        }
    }
    [Fact] public void CompiledPluginDoesNotWriteGameplayFieldsOrChangeParticleCounts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src/TofuFog.Plugin"))) dir = dir.Parent;
        Assert.NotNull(dir);
        using var plugin = AssemblyDefinition.ReadAssembly(Path.Combine(dir!.FullName, "src/TofuFog.Plugin/bin/Release/net472/TofuFog.dll"));
        var types = plugin.MainModule.Types.SelectMany(t => new[] { t }.Concat(t.NestedTypes));
        var il = types.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).ToArray();
        Assert.DoesNotContain(il, i => i.OpCode.Code == Mono.Cecil.Cil.Code.Stfld && i.Operand is FieldReference f && f.DeclaringType.Scope.Name == "assembly_valheim");
        Assert.DoesNotContain(il, i => i.Operand is MethodReference m && new[] { "set_rateOverTime", "set_rateOverDistance", "set_maxParticles", "SetGlobalKey", "set_ambientLight", "set_intensity", "SetParticles" }.Contains(m.Name));
        Assert.DoesNotContain(il, i => i.Operand is MethodReference m && m.Name == "Clear" && m.DeclaringType.Name == "ParticleSystem");
        Assert.Contains(il, i => i.Operand is MethodReference m && m.Name == "set_startColor");
        Assert.Contains(il, i => i.Operand is MethodReference m && m.Name == "set_fogDensity");
    }
}
