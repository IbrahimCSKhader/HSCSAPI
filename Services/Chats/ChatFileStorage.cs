using HSCSAPI.Models.Enums;

namespace HSCSAPI.Services.Chats;

public class ChatFileStorage : IChatFileStorage
{
    public const long MaxImageSize = 10 * 1024 * 1024;
    public const long MaxAudioSize = 25 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, FileRule> ImageRules =
        new Dictionary<string, FileRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = new(".jpg", IsJpeg),
            ["image/png"] = new(".png", IsPng),
            ["image/gif"] = new(".gif", IsGif),
            ["image/webp"] = new(".webp", IsWebp)
        };

    private static readonly IReadOnlyDictionary<string, FileRule> AudioRules =
        new Dictionary<string, FileRule>(StringComparer.OrdinalIgnoreCase)
        {
            ["audio/mpeg"] = new(".mp3", IsMp3),
            ["audio/wav"] = new(".wav", IsWav),
            ["audio/x-wav"] = new(".wav", IsWav),
            ["audio/ogg"] = new(".ogg", IsOgg),
            ["audio/webm"] = new(".webm", IsWebm),
            ["audio/mp4"] = new(".m4a", IsMp4),
            ["audio/aac"] = new(".aac", IsAac)
        };

    private readonly string _contentRootPath;
    private readonly string _chatsRootPath;

    public ChatFileStorage(IWebHostEnvironment environment)
    {
        _contentRootPath = Path.GetFullPath(environment.ContentRootPath);
        _chatsRootPath = Path.Combine(_contentRootPath, "wwwroot", "chats");
        Directory.CreateDirectory(_chatsRootPath);
    }

    public void EnsureChatDirectory(Guid chatId)
    {
        Directory.CreateDirectory(GetChatDirectory(chatId));
    }

    public async Task<StoredChatFile> SaveAsync(
        Guid chatId,
        Guid messageId,
        ChatMessageType messageType,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (messageType is not (ChatMessageType.Image or ChatMessageType.Audio))
        {
            throw new ArgumentException("Only image and audio messages can contain files.");
        }

        if (file.Length <= 0)
        {
            throw new ArgumentException("The uploaded file is empty.");
        }

        var maxSize = messageType == ChatMessageType.Image ? MaxImageSize : MaxAudioSize;
        if (file.Length > maxSize)
        {
            throw new ArgumentException(
                messageType == ChatMessageType.Image
                    ? "Images cannot exceed 10 MB."
                    : "Audio files cannot exceed 25 MB.");
        }

        var normalizedContentType = file.ContentType?.Split(';', 2)[0].Trim() ?? string.Empty;
        var rules = messageType == ChatMessageType.Image ? ImageRules : AudioRules;
        if (!rules.TryGetValue(normalizedContentType, out var rule))
        {
            throw new ArgumentException(
                messageType == ChatMessageType.Image
                    ? "Unsupported image type. Use JPEG, PNG, GIF, or WebP."
                    : "Unsupported audio type. Use MP3, WAV, OGG, WebM, M4A, or AAC.");
        }

        var header = new byte[16];
        await using (var validationStream = file.OpenReadStream())
        {
            var bytesRead = await validationStream.ReadAsync(header, cancellationToken);
            if (!rule.SignatureValidator(header.AsSpan(0, bytesRead)))
            {
                throw new ArgumentException("The file content does not match its declared content type.");
            }
        }

        EnsureChatDirectory(chatId);
        var storedFileName = $"{messageId:N}{rule.Extension}";
        var physicalPath = Path.Combine(GetChatDirectory(chatId), storedFileName);

        try
        {
            await using var destination = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await file.CopyToAsync(destination, cancellationToken);
        }
        catch
        {
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }

            throw;
        }

        var relativePath = Path.Combine("wwwroot", "chats", chatId.ToString("N"), storedFileName)
            .Replace('\\', '/');

        return new StoredChatFile(
            relativePath,
            Path.GetFileName(file.FileName),
            normalizedContentType,
            file.Length);
    }

    public void DeleteIfExists(string relativePath)
    {
        var physicalPath = ResolvePhysicalPath(relativePath);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }

    public string ResolvePhysicalPath(string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.GetFullPath(Path.Combine(_contentRootPath, normalizedRelativePath));
        var chatsRootWithSeparator = _chatsRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!physicalPath.StartsWith(chatsRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid chat file path.");
        }

        return physicalPath;
    }

    private string GetChatDirectory(Guid chatId) =>
        Path.Combine(_chatsRootPath, chatId.ToString("N"));

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

    private static bool IsGif(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8));

    private static bool IsWebp(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8);

    private static bool IsMp3(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && (bytes[..3].SequenceEqual("ID3"u8) || (bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0));

    private static bool IsWav(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WAVE"u8);

    private static bool IsOgg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && bytes[..4].SequenceEqual("OggS"u8);

    private static bool IsWebm(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && bytes[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 });

    private static bool IsMp4(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8);

    private static bool IsAac(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 2 && bytes[0] == 0xFF && (bytes[1] & 0xF6) == 0xF0;

    private delegate bool FileSignatureValidator(ReadOnlySpan<byte> bytes);

    private sealed record FileRule(string Extension, FileSignatureValidator SignatureValidator);
}
