using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TraceCore.Application.Common.Interfaces;

namespace TraceCore.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _storageRoot;

    public LocalFileStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:LocalPath"] ?? "App_Data/Storage";
        _storageRoot = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);

        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        string safeExtension = Path.GetExtension(fileName).ToLowerInvariant();
        string safeName = $"{Guid.NewGuid()}{safeExtension}";
        string fullPath = Path.Combine(_storageRoot, safeName);

        // Security check: ensure path is within storage root
        if (!fullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path traversal detected.");
        }

        using var destination = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(destination, cancellationToken);

        return safeName;
    }

    public Task<Stream> GetFileStreamAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.Combine(_storageRoot, Path.GetFileName(storagePath));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Requested file was not found.", storagePath);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.Combine(_storageRoot, Path.GetFileName(storagePath));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public async Task<string> ComputeSha256Async(Stream fileStream, CancellationToken cancellationToken = default)
    {
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        byte[] hash = await SHA256.HashDataAsync(fileStream, cancellationToken);

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        return Convert.ToHexString(hash);
    }
}
