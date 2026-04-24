// ✅ FIXED: IsValidContentType validation mapping
// ✅ FIX 1: Added GetFileStreamAsync to return Stream instead of byte[] for large files
// ✅ FIX 3: Null-safe upload path (fallback to ContentRootPath/wwwroot)
// ✅ FIX 4: Check size before streaming/reading (500MB limit)
// ✅ FIXED: Regex pattern string format inconsistency

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PastPort.Application.Interfaces;

namespace PastPort.Infrastructure.ExternalServices.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _uploadPath;

    private const long MaxTotalUploadSize = 1000 * 1024 * 1024; // 1GB

    // ✅ FIXED: نقلنا validTypes لـ static readonly field 
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

    public LocalFileStorageService(
        IWebHostEnvironment environment,
        ILogger<LocalFileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;

        // ✅ FIX 3: Fallback if WebRootPath is null (e.g., no wwwroot in Docker/Linux)
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        _uploadPath = Path.Combine(webRoot, "uploads");

        if (!Directory.Exists(_uploadPath))
        {
            Directory.CreateDirectory(_uploadPath);
            _logger.LogInformation("Created uploads directory: {UploadPath}", _uploadPath);
        }
    }

    /// <summary>
    /// رفع ملف بأمان
    /// </summary>
    public async Task<string> UploadFileAsync(IFormFile file, string folder)
    {
        try
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("الملف فارغ");

            var folderPath = Path.Combine(_uploadPath, folder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var fileName = GenerateUniqueFileName(file.FileName);
            var filePath = Path.Combine(folderPath, fileName);

            // حماية من Path Traversal
            var fullPath = Path.GetFullPath(filePath);
            var fullUploadPath = Path.GetFullPath(_uploadPath);

            if (!fullPath.StartsWith(fullUploadPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("محاولة رفع ملف خارج المسار المسموح");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileUrl = $"/uploads/{folder}/{fileName}";

            _logger.LogInformation(
                "تم رفع الملف بنجاح: {FileName}, الحجم: {FileSize} bytes",
                fileName, file.Length);

            return fileUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في رفع الملف: {FileName}", file.FileName);
            throw new Exception("فشل رفع الملف", ex);
        }
    }

    /// <summary>
    /// حذف ملف بأمان
    /// </summary>
    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return false;

                var relativePath = fileUrl.Replace("/uploads/", "")
                    .Replace('/', Path.DirectorySeparatorChar);
                var filePath = Path.Combine(_uploadPath, relativePath);

                var fullPath = Path.GetFullPath(filePath);
                var fullUploadPath = Path.GetFullPath(_uploadPath);

                if (!fullPath.StartsWith(fullUploadPath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("محاولة حذف ملف خارج المسار المسموح: {FilePath}", filePath);
                    return false;
                }

                if (!File.Exists(filePath))
                    return false;

                File.Delete(filePath);
                _logger.LogInformation("تم حذف الملف: {FileUrl}", fileUrl);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف الملف: {FileUrl}", fileUrl);
                return false;
            }
        });
    }

    /// <summary>
    /// ✅ FIX 1 & 4: الحصول على محتوى الملف كـ Stream للملفات الكبيرة (مثل 3D Models)
    /// </summary>
    public Task<Stream> GetFileStreamAsync(string fileUrl)
    {
        var relativePath = fileUrl.Replace("/uploads/", "")
            .Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(_uploadPath, relativePath);

        var fullPath = Path.GetFullPath(filePath);
        var fullUploadPath = Path.GetFullPath(_uploadPath);

        if (!fullPath.StartsWith(fullUploadPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("محاولة الوصول إلى ملف خارج المسار المسموح");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"الملف غير موجود: {fileUrl}");

        // ✅ FIX 4: Check size before streaming
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > 500 * 1024 * 1024) // 500MB hard limit
            throw new InvalidOperationException("File too large to serve");

        return Task.FromResult<Stream>(new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true));
    }

    /// <summary>
    /// الحصول على محتوى الملف بالكامل (للملفات الصغيرة فقط)
    /// </summary>
    public async Task<byte[]> GetFileAsync(string fileUrl)
    {
        try
        {
            var relativePath = fileUrl.Replace("/uploads/", "")
                .Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(_uploadPath, relativePath);

            var fullPath = Path.GetFullPath(filePath);
            var fullUploadPath = Path.GetFullPath(_uploadPath);

            if (!fullPath.StartsWith(fullUploadPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("محاولة الوصول إلى ملف خارج المسار المسموح");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"الملف غير موجود: {fileUrl}");

            // ✅ إضافة حماية هنا أيضاً للملفات الضخمة حتى لو تم استدعاؤها بالخطأ
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 50 * 1024 * 1024) // منع تحميل أكثر من 50 ميجا كـ ByteArray
                throw new InvalidOperationException("الملف كبير جداً للقراءة في الذاكرة بالكامل. استخدم GetFileStreamAsync بدلاً من ذلك.");

            return await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في الحصول على الملف: {FileUrl}", fileUrl);
            throw;
        }
    }

    /// <summary>
    /// التحقق من صحة الملف
    /// </summary>
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
                _logger.LogWarning(
                    "حجم الملف يتجاوز الحد المسموح: {FileSize} > {MaxSize}",
                    file.Length, maxSizeInBytes);
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
                _logger.LogWarning(
                    "نوع محتوى غير متطابق: {ContentType} للامتداد {Extension}",
                    file.ContentType, extension);
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

    /// <summary>
    /// حذف مجلد كامل
    /// </summary>
    public async Task<bool> DeleteFolderAsync(string folder)
    {
        return await Task.Run(() =>
        {
            try
            {
                var folderPath = Path.Combine(_uploadPath, folder);

                var fullPath = Path.GetFullPath(folderPath);
                var fullUploadPath = Path.GetFullPath(_uploadPath);

                if (!fullPath.StartsWith(fullUploadPath, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!Directory.Exists(folderPath))
                    return false;

                Directory.Delete(folderPath, true);
                _logger.LogInformation("تم حذف المجلد: {Folder}", folder);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف المجلد: {Folder}", folder);
                return false;
            }
        });
    }

    /// <summary>
    /// الحصول على حجم الملف
    /// </summary>
    public long GetFileSize(string fileUrl)
    {
        try
        {
            var relativePath = fileUrl.Replace("/uploads/", "")
                .Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(_uploadPath, relativePath);

            return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في الحصول على حجم الملف");
            return 0;
        }
    }

    /// <summary>
    /// التحقق من وجود الملف
    /// </summary>
    public bool FileExists(string fileUrl)
    {
        try
        {
            var relativePath = fileUrl.Replace("/uploads/", "")
                .Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(_uploadPath, relativePath);
            return File.Exists(filePath);
        }
        catch
        {
            return false;
        }
    }

    // ==================== Private Helpers ====================

    private static string GenerateUniqueFileName(string originalFileName)
    {
        var fileExtension = Path.GetExtension(originalFileName);
        var fileName = Path.GetFileNameWithoutExtension(originalFileName);

        // ✅ FIXED: تم إزالة @ لتجنب أي مشاكل في الـ Regex وتوحيد الشكل
        var safeName = System.Text.RegularExpressions.Regex.Replace(
            fileName, "[^a-zA-Z0-9_-]", "_");

        return $"{safeName}_{Guid.NewGuid().ToString()[..8]}{fileExtension}";
    }

    private static bool IsValidContentType(string? contentType, string extension)
    {
        if (!_validContentTypes.ContainsKey(extension))
            return false;

        if (string.IsNullOrEmpty(contentType))
            return false;

        return _validContentTypes[extension].Contains(
            contentType.ToLowerInvariant());
    }
}