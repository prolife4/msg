using Max.BotClient.Types;

namespace Vmx.Messenger.Services.MAX;

/// <summary>
/// Определяет UploadType и Content-Type по расширению файла/URL.
/// </summary>
internal static class MaxAttachmentTypeResolver
{
    private static readonly Dictionary<string, (UploadType UploadType, string ContentType)> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = (UploadType.Image, "image/png"),
            [".jpg"] = (UploadType.Image, "image/jpeg"),
            [".jpeg"] = (UploadType.Image, "image/jpeg"),
            [".gif"] = (UploadType.Image, "image/gif"),
            [".webp"] = (UploadType.Image, "image/webp"),
            [".bmp"] = (UploadType.Image, "image/bmp"),

            [".mp4"] = (UploadType.Video, "video/mp4"),
            [".mov"] = (UploadType.Video, "video/quicktime"),
            [".webm"] = (UploadType.Video, "video/webm"),

            [".mp3"] = (UploadType.Audio, "audio/mpeg"),
            [".ogg"] = (UploadType.Audio, "audio/ogg"),
            [".wav"] = (UploadType.Audio, "audio/wav"),
        };

    private static readonly (UploadType UploadType, string ContentType) DefaultFile =
        (UploadType.File, "application/octet-stream");

    public static (UploadType UploadType, string ContentType) Resolve(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return DefaultFile;

        var ext = Path.GetExtension(GetPathPart(source));
        return Map.TryGetValue(ext, out var t) ? t : DefaultFile;
    }

    public static string ResolveFileName(string source)
    {
        var name = Path.GetFileName(GetPathPart(source));
        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }

    private static string GetPathPart(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.AbsolutePath;
        }
        return source;
    }
}