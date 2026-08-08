using System.Xml.Linq;
using NetArchTest.Rules;
using Orders.Api.Endpoints;
using Orders.Application.Interfaces;
using Orders.Domain;
using Orders.Infrastructure.Persistence;
using Xunit;

namespace Orders.Architecture.Tests;

/// <summary>
/// Architecture boundary tests that enforce strict isolation rules
/// between layers in the Clean Architecture.
/// </summary>
public class ArchitectureBoundaryTests
{
    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(Order).Assembly;

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(IOrderReader).Assembly;

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(OrdersDbContext).Assembly;

    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(OrdersEndpoints).Assembly;

    [Fact]
    public void Domain_Should_Have_Zero_PackageReferences()
    {
        var testAssemblyDir = Path.GetDirectoryName(typeof(ArchitectureBoundaryTests).Assembly.Location)!;
        var solutionRoot = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", "..", ".."));
        var domainProjectName = DomainAssembly.GetName().Name!;
        var domainCsprojPath = Path.Combine(solutionRoot, "src", domainProjectName, $"{domainProjectName}.csproj");

        Assert.True(File.Exists(domainCsprojPath),
            $"Domain .csproj not found at: {domainCsprojPath}");

        var doc = XDocument.Load(domainCsprojPath);
        var packageReferences = doc.Descendants("PackageReference").ToList();

        Assert.True(packageReferences.Count == 0,
            $"Domain project must have zero PackageReference items but found {packageReferences.Count}: " +
            string.Join(", ", packageReferences.Select(pr => pr.Attribute("Include")?.Value ?? "unknown")));
    }

    [Fact]
    public void Domain_Should_Not_Contain_MediatR_Types()
    {
        var mediatRInterfaces = new[]
        {
            "MediatR.IRequest",
            "MediatR.IRequestHandler",
            "MediatR.INotification",
            "MediatR.INotificationHandler"
        };

        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(mediatRInterfaces)
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Domain layer must not contain types implementing MediatR interfaces. " +
            "Violating types: " +
            string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Application_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Application layer must not reference Microsoft.EntityFrameworkCore namespace.");
    }

    [Fact]
    public void Repository_Interfaces_Implemented_Only_In_Infrastructure()
    {
        var repositoryInterfaces = Types.InAssembly(DomainAssembly)
            .That()
            .AreInterfaces()
            .And()
            .HaveNameEndingWith("Repository")
            .GetTypes()
            .ToList();

        Assert.True(repositoryInterfaces.Any(),
            "Expected at least one I*Repository interface in the Domain assembly.");

        var violatingTypes = new List<string>();

        var disallowedAssemblies = new (System.Reflection.Assembly Assembly, string LayerName)[]
        {
            (DomainAssembly, "Domain"),
            (ApplicationAssembly, "Application"),
            (ApiAssembly, "Api")
        };

        foreach (var repoInterface in repositoryInterfaces)
        {
            foreach (var (assembly, layerName) in disallowedAssemblies)
            {
                var implementingTypes = Types.InAssembly(assembly)
                    .That()
                    .ImplementInterface(repoInterface)
                    .GetTypes()
                    .ToList();

                foreach (var type in implementingTypes)
                {
                    violatingTypes.Add($"{type.FullName} (in {layerName} layer)");
                }
            }
        }

        Assert.True(violatingTypes.Count == 0,
            "Repository interfaces defined in Domain must only be implemented in Infrastructure. " +
            "Violating types: " +
            string.Join(", ", violatingTypes));
    }
}
