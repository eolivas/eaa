using NetArchTest.Rules;
using Orders.Application.Interfaces;
using Orders.Domain;
using Orders.Infrastructure.Persistence;
using Xunit;

namespace Orders.Architecture.Tests;

/// <summary>
/// Architecture enforcement tests that verify Clean Architecture dependency rules
/// are respected across all layers.
/// Validates: Requirements 9.4
/// </summary>
public class CleanArchitectureTests
{
    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(Order).Assembly;

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(IOrderReader).Assembly;

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(OrdersDbContext).Assembly;

    [Fact]
    public void Domain_should_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Orders.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain layer must not depend on Infrastructure layer.");
    }

    [Fact]
    public void Domain_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Orders.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain layer must not depend on Api layer.");
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Orders.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Application layer must not depend on Infrastructure layer.");
    }

    [Fact]
    public void Application_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Orders.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Application layer must not depend on Api layer.");
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("Orders.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Infrastructure layer must not depend on Api layer.");
    }
}
