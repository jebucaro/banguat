namespace Banguat.ExchangeRates.Cli.SkillInstallation;

public sealed record SkillFileFailure(string RelativePath, string Message);

public sealed class InstallResult
{
    private readonly List<string> _written = [];
    private readonly List<string> _updated = [];
    private readonly List<string> _skipped = [];
    private readonly List<SkillFileFailure> _failed = [];

    public IReadOnlyList<string> Written => _written;

    public IReadOnlyList<string> Updated => _updated;

    public IReadOnlyList<string> Skipped => _skipped;

    public IReadOnlyList<SkillFileFailure> Failed => _failed;

    public bool HasChanges => _written.Count > 0 || _updated.Count > 0;

    public bool HasFailures => _failed.Count > 0;

    internal void RecordWritten(string relativePath) => _written.Add(relativePath);

    internal void RecordUpdated(string relativePath) => _updated.Add(relativePath);

    internal void RecordSkipped(string relativePath) => _skipped.Add(relativePath);

    internal void RecordFailed(string relativePath, string message) => _failed.Add(new SkillFileFailure(relativePath, message));
}
