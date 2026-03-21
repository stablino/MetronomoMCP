using FluentAssertions;
using MetronomoMCP.Data.Abstractions;
using MetronomoMCP.Tools;
using Moq;
using Xunit;

namespace MetronomoMCP.Tests.Tools;

public class QueryToolTests
{
    private readonly Mock<IQueryRepository> _repositoryMock = new();
    private readonly QueryTool _sut;

    public QueryToolTests()
    {
        _sut = new QueryTool(_repositoryMock.Object);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithValidSelectQuery_ReturnsFormattedResult()
    {
        var queryResult = new QueryResult(
            Columns: ["Id", "Name"],
            Rows: [["1", "Alice"], ["2", "Bob"]],
            TotalRows: 2,
            WasTruncated: false);

        _repositoryMock
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);

        var result = await _sut.ExecuteQueryAsync("SELECT Id, Name FROM Users");

        result.Should().Contain("Id");
        result.Should().Contain("Alice");
        result.Should().Contain("2 righe restituite");
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithForbiddenKeyword_ReturnsValidationError()
    {
        var result = await _sut.ExecuteQueryAsync("DELETE FROM Users");

        result.Should().StartWith("ERRORE VALIDAZIONE:");
        _repositoryMock.Verify(r => r.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteQueryAsync_WhenRepositoryThrows_ReturnsExecutionError()
    {
        _repositoryMock
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var result = await _sut.ExecuteQueryAsync("SELECT 1");

        result.Should().StartWith("ERRORE ESECUZIONE:");
    }
}
