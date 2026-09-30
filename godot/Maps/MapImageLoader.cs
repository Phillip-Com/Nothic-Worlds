using Godot;
using NothicWorlds.Core.Maps;
using NothicWorlds.Core.Storage;

namespace NothicWorlds.Maps;

/// <summary>
/// Loads a map image from disk and prepares it for the planet (VISION.md MAP-01). It checks the
/// image against <see cref="MapImageRules"/>, shrinks it if it's too large, builds mipmaps
/// (smaller copies the GPU uses when zoomed out), and compresses it to save graphics memory. The
/// slow work runs on a background thread so the app stays responsive.
/// </summary>
public static class MapImageLoader
{
    /// <summary>
    /// Loads and prepares the image at <paramref name="path"/>. Must be called from the main
    /// thread, which is where the result is returned.
    /// </summary>
    /// <exception cref="MapLoadException">
    /// The file is missing, unsupported, too large, or unreadable.
    /// </exception>
    public static async Task<LoadedMap> LoadAsync(string path)
    {
        ValidateFile(path);
        return await LoadAsync(new FileAssetSource(path), path);
    }

    /// <summary>
    /// Loads and prepares a map image from any source, e.g. an image stored inside a world file.
    /// Must be called from the main thread, which is where the result is returned.
    /// </summary>
    /// <param name="source">Where to read the image's bytes.</param>
    /// <param name="name">
    /// The image's file or asset name; its extension says how to decode it.
    /// </param>
    /// <exception cref="MapLoadException">The image is unsupported or unreadable.</exception>
    public static async Task<LoadedMap> LoadAsync(IAssetSource source, string name)
    {
        string extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        (Image image, MapImageCheck check) =
            await Task.Run(() => Prepare(Decode(ReadAll(source), extension)));

        // Back on the main thread (Godot resumes awaits there): upload to the GPU, then free the
        // CPU copy right away instead of waiting for .NET's garbage collector (it can be
        // hundreds of MB).
        using (image)
        {
            return new LoadedMap(ImageTexture.CreateFromImage(image), check);
        }
    }

    private static void ValidateFile(string path)
    {
        string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if (!MapImageRules.SupportedExtensions.Contains(extension))
        {
            string supported =
                string.Join(", ", MapImageRules.SupportedExtensions).ToUpperInvariant();
            throw new MapLoadException($"only {supported} images are supported.");
        }

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new MapLoadException("the file doesn't exist.");
        }

        if (file.Length > MapImageRules.MaxFileBytes)
        {
            throw new MapLoadException(
                $"the file is {file.Length / (1024 * 1024)} MB. The limit is " +
                $"{MapImageRules.MaxFileBytes / (1024 * 1024)} MB.");
        }
    }

    private static byte[] ReadAll(IAssetSource source)
    {
        try
        {
            using Stream stream = source.OpenRead();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or WorldFileException)
        {
            throw new MapLoadException($"the image couldn't be read ({error.Message}).");
        }
    }

    private static Image Decode(byte[] bytes, string extension)
    {
        var image = new Image();
        Error result = extension switch
        {
            "png" => image.LoadPngFromBuffer(bytes),
            "jpg" or "jpeg" => image.LoadJpgFromBuffer(bytes),
            "webp" => image.LoadWebpFromBuffer(bytes),
            _ => Error.FileUnrecognized,
        };

        if (result != Error.Ok || image.IsEmpty())
        {
            image.Dispose();
            throw new MapLoadException(
                "the image couldn't be read. It may be damaged or not really that file type.");
        }

        return image;
    }

    private static (Image Image, MapImageCheck Check) Prepare(Image image)
    {
        MapImageCheck check = MapImageRules.Check(image.GetWidth(), image.GetHeight());
        if (check.NeedsResize)
        {
            ShrinkTo(image, check.TargetWidth, check.TargetHeight);
        }

        Error mipmapResult = image.GenerateMipmaps();
        if (mipmapResult != Error.Ok)
        {
            throw new MapLoadException($"the image couldn't be prepared ({mipmapResult}).");
        }

        // S3TC compression (owner decision): an 8k map uses ~137 MB of graphics memory instead
        // of ~497 MB on the baseline laptop, at the cost of ~2 s extra load time and slight
        // blockiness on fine detail. If it fails, keep the uncompressed image instead of failing
        // the import.
        Error compressResult = image.Compress(Image.CompressMode.S3Tc, Image.CompressSource.Srgb);
        if (compressResult != Error.Ok)
        {
            GD.PushWarning($"Map compression failed ({compressResult}); using it uncompressed.");
        }

        return (image, check);
    }

    private static void ShrinkTo(Image image, int width, int height)
    {
        // Halving (averaging each 2×2 block) is fast and high quality, so do that while the
        // image is at least twice the target size. Then finish with one smaller resize. Resizing
        // a 16k image in a single step took ~20 s on the baseline laptop.
        while (image.GetWidth() >= width * 2 && image.GetHeight() >= height * 2)
        {
            image.ShrinkX2();
        }

        if (image.GetWidth() != width || image.GetHeight() != height)
        {
            image.Resize(width, height, Image.Interpolation.Cubic);
        }
    }
}
