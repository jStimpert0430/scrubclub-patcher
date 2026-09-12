using System.IO;
using Mono.Cecil;
var input=args[0];var output=args[1];Directory.CreateDirectory(output);
void Public(TypeDefinition t)
{
    if(t.IsNested)t.IsNestedPublic=true;else t.IsPublic=true;
    foreach(var f in t.Fields)f.IsPublic=true;
    foreach(var m in t.Methods)m.IsPublic=true;
    foreach(var child in t.NestedTypes)Public(child);
}
using var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(input);
foreach(var path in Directory.GetFiles(input,"assembly_*.dll"))
{
    using var a=AssemblyDefinition.ReadAssembly(path,new ReaderParameters{AssemblyResolver=resolver});
    foreach(var t in a.MainModule.Types)Public(t);
    a.Write(Path.Combine(output,Path.GetFileName(path)));
}
