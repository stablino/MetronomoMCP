using FluentAssertions;
using MetronomoMCP.Validation;
using Xunit;

namespace MetronomoMCP.Tests.Validation;

public class SqlReadOnlyValidatorTests
{
    [Theory]
    [InlineData("SELECT * FROM Customers")]
    [InlineData("SELECT Id, Name FROM Orders WHERE Status = 'Active'")]
    [InlineData("SELECT COUNT(*) FROM Products")]
    public void ValidQuery_ShouldPass(string sql)
    {
        var result = SqlReadOnlyValidator.Validate(sql);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("INSERT INTO Customers VALUES ('x')")]
    [InlineData("DELETE FROM Orders")]
    [InlineData("DROP TABLE Products")]
    [InlineData("UPDATE Users SET Name = 'hack'")]
    [InlineData("EXEC sp_executesql N'SELECT 1'")]
    [InlineData("TRUNCATE TABLE Logs")]
    public void ForbiddenQuery_ShouldFail(string sql)
    {
        var result = SqlReadOnlyValidator.Validate(sql);
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EmptyQuery_ShouldFail()
    {
        var result = SqlReadOnlyValidator.Validate("   ");
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
