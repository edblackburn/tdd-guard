namespace TddGuard.Dotnet.Core;

/// <summary>
/// Maps raw MTP test node input into a <see cref="CollectedResult"/>,
/// extracting the full name from the UID and using the file path as module ID.
/// </summary>
public static class TestNodeMapper
{
    public static CollectedResult ToCollectedResult(this TestNodeInput input)
    {
        var uid = input.Uid;
        var parenIndex = uid.IndexOf('(', StringComparison.Ordinal);
        var fullName = parenIndex >= 0 ? uid[..parenIndex] : uid;

        return new CollectedResult(input.DisplayName, fullName, input.FilePath ?? uid, input.State);
    }
}
