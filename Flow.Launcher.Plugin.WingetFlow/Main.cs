using Flow.Launcher.Plugin.WingetFlow.Helpers;
using Flow.Launcher.Plugin.WingetFlow.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.WingetFlow
{
    public class WingetFlow : IAsyncPlugin
    {
        private PluginInitContext _context;
        private CancellationTokenSource _debounceCts;
        private const int DebounceDelayMs = 1000;
        private bool _isInstalling;
        private string _currentInstallation;

        public Task InitAsync(PluginInitContext context)
        {
            _context = context;
            return Task.CompletedTask;
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            if (_isInstalling)
                return BuildInstallationResult();

            var linkedCts = await HandleDebounce(token);
            if (linkedCts == null)
                return new List<Result>();

            var search = query.Search.Trim();

            if (string.IsNullOrWhiteSpace(search) || search.Length < 2)
                return BuildPromptResult();

            try
            {
                var apps = await GetPackagesFromWinget(search, linkedCts.Token);

                if (apps.Count == 0)
                    return BuildNoResult(search);

                return BuildResultsList(apps);
            }
            catch (TaskCanceledException)
            {
                return new List<Result>();
            }
        }

        private async Task<CancellationTokenSource> HandleDebounce(CancellationToken token)
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, _debounceCts.Token);

            try
            {
                await Task.Delay(DebounceDelayMs, linkedCts.Token);
                return linkedCts;
            }
            catch (TaskCanceledException)
            {
                return null;
            }
        }

        private async Task<List<PackageWinget>> GetPackagesFromWinget(string search, CancellationToken token)
        {
            var searchCommand = $"winget search \"{search}\"";
            var listCommand = "winget list";

            Task<string> searchTask = WingetCommandHelper.Execute(searchCommand, token);
            Task<string> localTask = WingetCommandHelper.Execute(listCommand, token);

            await Task.WhenAll(searchTask, localTask);

            List<PackageWinget> searchApps = ParserHelper.ParseSearch(searchTask.Result);
            List<LocalPackageWinget> localApps = ParserHelper.ParseLocal(localTask.Result);

            var localeMap = localApps
                .GroupBy(u => u.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var app in searchApps)
            {
                if (localeMap.TryGetValue(app.Id, out var upgrade))
                {
                    app.IsAlreadyInstall = true;
                    app.IsUpgradable = !string.IsNullOrEmpty(upgrade.Available);
                    app.CurrentVersion = upgrade.Version;
                }
            }

            return searchApps
                .OrderByDescending(a => a.IsAlreadyInstall)
                .ThenBy(a => a.IsUpgradable)
                .ToList();
        }

        private List<Result> BuildResultsList(List<PackageWinget> apps)
        {
            var results = new List<Result>();

            foreach (var app in apps)
            {
                string title = ""; 
                string subTitle = ""; 
                string icoPath = ""; 

                if (app.IsUpgradable) 
                { 
                    title = $"{app.Name} | New version available"; 
                    subTitle = $"ID: {app.Id} | Version: {app.CurrentVersion} -> {app.Version} | Source: {app.Source}"; 
                    icoPath = "Images\\upload.png"; }
                else if (app.IsAlreadyInstall) 
                { 
                    title = $"{app.Name} | Installed"; 
                    subTitle = $"ID: {app.Id} | Version: {app.CurrentVersion} | Source: {app.Source}"; 
                    icoPath = "Images\\success.png"; 
                }
                else
                { 
                    title = $"{app.Name}"; 
                    subTitle = $"ID: {app.Id} | Version: {app.Version} | Source: {app.Source}"; 
                    icoPath = "Images\\download.png"; 
                }

                results.Add(new Result
                {
                    Title = title,
                    SubTitle = subTitle,
                    IcoPath = icoPath,
                    Action = _ =>
                    {
                        if (app.IsUpgradable)
                            Task.Run(() => UpgradePackage(app.Id, app.Name));
                        else if (!app.IsAlreadyInstall)
                            Task.Run(() => InstallPackage(app.Id, app.Name));

                        return false;
                    }
                });
            }

            return results;
        }

        private async Task ExecutePackageOperation(string packageId, string packageName, bool isUpgrade)
        {
            var op = GetOperationInfo(isUpgrade);
            try
            {
                _isInstalling = true;
                _currentInstallation = $"{op.ProgressMessage} {packageName}...";

                _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeyword + " ", true);

                var command = $"winget {op.Command} --id \"{packageId}\" --silent --accept-source-agreements --accept-package-agreements";
                await WingetCommandHelper.Execute(command);

                _context.API.ShowMsg($"{op.Verb} complete", $"{packageName} {op.SuccessMessage}", GetIconPath("success"));
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg($"Error {op.Verb.ToLower()}", $"Update failed {op.Verb.ToLower()} for {packageName}: {ex.Message}", GetIconPath("error"));
            }
            finally
            {
                _isInstalling = false;
                _context.API.ChangeQuery("", true);
            }
        }

        private OperationInfo GetOperationInfo(bool isUpgrade) => isUpgrade
            ? new("Update", "upgrade", "successfully updated", "Update")
            : new("Installation", "install", "was successfully installed", "Installing");

        private async Task InstallPackage(string packageId, string packageName)
            => await ExecutePackageOperation(packageId, packageName, isUpgrade: false);

        private async Task UpgradePackage(string packageId, string packageName)
            => await ExecutePackageOperation(packageId, packageName, isUpgrade: true);

        private string GetIconPath(string iconName)
            => System.IO.Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "Images", $"{iconName}.png");

        private List<Result> BuildInstallationResult()
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Installing...",
                    SubTitle = _currentInstallation,
                    IcoPath = "Images\\download.png",
                }
            };
        }

        private List<Result> BuildPromptResult()
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "Type to search for packages",
                    SubTitle = "At least 2 characters",
                    IcoPath = "Images\\search.png",
                }
            };
        }

        private List<Result> BuildNoResult(string search)
        {
            return new List<Result>
            {
                new Result
                {
                    Title = "No packages found",
                    SubTitle = $"No results found for \"{search}\"",
                    IcoPath = "Images\\search.png",
                }
            };
        }
    }
}