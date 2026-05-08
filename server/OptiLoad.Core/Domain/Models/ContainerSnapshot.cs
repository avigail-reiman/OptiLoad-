namespace OptiLoad.Core.Models
{
    public class ContainerSnapshot
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int LoadingStep { get; set; }
        public string BoxName { get; set; } = string.Empty;
        public string ImageData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
