using System.Runtime.InteropServices;
using Godot;

namespace NothicWorlds.Rendering;

/// <summary>
/// A mesh surface of positions, CUSTOM0 and CUSTOM1 (four floats each), and triangles, packed
/// on a worker thread the way the engine keeps it, so handing it over on the main thread only
/// copies it to the GPU. Handed over as arrays (ArrayMesh.AddSurfaceFromArrays), the engine
/// packs it there itself, which for the standing view's ground (GroundTiles) paused the main
/// thread for tens of milliseconds. Whether the engine keeps surfaces this way is checked once
/// against one it packs itself (<see cref="Works"/>); if not, callers hand over arrays.
/// </summary>
internal sealed class PackedSurface
{
    /// <summary>The format the surfaces are made in (both CUSTOM channels four floats).</summary>
    public static readonly Mesh.ArrayFormat Format = (Mesh.ArrayFormat)(
        ((int)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom0Shift)
        | ((int)Mesh.ArrayCustomFormat.RgbaFloat << (int)Mesh.ArrayFormat.FormatCustom1Shift));

    // The most points whose triangles the engine keeps in 16-bit indices.
    private const int MostShortIndexed = 1 << 16;

    private static bool? _works;
    private static long _engineFormat;

    private readonly byte[] _vertices, _attributes, _indices;
    private readonly int _vertexCount, _indexCount;
    private readonly Aabb _bounds;

    private PackedSurface(byte[] vertices, byte[] attributes, byte[] indices, int vertexCount,
        int indexCount, Aabb bounds)
    {
        (_vertices, _attributes, _indices) = (vertices, attributes, indices);
        (_vertexCount, _indexCount, _bounds) = (vertexCount, indexCount, bounds);
    }

    /// <summary>
    /// Whether surfaces packed here can be handed to the engine as they are: checked once (on
    /// the main thread) against a small one the engine packs itself.
    /// </summary>
    public static bool Works => _works ??= Check();

    /// <summary>
    /// Packs a surface: <paramref name="custom0"/> and <paramref name="custom1"/> have four
    /// floats a point. Safe on any thread.
    /// </summary>
    public static PackedSurface Pack(Vector3[] positions, float[] custom0, float[] custom1,
        int[] indices)
    {
        // Positions in one stream; CUSTOM0 then CUSTOM1 for each point in another.
        byte[] vertices = MemoryMarshal.AsBytes(positions.AsSpan()).ToArray();
        var attributes = new float[positions.Length * 8];
        for (int i = 0; i < positions.Length; i++)
        {
            custom0.AsSpan(i * 4, 4).CopyTo(attributes.AsSpan(i * 8, 4));
            custom1.AsSpan(i * 4, 4).CopyTo(attributes.AsSpan(i * 8 + 4, 4));
        }

        byte[] indexBytes;
        if (positions.Length <= MostShortIndexed)
        {
            var shortIndices = new ushort[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                shortIndices[i] = (ushort)indices[i];
            }

            indexBytes = MemoryMarshal.AsBytes(shortIndices.AsSpan()).ToArray();
        }
        else
        {
            indexBytes = MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        }

        Vector3 lowest = positions[0], highest = positions[0];
        foreach (Vector3 position in positions)
        {
            (lowest, highest) = (lowest.Min(position), highest.Max(position));
        }

        return new PackedSurface(vertices, MemoryMarshal.AsBytes(attributes.AsSpan()).ToArray(),
            indexBytes, positions.Length, indices.Length, new Aabb(lowest, highest - lowest));
    }

    /// <summary>The surface as a mesh (on the main thread, once <see cref="Works"/>).</summary>
    public ArrayMesh ToMesh()
    {
        var mesh = new ArrayMesh();
        mesh.Set("_surfaces", new Godot.Collections.Array { ToDictionary() });
        return mesh;
    }

    // The surface as the engine describes one (ArrayMesh's stored surfaces).
    private Godot.Collections.Dictionary ToDictionary() => new()
    {
        ["format"] = _engineFormat,
        ["primitive"] = (int)Mesh.PrimitiveType.Triangles,
        ["vertex_data"] = _vertices,
        ["vertex_count"] = _vertexCount,
        ["attribute_data"] = _attributes,
        ["aabb"] = _bounds,
        ["uv_scale"] = Vector4.Zero,
        ["index_data"] = _indices,
        ["index_count"] = _indexCount,
    };

    // Packs a small surface both ways and compares them, byte for byte.
    private static bool Check()
    {
        try
        {
            Vector3[] positions = [new(0.5f, 2, 3), new(1.5f, -2, 3), new(2.5f, 2, -3)];
            float[] custom0 = [10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21];
            float[] custom1 = [30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41];
            int[] indices = [0, 1, 2];
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = positions;
            arrays[(int)Mesh.ArrayType.Custom0] = custom0;
            arrays[(int)Mesh.ArrayType.Custom1] = custom1;
            arrays[(int)Mesh.ArrayType.Index] = indices;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, flags: Format);
            var engine = mesh.Get("_surfaces").AsGodotArray()[0].AsGodotDictionary();
            PackedSurface ours = Pack(positions, custom0, custom1, indices);
            _engineFormat = engine["format"].AsInt64();
            bool same = engine["vertex_data"].AsByteArray().AsSpan().SequenceEqual(ours._vertices)
                && engine["attribute_data"].AsByteArray().AsSpan().SequenceEqual(ours._attributes)
                && engine["index_data"].AsByteArray().AsSpan().SequenceEqual(ours._indices)
                && engine["vertex_count"].AsInt32() == ours._vertexCount
                && engine["index_count"].AsInt32() == ours._indexCount;
            if (!same)
            {
                GD.PushWarning("The engine packs meshes differently than expected: the standing "
                    + "view's ground is handed over the slower way.");
            }

            return same;
        }
        catch (Exception error)
        {
            GD.PushWarning("Couldn't check how the engine packs meshes; the standing view's "
                + $"ground is handed over the slower way: {error.Message}");
            return false;
        }
    }
}
