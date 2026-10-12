using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SaveTheDate.Data;
using SaveTheDate.Services;

namespace SaveTheDate.Tests;

internal class TestUser(string id = "owner") : ICurrentUser
{
    public string Id { get; set; } = id;
    public Task<string> GetIdAsync() => Task.FromResult(Id);
}

internal class TestDbFactory(string database) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={database};Pooling=False").Options);
    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
}

internal class TestEnvironment(string root) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "SaveTheDate";
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = root;
    public string WebRootPath { get; set; } = root;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
