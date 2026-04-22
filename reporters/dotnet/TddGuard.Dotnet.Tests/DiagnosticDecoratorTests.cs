using OneOf;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

internal sealed class DiagnosticDecoratorTests
{
    [Test("LogOnError logs message when result is ResolveError")]
    public async Task LogOnErrorLogsOnFailure()
    {
        string? captured = null;
        OneOf<ProjectRoot, ResolveError> result = new ResolveError("bad path");

        result.LogOnError(msg => captured = msg);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!).Contains("bad path");
    }

    [Test("LogOnError does not log when result is ProjectRoot")]
    public async Task LogOnErrorDoesNotLogOnSuccess()
    {
        string? captured = null;
        OneOf<ProjectRoot, ResolveError> result = new ProjectRoot("/valid");

        result.LogOnError(msg => captured = msg);

        await Assert.That(captured).IsNull();
    }

    [Test("WithDiagnostics logs message when write returns Error")]
    public async Task WithDiagnosticsLogsOnWriteError()
    {
        string? captured = null;
        WriteTestOutput inner = _ => new WriteResult.Error("disk full");
        var decorated = inner.WithDiagnostics(msg => captured = msg);

        decorated(new TestRunOutput([], "passed"));

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!).Contains("disk full");
    }

    [Test("WithDiagnostics does not log when write succeeds")]
    public async Task WithDiagnosticsDoesNotLogOnSuccess()
    {
        string? captured = null;
        WriteTestOutput inner = _ => new WriteResult.Success();
        var decorated = inner.WithDiagnostics(msg => captured = msg);

        decorated(new TestRunOutput([], "passed"));

        await Assert.That(captured).IsNull();
    }
}
