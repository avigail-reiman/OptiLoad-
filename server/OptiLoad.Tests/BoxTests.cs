using OptiLoad.Core.Models;
using Xunit;

namespace OptiLoad.Tests;

public class BoxTests
{
    
    private static Box MakeBox(double w, double h, double d, bool allowRotation = true) => new()
    {
        BoxId         = 1,
        BoxName       = "TestBox",
        Width         = w,
        Height        = h,
        Depth         = d,
        AllowRotation = allowRotation
    };

[Fact]
    public void GetAllowedRotations_WhenRotationDisabled_ReturnsSingleRotation()
    {
        var box = MakeBox(10, 20, 30, allowRotation: false);

        var rotations = box.GetAllowedRotations().ToList();

        Assert.Single(rotations);
        Assert.Equal(10, rotations[0].W);
        Assert.Equal(20, rotations[0].H);
        Assert.Equal(30, rotations[0].D);
    }

[Fact]
    public void GetAllowedRotations_CubeBox_ReturnsExactlyOneUniqueRotation()
    {
        var box = MakeBox(5, 5, 5);

        var rotations = box.GetAllowedRotations().ToList();

        Assert.Single(rotations);
    }

[Fact]
    public void GetAllowedRotations_DistinctDimensions_ReturnsSixUniqueRotations()
    {
        var box = MakeBox(1, 2, 3);

        var rotations = box.GetAllowedRotations().ToList();

        Assert.Equal(6, rotations.Count);
    }

[Fact]
    public void GetAllowedRotations_TwoEqualDimensions_ReturnsThreeUniqueRotations()
    {
        var box = MakeBox(2, 2, 5);

        var rotations = box.GetAllowedRotations().ToList();

        Assert.Equal(3, rotations.Count);
    }

[Fact]
    public void GetAllowedRotations_VolumeIsPreservedAcrossAllRotations()
    {
        var box = MakeBox(2, 3, 7);
        double expectedVolume = 2 * 3 * 7;

        foreach (var rot in box.GetAllowedRotations())
        {
            Assert.Equal(expectedVolume, rot.W * rot.H * rot.D, precision: 9);
        }
    }
}
