using OptiLoad.Application.Algorithms;
using OptiLoad.Core.Models;
using Xunit;

namespace OptiLoad.Tests;

public class GreedySolverTests
{
    
    private static ContainerDimensions MakeContainer(double w, double h, double d) => new()
    {
        Width  = w,
        Height = h,
        Depth  = d
    };

    private static BoxInstance MakeInstance(double w, double h, double d, int idx = 0,
        bool allowRotation = false) => new()
    {
        BoxDefinition = new Box
        {
            BoxId         = idx,
            BoxName       = $"B{idx}",
            Width         = w,
            Height        = h,
            Depth         = d,
            AllowRotation = allowRotation
        },
        InstanceIndex = idx
    };

[Fact]
    public void FillRemaining_SingleFittingBox_NoUnplacedBoxes()
    {
        var container = MakeContainer(100, 100, 100);
        var boxes     = new List<BoxInstance> { MakeInstance(10, 10, 10) };
        var openBins  = new List<PackingState>();

        var unplaced = GreedySolver.FillRemaining(boxes, openBins, container);

        Assert.Empty(unplaced);
        Assert.Single(openBins); 
    }

[Fact]
    public void FillRemaining_MultipleSmallBoxes_AllFitInOneBin()
    {
        var container = MakeContainer(100, 100, 100);
        var boxes = Enumerable.Range(0, 8)
            .Select(i => MakeInstance(50, 50, 50, i))
            .ToList();
        var openBins = new List<PackingState>();

        var unplaced = GreedySolver.FillRemaining(boxes, openBins, container);

        Assert.Empty(unplaced);
        Assert.Single(openBins); 
    }

[Fact]
    public void FillRemaining_BoxLargerThanContainer_ReturnsUnplaced()
    {
        var container = MakeContainer(10, 10, 10);
        var boxes     = new List<BoxInstance> { MakeInstance(20, 5, 5) }; 
        var openBins  = new List<PackingState>();

        var unplaced = GreedySolver.FillRemaining(boxes, openBins, container);

        Assert.Single(unplaced);
    }

[Fact]
    public void FillRemaining_WithExistingOpenBin_DoesNotOpenNewBin()
    {
        var container = MakeContainer(100, 100, 100);

var existingBin = new PackingState();
        var existingBox = new PlacedBox(
            MakeInstance(10, 10, 10, idx: 0),
            new Position3D(0, 0, 0),
            new Rotation(10, 10, 10, 0));
        existingBin.AddBox(existingBox);

        var openBins = new List<PackingState> { existingBin };
        var newBoxes = new List<BoxInstance> { MakeInstance(10, 10, 10, idx: 1) };

        GreedySolver.FillRemaining(newBoxes, openBins, container);

        Assert.Single(openBins); 
    }
}
