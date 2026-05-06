using OptiLoad.Core.Algorithms;
using OptiLoad.Core.Models;
using Xunit;

namespace OptiLoad.Tests;

public class BranchAndBoundTests
{
    
    private static ContainerDimensions MakeContainer(double w, double h, double d) => new()
    {
        Width  = w,
        Height = h,
        Depth  = d
    };

    private static BoxInstance MakeInstance(double w, double h, double d, int idx,
        bool fragile = false, bool allowRotation = false) => new()
    {
        BoxDefinition = new Box
        {
            BoxId         = idx,
            BoxName       = $"B{idx}",
            Width         = w,
            Height        = h,
            Depth         = d,
            IsFragile     = fragile,
            AllowRotation = allowRotation
        },
        InstanceIndex = idx
    };

[Fact]
    public void Solve_EmptyInput_ReturnsZeroBins()
    {
        var solver = new BranchAndBoundSolver(MakeContainer(100, 100, 100));

        var result = solver.Solve(Enumerable.Empty<BoxInstance>());

        Assert.Equal(0, result.BinsUsed);
        Assert.Empty(result.PlacedBoxes);
        Assert.Empty(result.UnplacedBoxes);
    }

[Fact]
    public void Solve_SingleSmallBox_UsesOneBin()
    {
        var solver = new BranchAndBoundSolver(MakeContainer(100, 100, 100));
        var boxes  = new[] { MakeInstance(10, 10, 10, 0) };

        var result = solver.Solve(boxes);

        Assert.Equal(1, result.BinsUsed);
        Assert.Single(result.PlacedBoxes);
        Assert.Empty(result.UnplacedBoxes);
    }

[Fact]
    public void Solve_TwoHalfContainerBoxes_UsesOneBin()
    {
        var container = MakeContainer(100, 100, 100);
        var solver    = new BranchAndBoundSolver(container);
        var boxes = new[]
        {
            MakeInstance(100, 100, 50, 0), 
            MakeInstance(100, 100, 50, 1)  
        };

        var result = solver.Solve(boxes);

        Assert.Equal(1, result.BinsUsed);
        Assert.Equal(2, result.PlacedBoxes.Count);
    }

[Fact]
    public void Solve_BoxBiggerThanContainer_ReturnsUnplaced()
    {
        var solver = new BranchAndBoundSolver(MakeContainer(10, 10, 10));
        var boxes  = new[] { MakeInstance(20, 20, 20, 0) };

        var result = solver.Solve(boxes);

        Assert.Single(result.UnplacedBoxes);
        Assert.Empty(result.PlacedBoxes);
    }

[Fact]
    public void Solve_FragileBox_PlacedAboveNonFragileBox()
    {
        var container = MakeContainer(100, 100, 100);
        var solver    = new BranchAndBoundSolver(container);

        var boxes = new[]
        {
            MakeInstance(100, 50, 100, idx: 0, fragile: false), 
            MakeInstance(100, 30, 100, idx: 1, fragile: true)   
        };

        var result = solver.Solve(boxes);

        Assert.Equal(2, result.PlacedBoxes.Count);

        var nonFragilePlaced = result.PlacedBoxes.First(p => !p.Instance.BoxDefinition.IsFragile);
        var fragilePlaced    = result.PlacedBoxes.First(p =>  p.Instance.BoxDefinition.IsFragile);

bool fragileIsAbove = fragilePlaced.Y1 >= nonFragilePlaced.Y2 - 1e-9;
        Assert.True(fragileIsAbove,
            $"Fragile Y1={fragilePlaced.Y1} should be >= NonFragile Y2={nonFragilePlaced.Y2}");
    }

[Fact]
    public void Solve_TwoBoxesThatCannotShareBin_UsesTwoBins()
    {
        var container = MakeContainer(100, 100, 100);
        var solver    = new BranchAndBoundSolver(container);
        var boxes = new[]
        {
            MakeInstance(100, 100, 60, 0), 
            MakeInstance(100, 100, 60, 1)  
        };

        var result = solver.Solve(boxes);

        Assert.Equal(2, result.BinsUsed);
        Assert.Equal(2, result.PlacedBoxes.Count);
    }

[Fact]
    public void Solve_RealContainerData_AllBoxesFitInOneBin()
    {
        
        var container = new ContainerDimensions
        {
            Width      = 589,
            Height     = 239,
            Depth      = 235,
            MaxWeightKg = 21770
        };
        var solver = new BranchAndBoundSolver(container);

var boxes = new List<BoxInstance>
        {
            
            MakeInstance(200, 120, 150, 0, allowRotation: true),
            MakeInstance(200, 120, 150, 1, allowRotation: true),
            
            MakeInstance(230,  80,  60, 2, allowRotation: true),
            
            MakeInstance( 40,  50,  50, 3, allowRotation: true),
            MakeInstance( 40,  50,  50, 4, allowRotation: true),
            MakeInstance( 40,  50,  50, 5, allowRotation: true),
            MakeInstance( 70,  90,  50, 6, allowRotation: true),
            MakeInstance( 70,  90,  50, 7, allowRotation: true),
            MakeInstance( 20,  20,  50, 8, allowRotation: true),
            MakeInstance( 20,  20,  50, 9, allowRotation: true),
        };

        var result = solver.Solve(boxes);

        Assert.Equal(1,          result.BinsUsed);
        Assert.Equal(10,         result.PlacedBoxes.Count);
        Assert.Empty(result.UnplacedBoxes);
    }
}
