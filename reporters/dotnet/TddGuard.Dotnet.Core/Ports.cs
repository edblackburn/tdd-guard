namespace TddGuard.Dotnet.Core;

/// <summary>
/// Named delegate for environment variable lookup, replacing <c>Func&lt;string, string?&gt;</c>
/// for readability at call sites.
/// </summary>
public delegate string? GetEnvironmentVariable(string name);

/// <summary>
/// Port delegate that writes a completed test run to persistent storage.
/// Returns errors as values rather than throwing exceptions.
/// </summary>
public delegate WriteResult WriteTestOutput(TestRunOutput output);

/// <summary>
/// Binds a <see cref="WriteTestOutput"/> to the project root the report belongs under.
/// The root is only known once resolved, so the write is built rather than passed.
/// </summary>
public delegate WriteTestOutput OpenTestOutput(string projectRoot);

/// <summary>
/// Reduces a path to the one name the filesystem knows it by, so that two paths
/// reaching the same directory through different links compare equal.
/// </summary>
public delegate string CanonicalPath(string path);

/// <summary>
/// Turns a reported test node into the entry the report carries. Bound to the project
/// root at the composition root, so nothing downstream holds a path.
/// </summary>
public delegate CollectedResult MapTestNode(TestNodeInput input);

/// <summary>
/// Named delegate for retrieving the current working directory.
/// </summary>
public delegate string GetCurrentWorkingDirectory();

/// <summary>
/// Port delegate for diagnostic messages (resolve failures, write errors).
/// Production default writes to stderr with a <c>[tdd-guard-dotnet]</c> prefix.
/// </summary>
public delegate void LogDiagnostic(string message);
