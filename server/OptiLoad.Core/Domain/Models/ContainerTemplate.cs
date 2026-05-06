using System;

namespace OptiLoad.Core.Models
{

    public class ContainerTemplate
    {
        public int      TemplateId   { get; set; }
        public string   TemplateName { get; set; } = string.Empty;
        public double   Width        { get; set; }
        public double   Height       { get; set; }
        public double   Depth        { get; set; }
        public double   MaxWeightKg  { get; set; }
        public DateTime CreatedAt    { get; set; }

        public double Volume => Width * Height * Depth;

        public override string ToString() =>
            $"{TemplateName} ({Width}×{Height}×{Depth}, maxWeight={MaxWeightKg}kg)";
    }
}
