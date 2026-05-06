using System;

namespace OptiLoad.Core.Models
{

    public class PackingJob
    {
        public int        JobId             { get; set; }
        public int        ContainerId       { get; set; }
        public JobStatus  Status            { get; set; } = JobStatus.Pending;
        public int?       BinsUsed          { get; set; }
        public double?    VolumeUtilization { get; set; }
        public double?    TotalWeightKg     { get; set; }
        public double?    SolveTimeSeconds  { get; set; }
        public bool?      IsOptimal         { get; set; }
        public string?    StatusMessage     { get; set; }
        public DateTime   CreatedAt         { get; set; }
        public DateTime?  CompletedAt       { get; set; }

public Container? Container { get; set; }

        public override string ToString() =>
            $"Job[{JobId}] Status={Status}, Bins={BinsUsed}";
    }

    public enum JobStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }
}
