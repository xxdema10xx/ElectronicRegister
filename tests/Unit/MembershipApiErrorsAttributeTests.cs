using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElectronicRegisterAPI.Api.ExceptionHandling;
using ElectronicRegisterAPI.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

// MembershipApiErrors: SQL concurrency of the attributed IAM/invitation actions becomes 409 with the
// unchanged Italian title and code ConcurrentChange; every other exception is left to the global handler.
public sealed class MembershipApiErrorsAttributeTests
{
    private static ExceptionContext Context(Exception exception) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>()) { Exception = exception };

    // MySqlConnector exposes no public constructor: build the real exception type with its error number.
    private static MySqlException Sql(int number)
    {
        var constructor = typeof(MySqlException).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .First(c => c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(MySqlErrorCode), typeof(string), typeof(string) }));
        var exception = (MySqlException)constructor.Invoke(new object?[] { (MySqlErrorCode)number, "HY000", "synthetic" });
        Assert.Equal(number, exception.Number);
        return exception;
    }

    public static IEnumerable<object[]> Concurrency()
    {
        yield return new object[] { "ef-concurrency" };
        foreach (var number in new[] { 1062, 1205, 1213 })
        {
            yield return new object[] { "mysql-" + number };
            yield return new object[] { "wrapped-" + number };
        }
    }

    private static Exception Build(string kind) => kind switch
    {
        "ef-concurrency" => new DbUpdateConcurrencyException("synthetic"),
        _ when kind.StartsWith("mysql-") => Sql(int.Parse(kind["mysql-".Length..])),
        _ => new DbUpdateException("synthetic", Sql(int.Parse(kind["wrapped-".Length..])))
    };

    [Theory]
    [MemberData(nameof(Concurrency))]
    public void ConcurrencyBecomes409WithUnchangedTitleAndConcurrentChangeCode(string kind)
    {
        var context = Context(Build(kind));
        new MembershipApiErrorsAttribute().OnException(context);
        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(409, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(409, problem.Status);
        Assert.Equal("Conflitto sulla membership istituzionale", problem.Title);
        Assert.Null(problem.Detail);
        Assert.Equal("ConcurrentChange", Assert.Single(problem.Extensions).Value);
        Assert.Equal("code", problem.Extensions.Single().Key);
    }

    public static IEnumerable<object[]> Others()
    {
        yield return new object[] { new MembershipConflictException() };
        yield return new object[] { new MembershipConflictException(MembershipConflictException.StaleRevision) };
        yield return new object[] { Sql(1452) };                                   // FK violation: not a concurrency error
        yield return new object[] { Sql(1146) };                                   // missing table
        yield return new object[] { new DbUpdateException("synthetic", Sql(1452)) };
        yield return new object[] { new DbUpdateException("synthetic") };
        yield return new object[] { new InvalidOperationException("synthetic") };
        yield return new object[] { new ArgumentException("synthetic") };
        yield return new object[] { new UnauthorizedAccessException() };
    }

    [Theory]
    [MemberData(nameof(Others))]
    public void OtherExceptionsAreNotTouched(Exception exception)
    {
        var context = Context(exception);
        new MembershipApiErrorsAttribute().OnException(context);
        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
        Assert.Same(exception, context.Exception);
    }
}
