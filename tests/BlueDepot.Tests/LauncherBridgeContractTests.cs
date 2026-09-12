using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;
namespace BlueDepot.Tests;

public sealed class LauncherBridgeContractTests
{
    [Fact] public void CharacterSelectionHookMatchesCurrentGameAndDoesNotBypassJoinFlow()
    {
        var game = Environment.GetEnvironmentVariable("GameManaged") ?? "/home/bunta/.local/share/Steam/steamapps/common/Valheim/valheim_Data/Managed";
        using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(game, "assembly_valheim.dll"));
        var startup = assembly.MainModule.Types.Single(t => t.Name == "FejdStartup");
        foreach (var name in new[] { "ShowCharacterSelection", "UpdateCharacterList", "OnCharacterStart" }) Assert.Contains(startup.Methods, m => m.Name == name && m.Parameters.Count == 0);
        Assert.Contains(startup.Fields, f => f.Name == "m_profiles" && f.FieldType.FullName.Contains("PlayerProfile"));
        Assert.Contains(startup.Fields, f => f.Name == "m_profileIndex" && f.FieldType.FullName == "System.Int32");
        Assert.Contains(startup.Fields, f => f.Name == "m_queuedJoinServer" && f.FieldType.Name == "ServerJoinData");
        var profile = assembly.MainModule.Types.Single(t => t.Name == "PlayerProfile");
        Assert.Contains(profile.Methods, m => m.Name == "GetFilename" && m.ReturnType.FullName == "System.String");
        Assert.Contains(profile.Fields, f => f.Name == "m_fileSource");
    }
}
