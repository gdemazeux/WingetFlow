namespace Flow.Launcher.Plugin.WingetFlow.Models
{
    public class PackageWinget
    {
        public required string Name { get; set; }
        public required string Id { get; set; }
        public required string Version { get; set; }
        public required string Source { get; set; }
        public bool IsUpgradable { get; set; }
        public bool IsAlreadyInstall { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
    }
}
