using CaseInterview.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;

namespace CaseInterview.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageDirectory;

    public LocalFileStorageService(IHostEnvironment environment)
    {
        _storageDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "uploads", "books");
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<string> SaveFileAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default)
    {
        var sanitizedExt = Path.GetExtension(originalFileName);
        var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(originalFileName)}{sanitizedExt}";
        var fullPath = Path.Combine(_storageDirectory, uniqueFileName);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, cancellationToken);

        return fullPath;
    }

    public Task DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // File deletion failure shouldn't abort database transaction, can be cleaned by background job if needed
            }
        }
        return Task.CompletedTask;
    }

    public Stream OpenReadStream(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found at '{filePath}'");
        }

        return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }
}
