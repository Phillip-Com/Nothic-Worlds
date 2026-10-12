using NothicWorlds.Core.Geometry;

namespace NothicWorlds.Core.Simulation;

/// <summary>
/// The wooden bridge a dirt path crosses a river on, and the boardwalk it crosses marsh on
/// (VISION.md BOD-13; owner's choices, 2026-10-12): a deck of planks laid across it on beams,
/// held up by pairs of posts at most <see cref="MaxBaySpanMeters"/> apart reaching down into
/// the bed or the ground; a bridge has a rail along each side, a boardwalk none. Built in the
/// bridge's own frame, in meters, as <see cref="StoneBridge"/> is.
/// </summary>
public static class WoodenBridge
{
    /// <summary>The farthest apart its pairs of posts are, in meters.</summary>
    public const double MaxBaySpanMeters = 4;

    /// <summary>How high over the water a bridge carries its deck, at least, in meters.</summary>
    public const double ClearanceMeters = 0.8;

    /// <summary>How high over the marsh a boardwalk's deck is, in meters.</summary>
    public const double BoardwalkMeters = 0.5;

    /// <summary>How high a bridge's rails are over its deck, in meters.</summary>
    public const double RailHeightMeters = 1;

    // How thick the deck's planks are, and the beams across under them at each pair of posts.
    private const double DeckMeters = 0.1, BeamMeters = 0.2, BeamWidthMeters = 0.25;

    // How thick the posts are, and the rails' posts and rails.
    private const double PostMeters = 0.25, RailPostMeters = 0.12, RailMeters = 0.1;

    // How far below the bed or ground the posts reach, in meters.
    private const double FootingMeters = 0.5;

    // The ways the planks' grain runs: across the deck, and along beams and rails, and up the
    // posts.
    private static readonly Vector3D _across = new(0, 1, 0), _along = new(1, 0, 0);
    private static readonly Vector3D _up = new(0, 0, 1);

    /// <summary>How many bays (spans between pairs of posts) a bridge this long has.</summary>
    public static int BayCount(double lengthMeters) =>
        Math.Max(1, (int)Math.Ceiling(lengthMeters / MaxBaySpanMeters - 1e-9));

    /// <summary>
    /// The bridge's wood: its triangles (as <see cref="StoneBridge.Build"/>'s) for a bridge
    /// <paramref name="lengthMeters"/> long and <paramref name="halfWidthMeters"/> to each
    /// side, its deck at <paramref name="startMeters"/> at the start and
    /// <paramref name="endMeters"/> at the end (straight between), its posts reaching down
    /// past <paramref name="footMeters"/> (the river's bed, or the marsh's ground), with rails
    /// if <paramref name="rails"/>.
    /// </summary>
    public static (List<BridgePoint> Points, List<int> Triangles) Build(double lengthMeters,
        double halfWidthMeters, double startMeters, double endMeters, double footMeters,
        bool rails)
    {
        var mesh = new BridgeMesh();
        double Deck(double x) => startMeters + (endMeters - startMeters)
            * (lengthMeters > 0 ? x / lengthMeters : 0);
        double foot = footMeters - FootingMeters;
        double half = halfWidthMeters;

        // The deck: planks across it, its top the deck's height.
        mesh.Box(0, lengthMeters, -half, half, Deck(0) - DeckMeters,
            Deck(lengthMeters) - DeckMeters, Deck(0), Deck(lengthMeters), BridgeMaterial.Wood,
            _across);

        int bays = BayCount(lengthMeters);
        for (int b = 0; b <= bays; b++)
        {
            // A beam across under the deck, on a pair of posts; inset from the ends.
            double x = Math.Clamp(lengthMeters * b / bays, BeamWidthMeters / 2,
                Math.Max(BeamWidthMeters / 2, lengthMeters - BeamWidthMeters / 2));
            double under = Deck(x) - DeckMeters;
            mesh.Box(x - BeamWidthMeters / 2, x + BeamWidthMeters / 2, -half, half,
                under - BeamMeters, under - BeamMeters, under, under, BridgeMaterial.Wood,
                _across);
            foreach (double side in new[] { -1.0, 1.0 })
            {
                double y = side * Math.Max(0, half - PostMeters);
                double top = under - BeamMeters;
                if (top > foot)
                {
                    mesh.Box(x - PostMeters / 2, x + PostMeters / 2, y - PostMeters / 2,
                        y + PostMeters / 2, foot, foot, top, top, BridgeMaterial.Wood, _up);
                }

                if (rails)
                {
                    double ry = side * (half - RailPostMeters / 2);
                    mesh.Box(x - RailPostMeters / 2, x + RailPostMeters / 2,
                        ry - RailPostMeters / 2, ry + RailPostMeters / 2, Deck(x), Deck(x),
                        Deck(x) + RailHeightMeters, Deck(x) + RailHeightMeters,
                        BridgeMaterial.Wood, _up);
                }
            }
        }

        if (rails)
        {
            foreach (double side in new[] { -1.0, 1.0 })
            {
                double ry = side * (half - RailPostMeters / 2);
                double startTop = Deck(0) + RailHeightMeters;
                double endTop = Deck(lengthMeters) + RailHeightMeters;
                mesh.Box(0, lengthMeters, ry - RailMeters / 2, ry + RailMeters / 2,
                    startTop - RailMeters, endTop - RailMeters, startTop, endTop,
                    BridgeMaterial.Wood, _along);
            }
        }

        return (mesh.Points, mesh.Triangles);
    }
}
