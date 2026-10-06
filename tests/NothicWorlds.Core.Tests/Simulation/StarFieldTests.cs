using NothicWorlds.Core.Geometry;
using NothicWorlds.Core.Model;
using NothicWorlds.Core.Simulation;

namespace NothicWorlds.Core.Tests.Simulation;

public class StarFieldTests
{
    private const int Seed = 424242;

    [Fact]
    public void Stars_AreTheSameForTheSameSeed_AndDifferForAnother()
    {
        Assert.Equal(StarField.Stars(Seed), StarField.Stars(Seed));
        Assert.NotEqual(StarField.Stars(Seed).Select(s => s.Id),
            StarField.Stars(Seed + 1).Select(s => s.Id));
    }

    [Fact]
    public void Stars_AreFixedForever()
    {
        // Saved constellations name stars by id, so these must never change.
        IReadOnlyList<StarField.Star> stars = StarField.Stars(Seed);

        Assert.Equal(4855, stars.Count);
        Assert.Equal([22, 121, 139, 246], stars.Take(4).Select(s => s.Id));
        Assert.Equal(0.6446419813, stars[0].Direction.X, 1e-9);
        Assert.Equal(0.89, stars[2].Brightness, 0.01);
    }

    [Fact]
    public void Stars_AreSpreadEvenlyOverTheSky()
    {
        IReadOnlyList<StarField.Star> stars = StarField.Stars(Seed);

        // Each of the six cube faces covers a sixth of the sky.
        foreach (IGrouping<int, StarField.Star> face in stars.GroupBy(s => s.Id / 16384))
        {
            Assert.InRange(face.Count(), stars.Count / 6 * 0.85, stars.Count / 6 * 1.15);
        }

        // A face's middle and its corners get about as many stars for the sky they cover.
        int middle = stars.Count(s => Math.Abs(StarField.FaceOf(s.Direction).A) < 0.3
            && Math.Abs(StarField.FaceOf(s.Direction).B) < 0.3);
        int corners = stars.Count(s => Math.Abs(StarField.FaceOf(s.Direction).A) > 0.7
            && Math.Abs(StarField.FaceOf(s.Direction).B) > 0.7);
        double middleArea = SkyShare(0, 0.3), cornerArea = SkyShare(0.7, 1.0);
        Assert.InRange(middle / middleArea / (corners / cornerArea), 0.8, 1.25);
    }

    [Fact]
    public void EveryStar_IsInTheCellItsIdNames()
    {
        Assert.All(StarField.Stars(Seed),
            star => Assert.Equal(star.Id, StarField.CellOf(star.Direction)));
    }

    [Theory]
    [InlineData(0.3, -0.8, 0.5)]
    [InlineData(-1, 0, 0)]
    [InlineData(0.01, 1, -0.02)]
    [InlineData(0.5, 0.2, -0.9)]
    public void FaceOf_IsTheReverseOfDirectionOf(double x, double y, double z)
    {
        var direction = new Vector3D(x, y, z);
        direction *= 1 / direction.Length;

        (int face, double a, double b) = StarField.FaceOf(direction);
        Vector3D back = StarField.DirectionOf(face, a, b);

        Assert.Equal(direction.X, back.X, 1e-12);
        Assert.Equal(direction.Y, back.Y, 1e-12);
        Assert.Equal(direction.Z, back.Z, 1e-12);
    }

    [Fact]
    public void CellImage_HoldsEachStarWhereItIs()
    {
        byte[] image = StarField.CellImage(Seed);
        StarField.Star star = StarField.Stars(Seed)[2];
        int x = star.Id % 128, y = star.Id / 128 % 128, face = star.Id / 16384;
        int at = (y * 768 + face * 128 + x) * 4;

        Assert.Equal(Math.Round(star.Brightness * 255), image[at + 2]);
        Assert.Equal(StarField.Stars(Seed).Count, Enumerable.Range(0, image.Length / 4)
            .Count(i => image[i * 4 + 2] > 0));
    }

    [Fact]
    public void ColorOf_RunsFromRedThroughWhiteToBlue()
    {
        RgbColor red = StarField.ColorOf(0), white = StarField.ColorOf(0.5);
        RgbColor blue = StarField.ColorOf(1);

        Assert.True(red.R > red.B);
        Assert.Equal(new RgbColor(255, 255, 255), white);
        Assert.True(blue.B > blue.R);
    }

    [Fact]
    public void SeedFor_IsTheSameEveryTimeForAWorld()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        Assert.Equal(StarField.SeedFor(id), StarField.SeedFor(id));
        Assert.True(StarField.SeedFor(id) >= 0);
    }

    [Fact]
    public void ConstellationLines_PaintAlongTheLine_AndNothingElse()
    {
        IReadOnlyList<StarField.Star> stars = StarField.Stars(Seed);
        StarField.Star from = stars[0], to = stars[1];
        var constellation = new Constellation { Name = "Pair", Lines = [new(from.Id, to.Id)] };

        byte[] lines = ConstellationLines.Paint(Seed, [constellation]);

        Vector3D middle = from.Direction + to.Direction;
        Assert.True(Coverage(lines, middle * (1 / middle.Length)) > 200);
        Assert.Equal(0, Coverage(lines, from.Direction * -1));
    }

    [Fact]
    public void Problem_FindsBadConstellations()
    {
        IReadOnlyList<StarField.Star> stars = StarField.Stars(Seed);
        int a = stars[0].Id, b = stars[1].Id;

        Assert.Null(new Constellation { Name = "Fine", Lines = [new(a, b)] }.Problem(Seed));
        Assert.NotNull(new Constellation { Name = "Self", Lines = [new(a, a)] }.Problem(Seed));
        Assert.NotNull(new Constellation { Name = "Twice", Lines = [new(a, b), new(b, a)] }
            .Problem(Seed));
        Assert.NotNull(new Constellation { Name = "Empty cell", Lines = [new(a, 0)] }
            .Problem(Seed));
        Assert.NotNull(new Constellation { Name = " " }.Problem(Seed));
    }

    // How much line covers the pixel a direction falls in.
    private static byte Coverage(byte[] lines, Vector3D direction)
    {
        (int face, double a, double b) = StarField.FaceOf(direction);
        int size = ConstellationLines.FaceSize;
        int x = Math.Clamp((int)((a + 1) / 2 * size), 0, size - 1);
        int y = Math.Clamp((int)((b + 1) / 2 * size), 0, size - 1);
        return lines[y * 6 * size + face * size + x];
    }

    // The share of the sky (out of the whole) covered by the parts of the six cube faces where
    // both face coordinates are between `low` and `high` in size, found by adding up cells.
    private static double SkyShare(double low, double high)
    {
        double total = 0;
        const int steps = 400;
        for (int i = 0; i < steps; i++)
        {
            for (int j = 0; j < steps; j++)
            {
                double a = -1 + 2 * (i + 0.5) / steps, b = -1 + 2 * (j + 0.5) / steps;
                if (Math.Abs(a) >= low && Math.Abs(a) < high
                    && Math.Abs(b) >= low && Math.Abs(b) < high)
                {
                    total += Math.Pow(2.0 / steps, 2) / Math.Pow(1 + a * a + b * b, 1.5);
                }
            }
        }

        return total;
    }
}
