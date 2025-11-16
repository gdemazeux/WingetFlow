using Flow.Launcher.Plugin.WingetFlow.Enums;
using Flow.Launcher.Plugin.WingetFlow.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WGetNET;

namespace Flow.Launcher.Plugin.WingetFlow
{
    public class WingetFlow : IAsyncPlugin, IContextMenu
    {
        private PluginInitContext _context;
        private WinGetPackageManager _packageManager;
        private Dictionary<string, WinGetPackage> _localMap;

        public Task InitAsync(PluginInitContext context)
        {
            _context = context;
            _packageManager = new WinGetPackageManager();

            return Task.CompletedTask;
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {

            var search = query.Search.Trim();

            if (string.IsNullOrWhiteSpace(search) || search.Length <= 2)
                return BuildPromptResult();

            try
            {
                var apps = await GetPackagesFromWinget(search, token);

                if (apps.Count == 0)
                    return BuildNoResult(search);

                return BuildResultsList(apps);
            }
            catch (Exception)
            {
                return [];
            }
        }

        private async Task<List<WinGetPackage>> GetPackagesFromWinget(string search, CancellationToken token)
        {
            var searchTask = _packageManager.SearchPackageAsync(search, cancellationToken: token);
            var localTask = _packageManager.GetInstalledPackagesAsync(cancellationToken: token);

            await Task.WhenAll(searchTask, localTask);

            List<WinGetPackage> searchApps = await searchTask;
            List<WinGetPackage> localApps = await localTask;

            _localMap = localApps
                .DistinctBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(app => app.Id, StringComparer.OrdinalIgnoreCase);

            return searchApps
                .Select(app => _localMap.TryGetValue(app.Id, out var localApp) ? localApp : app)
                .OrderByDescending(a => _localMap.ContainsKey(a.Id))
                .ThenByDescending(a => a.HasUpgrade)
                .ToList();
        }

        private List<Result> BuildResultsList(List<WinGetPackage> apps)
        {
            var results = new List<Result>();

            foreach (var app in apps)
            {
                string title;
                string subTitle;
                string icoPath;

                if (app.HasUpgrade)
                {
                    title = $"{app.Name} | New version available";
                    subTitle = $"ID: {app.Id} | Version: {app.VersionString} -> {app.AvailableVersionString} | Source: {app.SourceName}";
                    icoPath = "Images\\upload.png";
                }
                else if (_localMap.ContainsKey(app.Id))
                {
                    title = $"{app.Name} | Installed";
                    subTitle = $"ID: {app.Id} | Version: {app.VersionString} | Source: {app.SourceName}";
                    icoPath = "Images\\success.png";
                }
                else
                {
                    title = $"{app.Name}";
                    subTitle = $"ID: {app.Id} | Version: {app.VersionString} | Source: {app.SourceName}";
                    icoPath = "Images\\download.png";
                }

                results.Add(new Result
                {
                    Title = title,
                    SubTitle = subTitle,
                    IcoPath = icoPath,
                    ContextData = app,
                    Action = _ =>
                    {
                        if (app.HasUpgrade)
                            Task.Run(() => ExecutePackageOperation(app.Id, app.Name, WingetOperationEnum.Upgrade));
                        else if (!_localMap.ContainsKey(app.Id))
                            Task.Run(() => ExecutePackageOperation(app.Id, app.Name, WingetOperationEnum.Install));
                        else
                            Task.Run(() => ExecutePackageOperation(app.Id, app.Name, WingetOperationEnum.Uninstall));

                        return true;
                    }
                });
            }

            return results;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var app = selectedResult.ContextData as WinGetPackage;
            var menus = new List<Result>();

            if (app is null)
                return menus;

            if (app.HasUpgrade)
            {
                menus.Add(new Result
                {
                    Title = "Update",
                    SubTitle = $"Update to {app.AvailableVersionString}",
                    IcoPath = "Images\\upload.png",
                    Action = _ =>
                    {
                        Task.Run(() => ExecutePackageOperation(app.Id, app.Name, WingetOperationEnum.Upgrade));
                        return true;
                    }
                });
            }

            if (!_localMap.ContainsKey(app.Id))
            {
                menus.Add(new Result
                {
                    Title = "Install",
                    SubTitle = $"Install {app.Name}",
                    IcoPath = "Images\\download.png",
                    Action = _ =>
                    {
                        Task.Run(() => ExecutePackageOperation(app.Id, app.Name, WingetOperationEnum.Install));
                        return true;
                    }
                });
            }
            else
            {
                menus.Add(new Result
                {
                    Title = "Uninstall",
                    SubTitle = $"Uninstall {app.Name}",
                    IcoPath = "Images\\delete.png",
                    Action = _ =>
                    {
                        Task.Run(() => ExecutePackageOperation(app.Id, app.Name, WingetOperationEnum.Uninstall));
                        return true;
                    }
                });
            }

            return menus;
        }

        private async Task ExecutePackageOperation(string packageId, string packageName, WingetOperationEnum operation)
        {
            var op = GetOperationInfo(operation);

            try
            {
                _context.API.BackToQueryResults();
                _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeyword + " ", true);
                _context.API.ShowMsg($"{op.Verb} started", $"{packageName}", GetIconPath("start"));

                var result = operation switch
                {
                    WingetOperationEnum.Install => await _packageManager.InstallPackageAsync(packageId, true),
                    WingetOperationEnum.Uninstall => await _packageManager.UninstallPackageAsync(packageId, true),
                    WingetOperationEnum.Upgrade => await _packageManager.UpgradePackageAsync(packageId, true),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation), "Invalid operation"),
                };

                if (result)
                    _context.API.ShowMsg($"{op.Verb} complete", $"{packageName} {op.SuccessMessage}", GetIconPath("success"));
                else
                    _context.API.ShowMsg($"{op.Verb} cancelled", $"{op.Verb} could not be completed for {packageName}", GetIconPath("error"));
            }
            catch (Exception ex)
            {
                _context.API.ShowMsg($"Error {op.Verb.ToLower()}", $"{op.Verb.ToLower()} failed for {packageName}: {ex.Message}", GetIconPath("error"));
            }
            finally
            {
                _context.API.ChangeQuery("", true);
            }
        }

        private OperationInfo GetOperationInfo(WingetOperationEnum operation)
        {
            return operation switch
            {
                WingetOperationEnum.Install => new("Installation", "was successfully installed", "Installing"),
                WingetOperationEnum.Uninstall => new("Uninstallation", "was successfully uninstalled", "Uninstalling"),
                WingetOperationEnum.Upgrade => new("Update", "was successfully updated", "Updating"),
                _ => throw new ArgumentOutOfRangeException(nameof(operation), "Invalid operation"),
            };
        }

        private string GetIconPath(string iconName) =>
            System.IO.Path.Combine(_context.CurrentPluginMetadata.PluginDirectory, "Images", $"{iconName}.png");

        private List<Result> BuildPromptResult()
        {
            return [
                new Result
                {
                    Title = "Type to search winget packages",
                    SubTitle = "At least 3 characters",
                    IcoPath = "Images\\search.png",
                }
            ];
        }

        private List<Result> BuildNoResult(string search)
        {
            return [
                new Result
                {
                    Title = "No packages found",
                    SubTitle = $"No results found for \"{search}\"",
                    IcoPath = "Images\\search.png",
                }
            ];
        }
    }
}