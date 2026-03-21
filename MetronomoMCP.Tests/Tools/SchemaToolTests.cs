using FluentAssertions;
using MetronomoMCP.Data.Abstractions;
using MetronomoMCP.Tools;
using Moq;
using Xunit;

namespace MetronomoMCP.Tests.Tools;

public class SchemaToolTests
{
    private readonly Mock<IQueryRepository> _repositoryMock = new();
    private readonly SchemaTool _sut;

    public SchemaToolTests()
    {
        _sut = new SchemaTool(_repositoryMock.Object);
    }

    [Fact]
    public async Task DescribeTableAsync_WithExistingTable_ReturnsSchema()
    {
        var columns = new List<ColumnInfo>
        {
            new("Id", "int", false, null, true),
            new("Name", "nvarchar", false, 100, false),
            new("Email", "nvarchar", true, 200, false)
        };

        _repositoryMock
            .Setup(r => r.GetTableSchemaAsync("Users", It.IsAny<CancellationToken>()))
            .ReturnsAsync(columns);

        var result = await _sut.DescribeTableAsync("Users");

        result.Should().Contain("Users");
        result.Should().Contain("Id");
        result.Should().Contain("int");
        result.Should().Contain("✓");
    }

    [Fact]
    public async Task DescribeTableAsync_WithMissingTable_ReturnsNotFoundMessage()
    {
        _repositoryMock
            .Setup(r => r.GetTableSchemaAsync("NonExistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.DescribeTableAsync("NonExistent");

        result.Should().Contain("non trovata");
    }

    [Fact]
    public async Task DescribeTableAsync_WhenRepositoryThrows_ReturnsErrorMessage()
    {
        _repositoryMock
            .Setup(r => r.GetTableSchemaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Nome tabella non valido."));

        var result = await _sut.DescribeTableAsync("invalid table!");

        result.Should().StartWith("ERRORE:");
    }
}
