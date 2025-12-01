using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Options
{
    public class DatabaseOptions
    {
        public string MssqlConnectionString { get; set; } = "";
        public bool EnableSQLiteFallback { get; set; }
        public string SqlitePath { get; set; } = "fallback.db";
        public string[]? InputNames { get; set; }
        public bool EnableMSSQL { get; set; }
    }
}
