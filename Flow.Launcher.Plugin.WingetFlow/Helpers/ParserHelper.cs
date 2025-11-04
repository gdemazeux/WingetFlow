using Flow.Launcher.Plugin.WingetFlow.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Flow.Launcher.Plugin.WingetFlow.Helpers
{
    public static class ParserHelper
    {
        private static readonly Regex SeparatorRegex = new Regex(
            @"^-+$",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500)
        );

        private static readonly Regex SchemeRegex = new Regex(
            @"\S+(?:\s+)?",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(500)
        );

        public static List<PackageWinget> ParseSearch(string output)
        {
            var apps = new List<PackageWinget>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var startIndex = Array.FindIndex(lines, line => SeparatorRegex.IsMatch(line));

            if (startIndex == -1) 
                return apps;

            var scheme = BuildScheme(lines[startIndex - 1]);
            var maxLengh = scheme.Last().Lenght + scheme.Last().Index;

            if (scheme.Count > 3)
            {
                for (int i = startIndex + 1; i < lines.Length; i++)
                {
                    if (lines[i].Length >= maxLengh)
                    {
                        var line = lines[i] + " ";
                        var app = new PackageWinget
                        {
                            Name = line.Substring(scheme[0].Index, scheme[0].Lenght).Trim(),
                            Id = line.Substring(scheme[1].Index, scheme[1].Lenght).Trim(),
                            Version = line.Substring(scheme[2].Index, scheme[2].Lenght).Trim(),
                            Source = scheme.Count > 4
                                ? line.Substring(scheme[4].Index, scheme[4].Lenght + 1).Trim()
                                : line.Substring(scheme[3].Index, scheme[3].Lenght + 1).Trim(),
                        };

                        apps.Add(app);
                    }
                }
            }

            return apps;
        }

        public static List<LocalPackageWinget> ParseLocal(string output)
        {
            var apps = new List<LocalPackageWinget>();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var startIndex = Array.FindIndex(lines, line => SeparatorRegex.IsMatch(line));

            if (startIndex == -1) 
                return apps;

            var scheme = BuildScheme(lines[startIndex - 1]);
            var maxLengh = scheme.Last().Lenght + scheme.Last().Index;

            if (scheme.Count > 4)
            {
                for (int i = startIndex + 1; i < lines.Length - 1; i++)
                {
                    if (lines[i].Length >= maxLengh)
                    {
                        var line = lines[i] + " ";
                        var app = new LocalPackageWinget
                        {
                            Name = line.Substring(scheme[0].Index, scheme[0].Lenght).Trim(),
                            Id = line.Substring(scheme[1].Index, scheme[1].Lenght).Trim(),
                            Version = line.Substring(scheme[2].Index, scheme[2].Lenght).Trim(),
                            Available = line.Substring(scheme[3].Index, scheme[3].Lenght).Trim(),
                            Source = line.Substring(scheme[4].Index, scheme[4].Lenght + 1).Trim()
                        };

                        apps.Add(app);
                    }
                }
            }

            return apps;
        }

        private static List<SchemeResult> BuildScheme(string input)
        {
            var results = new List<SchemeResult>();

            if (string.IsNullOrWhiteSpace(input))
                return results;

            var matchs = SchemeRegex.Matches(input);
            int index = 0;

            foreach (Match match in matchs)
            {
                results.Add(new SchemeResult()
                {
                    Index = index,
                    Lenght = match.Length
                });

                index += match.Length;
            }

            return results;
        }
    }
}
