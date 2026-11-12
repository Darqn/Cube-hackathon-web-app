using Cube.Api.Common.Storage;
using Cube.Api.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cube.Tests;

public sealed class FileWriteBatchTests
{
    private sealed class Store : IPrivateFileStore
    {
        public Dictionary<string, byte[]> Contents { get; } = [];
        public string? FailOn { get; init; }
        public bool FailCleanup { get; init; }
        public Task WriteAsync(string key, byte[] bytes, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Contents[key] = bytes;
            if (key == FailOn) throw new IOException("partial write");
            return Task.CompletedTask;
        }
        public void Delete(string key)
        {
            if (FailCleanup) throw new IOException("cleanup failure");
            Contents.Remove(key);
        }
    }

    [Fact]
    public async Task Partial_write_is_removed_along_with_earlier_files()
    {
        var store = new Store { FailOn = "second" };
        var batch = new FileWriteBatch(store, NullLogger<FileWriteBatch>.Instance);
        var committed = false;
        await Assert.ThrowsAsync<IOException>(() => batch.SaveAsync([new("first", [1]), new("second", [2])], _ =>
        {
            committed = true;
            return Task.CompletedTask;
        }, default));
        Assert.Empty(store.Contents);
        Assert.False(committed);
    }

    [Fact]
    public async Task Failed_database_commit_removes_uploaded_content()
    {
        var store = new Store();
        var failure = new InvalidOperationException("commit failed");
        var batch = new FileWriteBatch(store, NullLogger<FileWriteBatch>.Instance);
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => batch.SaveAsync([new("file", [1, 2])], _ => throw failure, default));
        Assert.Same(failure, observed);
        Assert.Empty(store.Contents);
    }

    [Fact]
    public async Task Cleanup_failure_preserves_the_original_exception()
    {
        var store = new Store { FailCleanup = true };
        var failure = new InvalidOperationException("commit failed");
        var batch = new FileWriteBatch(store, NullLogger<FileWriteBatch>.Instance);
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => batch.SaveAsync([new("file", [1])], _ => throw failure, default));
        Assert.Same(failure, observed);
    }

    [Fact]
    public async Task Cancellation_during_commit_rolls_back_written_content()
    {
        var store = new Store();
        using var cancellation = new CancellationTokenSource();
        var batch = new FileWriteBatch(store, NullLogger<FileWriteBatch>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => batch.SaveAsync([new("file", [1])], token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, cancellation.Token));
        Assert.Empty(store.Contents);
    }

    [Fact]
    public async Task Successful_commit_retains_the_exact_content()
    {
        var store = new Store();
        await new FileWriteBatch(store, NullLogger<FileWriteBatch>.Instance).SaveAsync([new("file", [1, 2, 3])], _ => Task.CompletedTask, default);
        Assert.Equal(new byte[] { 1, 2, 3 }, store.Contents["file"]);
    }
}
