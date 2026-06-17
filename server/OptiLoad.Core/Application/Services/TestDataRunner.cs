using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OptiLoad.Core.Models;

// ⚠️ DEAD CODE — מחלקה זו נקראת רק מ-TestDataMain שגם הוא קוד מת
namespace OptiLoad.Core.Services
{
    public class TestDataRunner
    {
        public static async Task RunFromJson(string jsonPath)
        {
            var json = await File.ReadAllTextAsync(jsonPath);
            var dto = JsonSerializer.Deserialize<TestDataDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto == null)
            {
                Console.WriteLine("[TestDataRunner] Failed to load test data.");
                return;
            }

            var container = new ContainerDimensions
            {
                Width = dto.container.Width,
                Height = dto.container.Height,
                Depth = dto.container.Depth,
                MaxWeightKg = dto.container.MaxWeightKg
            };

            var boxInstances = new List<BoxInstance>();
            int idx = 1;
            foreach (var box in dto.boxes)
            {
                boxInstances.Add(new BoxInstance
                {
                    BoxDefinition = new Box
                    {
                        BoxId = box.BoxId,
                        BoxName = box.BoxName,
                        Width = box.Width,
                        Height = box.Height,
                        Depth = box.Depth,
                        WeightKg = box.WeightKg,
                        IsFragile = box.IsFragile,
                        AllowRotation = box.AllowRotation
                    },
                    InstanceIndex = idx++
                });
            }

            var service = new PackingService();
            var result = service.RunPackingJobInMemory(container, boxInstances);
            PackingService.PrintReport(result, container);
        }

        private class TestDataDto
        {
            public ContainerDto container { get; set; }
            public List<BoxDto> boxes { get; set; }
        }
        private class ContainerDto
        {
            public double Width { get; set; }
            public double Height { get; set; }
            public double Depth { get; set; }
            public double MaxWeightKg { get; set; }
        }
        private class BoxDto
        {
            public int BoxId { get; set; }
            public string BoxName { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double Depth { get; set; }
            public double WeightKg { get; set; }
            public bool IsFragile { get; set; }
            public bool AllowRotation { get; set; }
        }
    }
}
