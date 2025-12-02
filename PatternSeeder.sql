INSERT INTO machine_statuses
(
    machine_class,
    status_description,
    rising_edge_reason_required,
    falling_edge_reason_required,
    spindle_running,  -- ID0
    feed_hold,        -- ID1
    dry_run,          -- ID2
    m00_m01,          -- ID3
    in_alarm,         -- ID4
    in_cycle,         -- ID5
    feed_rate_lt_100, -- ID6
    feed_rate_100,    -- ID7
    feed_rate_gt_100, -- ID8
    red_lamp,         -- OD0
    amber_lamp,       -- OD1
    green_lamp,       -- OD2
    InputStatus,
    OutputStatus
)
VALUES
(0, 'Machine power down', NULL, NULL, 0,0,0,0,0,0,0,0,0,0,0,0,'Machine power down','Default Status'),
(0, 'In Cycle', NULL, NULL, 0,0,0,0,0,1,NULL,NULL,NULL,0,1,0,'In Cycle','Default Status'),
(0, 'In Alarm', NULL, NULL, 0,0,0,0,1,0,NULL,NULL,NULL,1,0,0,'In Alarm','Default Status'),
(0, 'In Cycle and in Alarm', NULL, NULL, 0,0,0,0,1,1,NULL,NULL,NULL,1,0,0,'In Cycle and in Alarm','Default Status'),
(0, 'In Cycle with M00/M01', NULL, NULL, 0,0,0,1,0,1,NULL,NULL,NULL,1,0,0,'In Cycle with M00/M01','Default Status'),
(0, 'In Cycle with M00/M01 and in Alarm', NULL, NULL, 0,0,0,1,1,1,NULL,NULL,NULL,1,0,0,'In Cycle with M00/M01 and in Alarm','Default Status'),
(0, 'In Cycle with Dry run', NULL, NULL, 0,0,1,0,0,1,NULL,NULL,NULL,0,1,0,'In Cycle with Dry run','Default Status'),
(0, 'In Cycle with Dry run and in Alarm', NULL, NULL, 0,0,1,0,1,1,NULL,NULL,NULL,0,1,0,'In Cycle with Dry run and in Alarm','Default Status'),
(0, 'In Cycle with Dry run and M00/M01', NULL, NULL, 0,0,1,1,0,1,NULL,NULL,NULL,1,0,0,'In Cycle with Dry run and M00/M01','Default Status'),
(0, 'In Cycle with Dry run and M00/M01 and in Alarm', NULL, NULL, 0,0,1,1,1,1,NULL,NULL,NULL,1,0,0,'In Cycle with Dry run and M00/M01 and in Alarm','Default Status'),
(0, 'Feed hold', NULL, NULL, 0,1,0,0,0,0,NULL,NULL,NULL,1,0,0,'Feed hold','Default Status'),
(0, 'In Cycle with Feed hold', NULL, NULL, 0,1,0,0,0,1,NULL,NULL,NULL,0,1,0,'In Cycle with Feed hold','Default Status'),
(0, 'Feed hold in Alarm', NULL, NULL, 0,1,0,0,1,0,NULL,NULL,NULL,1,0,0,'Feed hold in Alarm','Default Status'),
(0, 'In Cycle with Feed hold and in Alarm', NULL, NULL, 0,1,0,0,1,1,NULL,NULL,NULL,1,0,0,'In Cycle with Feed hold and in Alarm','Default Status'),
(0, 'In Cycle with Feed hold and M00/M01', NULL, NULL, 0,1,0,1,0,1,NULL,NULL,NULL,1,0,0,'In Cycle with Feed hold and M00/M01','Default Status'),
(0, 'In Cycle with Feed hold and M00/M01 and in Alarm', NULL, NULL, 0,1,0,1,1,1,NULL,NULL,NULL,1,0,0,'In Cycle with Feed hold and M00/M01 and in Alarm','Default Status'),
(0, 'Manual Spindle Running gt 100', NULL, NULL, 1,0,0,0,0,0,0,0,1,0,0,1,'Manual Spindle Running gt 100','Default Status'),
(0, 'Manual Spindle Running @ 100', NULL, NULL, 1,0,0,0,0,0,0,1,0,0,0,1,'Manual Spindle Running @ 100','Default Status'),
(0, 'Manual Spindle Running lt 100', NULL, NULL, 1,0,0,0,0,0,1,0,0,0,1,0,'Manual Spindle Running lt 100','Default Status'),
(0, 'Production gt100%', NULL, NULL, 1,0,0,0,0,1,0,0,1,0,0,1,'Production gt100%','Default Status'),
(0, 'Production @ 100%', NULL, NULL, 1,0,0,0,0,1,0,1,0,0,0,1,'Production @ 100%','Default Status'),
(0, 'Production lt 100%', NULL, NULL, 1,0,0,0,0,1,1,0,0,0,1,0,'Production lt 100%','Default Status'),
(0, 'Spindle Running in Alarm', NULL, NULL, 1,0,0,0,1,0,NULL,NULL,NULL,0,1,0,'Spindle Running in Alarm','Default Status'),
(0, 'Production in Alarm', NULL, NULL, 1,0,0,0,1,1,NULL,NULL,NULL,0,1,0,'Production in Alarm','Default Status'),
(0, 'Production at M00/M001', NULL, NULL, 1,0,0,1,0,1,NULL,NULL,NULL,0,1,0,'Production at M00/M001','Default Status'),
(0, 'Production at M00/M001 in Alarm', NULL, NULL, 1,0,0,1,1,1,NULL,NULL,NULL,0,1,0,'Production at M00/M001 in Alarm','Default Status'),
(0, 'Production with Dry run', NULL, NULL, 1,0,1,0,0,1,NULL,NULL,NULL,0,1,0,'Production with Dry run','Default Status'),
(0, 'Production with Dry run in Alarm', NULL, NULL, 1,0,1,0,1,1,NULL,NULL,NULL,0,1,0,'Production with Dry run in Alarm','Default Status'),
(0, 'Production with Dry run and M00/M001', NULL, NULL, 1,0,1,1,0,1,NULL,NULL,NULL,0,1,0,'Production with Dry run and M00/M001','Default Status'),
(0, 'Production with Dry run and M00/M001 in Alarm', NULL, NULL, 1,0,1,1,1,1,NULL,NULL,NULL,0,1,0,'Production with Dry run and M00/M001 in Alarm','Default Status'),
(0, 'Spindle Running with Feed hold', NULL, NULL, 1,1,0,0,0,0,NULL,NULL,NULL,1,0,0,'Spindle Running with Feed hold','Default Status'),
(0, 'Production with Feed hold', NULL, NULL, 1,1,0,0,0,1,NULL,NULL,NULL,1,0,0,'Production with Feed hold','Default Status'),
(0, 'Spindle Running with Feed hold in Alarm', NULL, NULL, 1,1,0,0,1,0,NULL,NULL,NULL,1,0,0,'Spindle Running with Feed hold in Alarm','Default Status'),
(0, 'Production with Feed hold in Alarm', NULL, NULL, 1,1,0,0,1,1,NULL,NULL,NULL,1,0,0,'Production with Feed hold in Alarm','Default Status'),
(0, 'Production with Feed hold and M00/M01', NULL, NULL, 1,1,0,1,0,1,NULL,NULL,NULL,1,0,0,'Production with Feed hold and M00/M01','Default Status'),
(0, 'Production with Feed hold and M00/M01 in Alarm', NULL, NULL, 1,1,0,1,1,1,NULL,NULL,NULL,1,0,0,'Production with Feed hold and M00/M01 in Alarm','Default Status');
