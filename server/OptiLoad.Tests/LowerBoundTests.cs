using OptiLoad.Core.Algorithms;
using OptiLoad.Core.Models;
using Xunit;

namespace OptiLoad.Tests;

public class LowerBoundTests
{
    
    private static ContainerDimensions MakeContainer(double w, double h, double d) => new()
    {
        Width  = w,
        Height = h,
        Depth  = d
    };

    private static BoxInstance MakeInstance(double w, double h, double d, int idx = 0) => new()
    {
        BoxDefinition = new Box { BoxId = idx, BoxName = $"B{idx}", Width = w, Height = h, Depth = d },
        InstanceIndex = idx
    };

[Fact]
    public void ComputeL0_SingleBoxHalfVolume_ReturnsOne()
    {
        var container = MakeContainer(10, 10, 10); 
        var boxes     = new[] { MakeInstance(5, 10, 10) }; 

        int l0 = LowerBoundCalculator.ComputeL0(boxes, container);

        Assert.Equal(1, l0);
    }

[Fact]
    public void ComputeL0_TwoBoxesExceedOneBin_ReturnsTwo()
    {
        var container = MakeContainer(10, 10, 10); 
        var boxes = new[]
        {
            MakeInstance(8, 10, 10, 0), 
            MakeInstance(5, 10, 10, 1)  
        };

        int l0 = LowerBoundCalculator.ComputeL0(boxes, container);

        Assert.Equal(2, l0);
    }

[Fact]
    public void ComputeL0_BoxExactlyFillsContainer_ReturnsOne()
    {
        var container = MakeContainer(10, 10, 10);
        var boxes     = new[] { MakeInstance(10, 10, 10) };

        int l0 = LowerBoundCalculator.ComputeL0(boxes, container);

        Assert.Equal(1, l0);
    }

[Fact]
    public void ComputeL0_EmptyBoxList_ReturnsZero()
    {
        var container = MakeContainer(10, 10, 10);

        int l0 = LowerBoundCalculator.ComputeL0(Enumerable.Empty<BoxInstance>(), container);

        Assert.Equal(0, l0);
    }
}
