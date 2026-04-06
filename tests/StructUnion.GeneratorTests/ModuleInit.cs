using System.Runtime.CompilerServices;

namespace StructUnion.GeneratorTests;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifySourceGenerators.Initialize();
        UseProjectRelativeDirectory("Snapshots");
    }
}
