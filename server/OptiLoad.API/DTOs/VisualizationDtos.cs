using System.ComponentModel.DataAnnotations;

namespace OptiLoad.API.DTOs;

public class VisualizationContainerConfig
{
    [Required][Range(1, 10000)] public double Width       { get; set; }
    [Required][Range(1, 10000)] public double Height      { get; set; }
    [Required][Range(1, 10000)] public double Depth       { get; set; }
    [Range(0, 1_000_000)]       public double MaxWeightKg { get; set; }
}

public class VisualizationBoxConfig
{
    [Required][StringLength(100, MinimumLength = 1)] public string Name { get; set; } = "";
    [Required][Range(1, 10000)] public double W { get; set; }
    [Required][Range(1, 10000)] public double H { get; set; }
    [Required][Range(1, 10000)] public double D { get; set; }
    [Range(0, 100_000)] public double Weight { get; set; }
    public bool Fragile { get; set; }
    public bool AllowRotation { get; set; }
    [Range(1, 10000)]  public int Qty { get; set; } = 1;
}

public class VisualizationRunRequest
{
    [Required] public VisualizationContainerConfig  Container { get; set; } = new();
    [Required][MinLength(1)] public List<VisualizationBoxConfig> Boxes { get; set; } = new();
    [Range(1, 300)] public double TimeLimitSeconds { get; set; } = 10.0;
    [Range(1, 10000)] public double? MaxFillHeight { get; set; }
    public string LoadingFace { get; set; } = "Front";
}
