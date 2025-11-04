using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.WingetFlow.Models
{
    public class LocalPackageWinget
    {
        public required string Name { get; set; }
        public required string Id { get; set; }
        public required string Version { get; set; }
        public required string Available { get; set; }
        public required string Source { get; set; }
    }
}
