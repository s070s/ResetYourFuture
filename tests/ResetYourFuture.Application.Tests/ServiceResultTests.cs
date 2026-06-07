using ResetYourFuture.Web.ApiInterfaces;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class ServiceResultTests
{
    [Theory]
    [InlineData( 200, true )]
    [InlineData( 201, true )]
    [InlineData( 204, true )]
    [InlineData( 400, false )]
    [InlineData( 403, false )]
    [InlineData( 404, false )]
    public void IsSuccess_ReflectsStatusCodeRange( int status, bool expected )
    {
        new ServiceResult<string>( "v", status ).IsSuccess.ShouldBe( expected );
    }

    [Fact]
    public void Ok_SetsValueAnd200()
    {
        var r = ServiceResult<int>.Ok( 42 );

        r.Value.ShouldBe( 42 );
        r.StatusCode.ShouldBe( 200 );
        r.IsSuccess.ShouldBeTrue();
        r.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Created_Sets201() => ServiceResult<int>.Created( 1 ).StatusCode.ShouldBe( 201 );

    [Fact]
    public void NotFound_Sets404AndError()
    {
        var r = ServiceResult<string>.NotFound( error: "missing" );

        r.StatusCode.ShouldBe( 404 );
        r.ErrorMessage.ShouldBe( "missing" );
        r.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Forbidden_Sets403() => ServiceResult<string>.Forbidden( error: "no" ).StatusCode.ShouldBe( 403 );

    [Fact]
    public void BadRequest_Sets400() => ServiceResult<string>.BadRequest( error: "bad" ).StatusCode.ShouldBe( 400 );

    [Fact]
    public void NoContent_Sets204AndDefaultValue()
    {
        var r = ServiceResult<string>.NoContent();

        r.StatusCode.ShouldBe( 204 );
        r.Value.ShouldBeNull();
        r.IsSuccess.ShouldBeTrue();
    }
}
