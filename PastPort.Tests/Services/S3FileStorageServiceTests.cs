using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PastPort.Infrastructure.ExternalServices.Storage;

namespace PastPort.Tests.Services;

public class S3FileStorageServiceTests
{
    private readonly Mock<IAmazonS3> _s3Mock;
    private readonly S3FileStorageService _service;

    public S3FileStorageServiceTests()
    {
        _s3Mock = new Mock<IAmazonS3>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["S3:BucketName"] = "pastpost-434676049576-eu-north-1-an"
            })
            .Build();
        var logger = Mock.Of<ILogger<S3FileStorageService>>();
        _service = new S3FileStorageService(_s3Mock.Object, config, logger);
    }

    [Fact]
    public async Task UploadFileAsync_ShouldReturnFileUrl_OnSuccess()
    {
        _s3Mock.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
               .ReturnsAsync(new PutObjectResponse { ETag = "\"abc123\"" });

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("test.png");
        file.Setup(f => f.ContentType).Returns("image/png");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());
        file.Setup(f => f.Length).Returns(100);

        var result = await _service.UploadFileAsync(file.Object, "avatars");

        Assert.StartsWith("/uploads/avatars/", result);
    }

    [Fact]
    public async Task UploadFileAsync_ShouldThrow_WhenFileIsNull()
    {
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            _service.UploadFileAsync(null!, "avatars"));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldReturnTrue_OnSuccess()
    {
        _s3Mock.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), default))
               .ReturnsAsync(new DeleteObjectResponse { HttpStatusCode = System.Net.HttpStatusCode.NoContent });

        var result = await _service.DeleteFileAsync("/uploads/avatars/test.png");

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldReturnFalse_WhenUrlIsEmpty()
    {
        var result = await _service.DeleteFileAsync("");

        Assert.False(result);
    }

    [Fact]
    public async Task GetFileAsync_ShouldReturnBytes_OnSuccess()
    {
        var responseStream = new MemoryStream("hello"u8.ToArray());
        var getResponse = new GetObjectResponse
        {
            ResponseStream = responseStream,
            HttpStatusCode = System.Net.HttpStatusCode.OK
        };
        _s3Mock.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
               .ReturnsAsync(getResponse);

        var result = await _service.GetFileAsync("/uploads/avatars/test.png");

        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetFileStreamAsync_ShouldReturnStream_OnSuccess()
    {
        var responseStream = new MemoryStream("hello"u8.ToArray());
        var getResponse = new GetObjectResponse
        {
            ResponseStream = responseStream,
            HttpStatusCode = System.Net.HttpStatusCode.OK
        };
        _s3Mock.Setup(x => x.GetObjectAsync(It.IsAny<GetObjectRequest>(), default))
               .ReturnsAsync(getResponse);

        var result = await _service.GetFileStreamAsync("/uploads/avatars/test.png");

        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateFile_ShouldReturnTrue_WhenFileIsValid()
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("test.png");
        file.Setup(f => f.ContentType).Returns("image/png");
        file.Setup(f => f.Length).Returns(1024);

        var result = _service.ValidateFile(file.Object, [".png"], 5 * 1024 * 1024);

        Assert.True(result);
    }

    [Fact]
    public void ValidateFile_ShouldReturnFalse_WhenExtensionNotAllowed()
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("test.exe");
        file.Setup(f => f.ContentType).Returns("application/x-msdownload");
        file.Setup(f => f.Length).Returns(1024);

        var result = _service.ValidateFile(file.Object, [".png"], 5 * 1024 * 1024);

        Assert.False(result);
    }

    [Fact]
    public void FileExists_ShouldReturnTrue_WhenFileExists()
    {
        _s3Mock.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
               .ReturnsAsync(new GetObjectMetadataResponse { HttpStatusCode = System.Net.HttpStatusCode.OK });

        var result = _service.FileExists("/uploads/avatars/test.png");

        Assert.True(result);
    }

    [Fact]
    public void FileExists_ShouldReturnFalse_WhenFileNotFound()
    {
        _s3Mock.Setup(x => x.GetObjectMetadataAsync(It.IsAny<GetObjectMetadataRequest>(), default))
               .ThrowsAsync(new AmazonS3Exception("Not found") { StatusCode = System.Net.HttpStatusCode.NotFound });

        var result = _service.FileExists("/uploads/avatars/test.png");

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteFolderAsync_ShouldReturnTrue_WhenFolderExists()
    {
        _s3Mock.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
               .ReturnsAsync(new ListObjectsV2Response
               {
                   S3Objects = [new S3Object { Key = "uploads/avatars/test.png" }]
               });
        _s3Mock.Setup(x => x.DeleteObjectsAsync(It.IsAny<DeleteObjectsRequest>(), default))
               .ReturnsAsync(new DeleteObjectsResponse());

        var result = await _service.DeleteFolderAsync("avatars");

        Assert.True(result);
    }

    [Fact]
    public async Task DeleteFolderAsync_ShouldReturnFalse_WhenFolderEmpty()
    {
        _s3Mock.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
               .ReturnsAsync(new ListObjectsV2Response { S3Objects = [] });

        var result = await _service.DeleteFolderAsync("avatars");

        Assert.False(result);
    }
}
