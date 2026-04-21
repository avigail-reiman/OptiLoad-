namespace OptiLoad.Core.Models
{
    /// <summary>
    /// קשר Many-to-Many בין משימה לארגזים + כמות (טבלה: PackingJobBox)
    /// </summary>
    public class PackingJobBox
    {
        public int  JobBoxId { get; set; }
        public int  JobId    { get; set; }
        public int  BoxId    { get; set; }
        public int  Quantity { get; set; } = 1;

        // ניווט
        public PackingJob? Job { get; set; }
        public Box?        Box { get; set; }
    }
}
