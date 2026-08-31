namespace PencePerLitre.Sync;

public static class EnvLoader
{
    public static void Load(string? searchPath = null)
    {
        var currentDir = searchPath ?? Directory.GetCurrentDirectory();
        
        // Check current directory and up to 2 parent directories for .env
        string? envPath = null;
        var dir = new DirectoryInfo(currentDir);
        for (int i = 0; i < 3 && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                envPath = candidate;
                break;
            }
            dir = dir.Parent;
        }

        if (envPath == null || !File.Exists(envPath)) return;

        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;

            var separatorIdx = trimmed.IndexOf('=');
            if (separatorIdx <= 0) continue;

            var key = trimmed[..separatorIdx].Trim();
            var value = trimmed[(separatorIdx + 1)..].Trim().Trim('"', '\'');

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

