using Microsoft.Testing.Platform.Builder;

#pragma warning disable CA1050 // MTP codegen requires global namespace
#pragma warning disable CA1515 // MTP codegen requires public class

public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder builder, string[] _)
        => TddGuard.Dotnet.TddGuardBuilder.Register(builder);
}
