using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GPulseConnector.Services.Sync;

namespace GPulseConnector.Abstraction.Models    
{
    [Table("machine_statuses")]
    public class PatternMapping : ISyncEntity
    {
        [Column("id")][Key]
        public int Id { get; set; } // Unique row identifier

        long ISyncEntity.Id
    {
        get => Id;
        set => Id = (int)value; // explicit conversion
    }

        [Column("machine_class")]
        public int MachineClass { get; set; } = 0;

        [Column("status_description")][AllowNull]
        public string? StatusDescription { get; set; }

        [Column("rising_edge_reason_required")][AllowNull]
        public bool? RisingEdgeReasonRequired { get; set; }

        [Column("falling_edge_reason_required")]
        public bool? FallingEdgeReasonRequired { get; set; }

        [Column("spindle_running")] 
        public bool? ID0 { get; set; }

        [Column("feed_hold")]
        public bool? ID1 { get; set; }
        
        [Column("dry_run")]
        public bool? ID2 { get; set; }
       
       [Column("m00_m01")]
        public bool? ID3 { get; set; }
        [Column("in_alarm")]
        public bool? ID4 { get; set; } 
        [Column("in_cycle")]
        public bool? ID5 { get; set; } 
        [Column("feed_rate_lt_100")]
        public bool? ID6 { get; set; } 
        [Column("feed_rate_100")]
        public bool? ID7 { get; set; }

        [Column("feed_rate_gt_100")]
        public bool? ID8 { get; set; }

        // Output bits
        [Column("red_lamp")]
        public bool OD0 { get; set; }
        [Column("amber_lamp")]
        public bool OD1 { get; set; }
        [Column("green_lamp")]
        public bool OD2 { get; set; }


        public string? InputStatus { get; set; } ="Default Status";
        public string? OutputStatus { get; set; } = "Default Status";
      

        public string ComputeHash()
        {
            return $"{ID0}|{ID1}|{ID2}|{ID3}|{ID4}|{ID5}|{ID6}|{ID7}|{ID8}|{OD0}|{OD1}|{OD2}|{InputStatus}|{OutputStatus}".GetHashCode().ToString();
        }
    }
}
