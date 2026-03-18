using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFoldering.Models
{
    public enum RuleType { Extension, Keyword }

    public class Rule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public RuleType Type { get; set; }
        public string Value { get; set; }  
    }

    public class TargetFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Path { get; set; }
        public List<Rule> Rules { get; set; } = new List<Rule>();
    }

    public class WatchFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Path { get; set; } 
        public List<TargetFolder> TargetFolders { get; set; } = new List<TargetFolder>();
    }

    public class AppSettings
    {
        public List<WatchFolder> WatchFolders { get; set; } = new List<WatchFolder>();
    }
}
