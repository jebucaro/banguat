namespace Banguat.ExchangeRates.Cli.SkillInstallation;

public static class SkillInstaller
{
    public static InstallResult Install(
        string root, SkillLocation location, string skillName, IReadOnlyList<SkillAssetFile> files)
    {
        InstallResult result = new();
        string skillRoot = Path.Combine(root, location.RelativeSkillDirectory, skillName);

        foreach (SkillAssetFile file in files)
        {
            string relativePath = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(skillRoot, relativePath);

            try
            {
                string? directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(fullPath))
                {
                    string existing = File.ReadAllText(fullPath);
                    if (string.Equals(
                            existing.ReplaceLineEndings("\n"),
                            file.Content.ReplaceLineEndings("\n"),
                            StringComparison.Ordinal))
                    {
                        result.RecordSkipped(file.RelativePath);
                        continue;
                    }

                    File.WriteAllText(fullPath, file.Content);
                    result.RecordUpdated(file.RelativePath);
                }
                else
                {
                    File.WriteAllText(fullPath, file.Content);
                    result.RecordWritten(file.RelativePath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.RecordFailed(file.RelativePath, ex.Message);
            }
        }

        return result;
    }
}
