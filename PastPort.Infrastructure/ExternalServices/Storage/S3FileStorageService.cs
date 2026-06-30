using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PastPort.Application.Interfaces;

namespace PastPort.Infrastructure.ExternalServices.Storage;

public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3FileStorageService> _logger;
    private readonly string _bucketName;

    private static readonly Dictionary<string, string[]> _validContentTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { ".jpg",  new[] { "image/jpeg", "image/jpg" } },
            { ".jpeg", new[] { "image/jpeg", "image/jpg" } },
            { ".png",  new[] { "image/png" } },
            { ".gif",  new[] { "image/gif" } },
            { ".webp", new[] { "image/webp" } },
            { ".mp3",  new[] { "audio/mpeg", "audio/mp3" } },
            { ".wav",  new[] { "audio/wav", "audio/x-wav" } },
            { ".ogg",  new[] { "audio/ogg" } },
            { ".glb",  new[] { "model/gltf-binary", "application/octet-stream" } },
            { ".gltf", new[] { "model/gltf+json", "application/json" } },
            { ".obj",  new[] { "model/obj", "application/octet-stream" } },
            { ".fbx",  new[] { "application/octet-stream" } },
            { ".pdf",  new[] { "application/pdf" } },
        };

    public S3FileStorageService(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3FileStorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        _bucketName = configuration["S3:BucketName"]!;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folder)
    {
        try
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("الملف فارغ");

            var fileName = GenerateUniqueFileName(file.FileName);
            var key = $"uploads/{folder}/{fileName}";

            using var stream = file.OpenReadStream();
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            await _s3Client.PutObjectAsync(request);

            _logger.LogInformation("تم رفع الملف بنجاح إلى S3: {Key}, الحجم: {FileSize} bytes", key, file.Length);

            return $"/{key}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في رفع الملف إلى S3: {FileName}", file?.FileName ?? "null");
            throw new Exception("فشل رفع الملف", ex);
        }
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(fileUrl))
                return false;

            var key = fileUrl.TrimStart('/');
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request);
            _logger.LogInformation("تم حذف الملف من S3: {Key}", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في حذف الملف من S3: {FileUrl}", fileUrl);
            return false;
        }
    }

    public async Task<byte[]> GetFileAsync(string fileUrl)
    {
        try
        {
            var key = fileUrl.TrimStart('/');
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في الحصول على الملف من S3: {FileUrl}", fileUrl);
            throw;
        }
    }

    public async Task<Stream> GetFileStreamAsync(string fileUrl)
    {
        try
        {
            var key = fileUrl.TrimStart('/');
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request);
            return response.ResponseStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في الحصول على تدفق الملف من S3: {FileUrl}", fileUrl);
            throw;
        }
    }

    public bool ValidateFile(IFormFile file, string[] allowedExtensions, long maxSizeInBytes)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("محاولة رفع ملف فارغ");
                return false;
            }

            if (file.Length > maxSizeInBytes)
            {
                _logger.LogWarning("حجم الملف يتجاوز الحد المسموح: {FileSize} > {MaxSize}", file.Length, maxSizeInBytes);
                return false;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogWarning("امتداد ملف غير مسموح: {Extension}", extension);
                return false;
            }

            if (!IsValidContentType(file.ContentType, extension))
            {
                _logger.LogWarning("نوع محتوى غير متطابق: {ContentType} للامتداد {Extension}", file.ContentType, extension);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في التحقق من الملف");
            return false;
        }
    }

    public async Task<bool> DeleteFolderAsync(string folder)
    {
        try
        {
            var prefix = $"uploads/{folder}/";
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var listResponse = await _s3Client.ListObjectsV2Async(listRequest);

            if (!listResponse.S3Objects.Any())
                return false;

            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = _bucketName,
                Objects = listResponse.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
            };

            await _s3Client.DeleteObjectsAsync(deleteRequest);
            _logger.LogInformation("تم حذف المجلد: {Folder}", folder);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في حذف المجلد: {Folder}", folder);
            return false;
        }
    }

    public long GetFileSize(string fileUrl)
    {
        try
        {
            var key = fileUrl.TrimStart('/');
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = _s3Client.GetObjectMetadataAsync(request).GetAwaiter().GetResult();
            return response.ContentLength;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في الحصول على حجم الملف: {FileUrl}", fileUrl);
            return 0;
        }
    }

    public bool FileExists(string fileUrl)
    {
        try
        {
            var key = fileUrl.TrimStart('/');
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = _s3Client.GetObjectMetadataAsync(request).GetAwaiter().GetResult();
            return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في التحقق من وجود الملف: {FileUrl}", fileUrl);
            return false;
        }
    }

    private static string GenerateUniqueFileName(string originalFileName)
    {
        var fileExtension = Path.GetExtension(originalFileName);
        var fileName = Path.GetFileNameWithoutExtension(originalFileName);
        var safeName = System.Text.RegularExpressions.Regex.Replace(fileName, "[^a-zA-Z0-9_-]", "_");
        return $"{safeName}_{Guid.NewGuid().ToString()[..8]}{fileExtension}";
    }

    private static bool IsValidContentType(string? contentType, string extension)
    {
        if (!_validContentTypes.ContainsKey(extension))
            return false;

        if (string.IsNullOrEmpty(contentType))
            return false;

        return _validContentTypes[extension].Contains(contentType.ToLowerInvariant());
    }
}
