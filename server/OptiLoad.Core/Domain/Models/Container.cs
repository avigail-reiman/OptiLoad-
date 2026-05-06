using System;

namespace OptiLoad.Core.Models
{

    public class Container
    {
        public int               ContainerId   { get; set; }
        public int               TemplateId    { get; set; }
        public string            ContainerCode { get; set; } = string.Empty;
        public ContainerStatus   Status        { get; set; } = ContainerStatus.Available;
        public DateTime          CreatedAt     { get; set; }

public ContainerTemplate? Template { get; set; }

        public override string ToString() =>
            $"Container[{ContainerCode}] Status={Status}";
    }

    public enum ContainerStatus
    {
        Available,
        Loading,
        Closed
    }
}
