using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.WingetFlow.Helpers
{
    public static class WingetCommandHelper
    {
        public static async Task<string> Execute(string command, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(command))
                return string.Empty;

            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(processInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start process.");

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException(error);
            }

            return output;
        }
    }
}
