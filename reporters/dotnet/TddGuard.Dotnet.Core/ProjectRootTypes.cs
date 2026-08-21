namespace TddGuard.Dotnet.Core;

/// <summary>The directory the report is written beneath.</summary>
public sealed record ProjectRoot(string Path);

/// <summary>Why no project root could be established, so callers can disable and say why.</summary>
public sealed record ResolveError(string Reason);
