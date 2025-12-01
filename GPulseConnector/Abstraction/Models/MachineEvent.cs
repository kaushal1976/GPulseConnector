using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Abstraction.Models
{
    [Table("machine_events")]
    public class MachineEvent
    {
        [Column("id")] public int Id { get; set; }
        [Column("status_id")] public int? StatusId { get; set; }
        [Column("machine_id")] public int MachineId { get; set; }
        [Column("timestamp")] public DateTime TimeStamp { get; set; }
        [Column("additional_information")] public required string AdditionalInformation { get; set; }
        [Column("epoch_ms")] public long Epoch { get; set; }
        [Column("spindle_running")] public bool SpindleRunning { get; set; }
        [Column("feed_hold")] public bool FeedHold { get; set; }
        [Column("dry_run")] public bool DryRun { get; set; }
        [Column("m00_m01")] public bool M00M01 { get; set; }
        [Column("in_alarm")] public bool InAlarm { get; set; }
        [Column("in_cycle")] public bool InClycle { get; set; }
        [Column("feedrate_less_than_100")] public bool FeedRateLessThan100 { get; set; }
        [Column("feed_rate_is_100")] public bool FeedRateIs100 { get; set; }
        [Column("feedrate_more_than_100")] public bool FeedRateGreaterThan100 { get; set; }

        public MachineEvent CloneObj()
        {
            return (MachineEvent)MemberwiseClone();
        }

    }

}
