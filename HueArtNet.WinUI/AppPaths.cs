namespace HueArtNet.WinUI;

internal static class AppPaths
{
  public const string PortableMarkerFileName = "HueArtNet.portable";

  public static string DataDirectory { get; } = ResolveDataDirectory();

  public static string CrashLogPath => Path.Combine(DataDirectory, "crash.log");

  private static string ResolveDataDirectory()
  {
    string[] candidateDirectories = ResolveApplicationDirectories();
    foreach (string appDirectory in candidateDirectories)
    {
      string portableMarkerPath = Path.Combine(appDirectory, PortableMarkerFileName);
      if (File.Exists(portableMarkerPath))
        return EnsureDirectory(Path.Combine(appDirectory, "data"));
    }

    return EnsureDirectory(Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "HueArtNet"));
  }

  private static string[] ResolveApplicationDirectories()
  {
    var directories = new[]
      {
        Path.GetDirectoryName(Environment.ProcessPath),
        AppContext.BaseDirectory
      }
      .Where(path => !string.IsNullOrWhiteSpace(path))
      .Select(path => Path.GetFullPath(path!))
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToArray();

    return directories.Length == 0 ? new[] { Directory.GetCurrentDirectory() } : directories;
  }

  private static string EnsureDirectory(string path)
  {
    Directory.CreateDirectory(path);
    return path;
  }
}
