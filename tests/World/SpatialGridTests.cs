using Mithara.Server.World;

namespace Mithara.Server.Tests.World;

public class SpatialGridTests
{
    private static SpatialGrid CreateGrid() => new();

    [Fact]
    public void AddEntity_EntityInGrid()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 100f, 200f);

        var nearby = grid.GetEntitiesInRadius(100f, 200f, 50f);
        Assert.Contains(1ul, nearby);
    }

    [Fact]
    public void AddEntity_TwoEntities_SameCell()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 100f, 100f);
        grid.AddEntity(2, 150f, 120f);

        var nearby = grid.GetEntitiesInRadius(125f, 110f, 50f);
        Assert.Equal(2, nearby.Count);
    }

    [Fact]
    public void MoveEntity_ChangesCell()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 0f, 0f);

        grid.MoveEntity(1, 0f, 0f, 500f, 500f);

        var oldPos = grid.GetEntitiesInRadius(0f, 0f, 50f);
        var newPos = grid.GetEntitiesInRadius(500f, 500f, 50f);

        Assert.DoesNotContain(1ul, oldPos);
        Assert.Contains(1ul, newPos);
    }

    [Fact]
    public void MoveEntity_SameCell_NoChange()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 100f, 100f);

        grid.MoveEntity(1, 100f, 100f, 150f, 140f);

        var nearby = grid.GetEntitiesInRadius(125f, 120f, 50f);
        Assert.Contains(1ul, nearby);
    }

    [Fact]
    public void RemoveEntity_RemovesFromGrid()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 100f, 100f);

        grid.RemoveEntity(1);

        var nearby = grid.GetEntitiesInRadius(100f, 100f, 50f);
        Assert.DoesNotContain(1ul, nearby);
    }

    [Fact]
    public void RemoveEntity_NonExistent_DoesNothing()
    {
        var grid = CreateGrid();

        grid.RemoveEntity(999);

        var nearby = grid.GetEntitiesInRadius(0f, 0f, 1000f);
        Assert.Empty(nearby);
    }

    [Fact]
    public void GetEntitiesInRadius_ReturnsEntitiesWithinRadius()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 0f, 0f);
        grid.AddEntity(2, 300f, 0f);
        grid.AddEntity(3, 600f, 0f);

        var nearby = grid.GetEntitiesInRadius(0f, 0f, 400f);

        Assert.Contains(1ul, nearby);
        Assert.Contains(2ul, nearby);
        Assert.DoesNotContain(3ul, nearby);
    }

    [Fact]
    public void GetEntitiesInRadius_EmptyGrid_ReturnsEmpty()
    {
        var grid = CreateGrid();

        var nearby = grid.GetEntitiesInRadius(0f, 0f, 100f);

        Assert.Empty(nearby);
    }

    [Fact]
    public void GetEntitiesInRadius_BroadPhase_ReturnsAllOverlappingCells()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 0f, 0f);
        grid.AddEntity(2, 250f, 0f);

        var nearby = grid.GetEntitiesInRadius(225f, 0f, 50f);

        Assert.Contains(2ul, nearby);
        Assert.Contains(1ul, nearby);
    }

    [Fact]
    public void Clear_RemovesAllEntities()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 100f, 100f);
        grid.AddEntity(2, 200f, 200f);

        grid.Clear();

        var nearby = grid.GetEntitiesInRadius(150f, 150f, 500f);
        Assert.Empty(nearby);
    }

    [Fact]
    public void MultipleEntities_DifferentCells()
    {
        var grid = CreateGrid();
        grid.AddEntity(1, 0f, 0f);
        grid.AddEntity(2, 1000f, 1000f);

        var zone1 = grid.GetEntitiesInRadius(0f, 0f, 100f);
        var zone2 = grid.GetEntitiesInRadius(1000f, 1000f, 100f);

        Assert.Single(zone1);
        Assert.Single(zone2);
        Assert.Contains(1ul, zone1);
        Assert.Contains(2ul, zone2);
    }
}
