namespace OptiLoad.Core.Models
{

    public class PackingJobBox
    {
        public int  JobBoxId { get; set; }
        public int  JobId    { get; set; }
        public int  BoxId    { get; set; }
        public int  Quantity { get; set; } = 1;

public PackingJob? Job { get; set; }
        public Box?        Box { get; set; }
    }
}
