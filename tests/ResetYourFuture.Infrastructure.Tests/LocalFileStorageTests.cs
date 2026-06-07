using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ResetYourFuture.Web.ApiServices;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Infrastructure.Tests;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _root = Path.Combine( Path.GetTempPath(), "ryf-tests-" + Guid.NewGuid().ToString( "N" ) );
        Directory.CreateDirectory( _root );
        var env = Substitute.For<IWebHostEnvironment>();
        env.ContentRootPath.Returns( _root );
        _storage = new LocalFileStorage( env, NullLogger<LocalFileStorage>.Instance );
    }

    private static MemoryStream Stream( int bytes = 16 ) => new( new byte[bytes] );

    [Fact]
    public async Task SaveFile_WritesUniqueNameAndReturnsRelativePath()
    {
        var path = await _storage.SaveFileAsync( Stream(), "photo.png", "avatars" );

        path.ShouldStartWith( "avatars/" );
        path.ShouldEndWith( ".png" );
        _storage.FileExists( path ).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveFile_BlankFileName_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _storage.SaveFileAsync( Stream(), "   ", "avatars" ) );
    }

    [Fact]
    public async Task SaveFile_FolderWithTraversal_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _storage.SaveFileAsync( Stream(), "a.png", "../escape" ) );
    }

    [Fact]
    public async Task SaveFile_ExceedsExplicitMax_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _storage.SaveFileAsync( Stream( 100 ), "a.png", "avatars", maxBytes: 10 ) );
    }

    [Fact]
    public async Task SaveFile_UnknownFolderWithoutCap_Throws()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _storage.SaveFileAsync( Stream(), "a.bin", "mystery-folder" ) );
    }

    [Fact]
    public async Task SaveFile_FolderHeuristicWithinCap_Succeeds()
    {
        var path = await _storage.SaveFileAsync( Stream( 1024 ), "doc.pdf", "lessons/pdf" );

        _storage.FileExists( path ).ShouldBeTrue();
    }

    [Fact]
    public async Task GetFile_Missing_Throws()
    {
        await Should.ThrowAsync<FileNotFoundException>(
            () => _storage.GetFileAsync( "avatars/missing.png" ) );
    }

    [Fact]
    public async Task GetFile_InvalidPath_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _storage.GetFileAsync( "../secret" ) );
    }

    [Fact]
    public async Task GetFile_ReturnsStreamAndContentTypeByExtension()
    {
        var path = await _storage.SaveFileAsync( Stream(), "image.png", "avatars" );

        var (stream, contentType) = await _storage.GetFileAsync( path );
        await using ( stream )
        {
            contentType.ShouldBe( "image/png" );
        }
    }

    [Fact]
    public async Task DeleteFile_RemovesExisting_AndIgnoresMissing()
    {
        var path = await _storage.SaveFileAsync( Stream(), "x.png", "avatars" );

        await _storage.DeleteFileAsync( path );
        _storage.FileExists( path ).ShouldBeFalse();

        await Should.NotThrowAsync( () => _storage.DeleteFileAsync( path ) ); // already gone
    }

    [Fact]
    public void FileExists_InvalidPath_ReturnsFalse()
    {
        _storage.FileExists( "../nope" ).ShouldBeFalse();
    }

    public void Dispose()
    {
        if ( Directory.Exists( _root ) )
            Directory.Delete( _root, recursive: true );
    }
}
