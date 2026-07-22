using System.Diagnostics;

namespace Orders.Template.Tests;

/// <summary>
/// Bug Condition Exploration Test - Property 1
/// 
/// Validates: Requirements 1.1, 1.2
///
/// This test encodes the EXPECTED (fixed) behavior:
/// - The generated output SHOULD include the docs/ folder with its contents
/// - The generated output SHOULD NOT contain Orders-specific sample files
///
/// On UNFIXED code, this test is EXPECTED TO FAIL because:
/// - docs/** is in the exclude array (so docs/ will be missing from output)
/// - Orders-specific sample files are NOT in the exclude array (so they will be present in output)
/// </summary>
public class TemplateBugConditionTests : IDisposable
{
    private readonly string _testOutputPath;
    private readonly string _templateSourcePath;

    public TemplateBugConditionTests()
    {
        // Use a unique path under the repo root to avoid conflicts
        _testOutputPath = Path.Combine(Path.GetTempPath(), $"template-test-{Guid.NewGuid():N}");
        _templateSourcePath = FindRepoRoot();
    }

    public void Dispose()
    {
        // Clean up test output directory
        if (Directory.Exists(_testOutputPath))
        {
            try
            {
                Directory.Delete(_testOutputPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Orders.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        // Fallback: try common development paths
        var fallback = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
        if (File.Exists(Path.Combine(fallback, "Orders.sln")))
            return fallback;

        throw new InvalidOperationException("Could not find repository root (Orders.sln)");
    }

    private void GenerateProject(string projectName = "TestProject")
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"new eaa-solution -n {projectName} -o \"{_testOutputPath}\"",
            WorkingDirectory = _templateSourcePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        process.WaitForExit(60000);

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"dotnet new failed with exit code {process.ExitCode}: {error}");
        }
    }

    /// <summary>
    /// **Validates: Requirements 1.2**
    /// 
    /// Property: Template output SHOULD include docs/ folder with all expected contents.
    /// 
    /// On unfixed code, this FAILS because docs/** is in the exclude array,
    /// so the docs/ folder is excluded from generated output.
    /// </summary>
    [Fact]
    public void GeneratedOutput_ShouldIncludeDocsFolder()
    {
        // Arrange & Act
        GenerateProject("TestProject");

        // Assert - docs/ folder should exist
        var docsPath = Path.Combine(_testOutputPath, "docs");
        Assert.True(Directory.Exists(docsPath), 
            $"Expected docs/ folder to exist at '{docsPath}' but it was not found. " +
            "This confirms bug 1.2: docs/** is incorrectly in the exclude array.");
    }

    /// <summary>
    /// **Validates: Requirements 1.2**
    /// 
    /// Property: Template output SHOULD include docs/ subfolders (adr/, cloud-topology/, llm-cost/, sizing/).
    /// </summary>
    [Fact]
    public void GeneratedOutput_ShouldIncludeDocsSubfolders()
    {
        // Arrange & Act
        GenerateProject("TestProject");

        // Assert - docs subfolders should exist
        var expectedDocsFolders = new[]
        {
            "docs/adr",
            "docs/cloud-topology",
            "docs/llm-cost",
            "docs/sizing"
        };

        foreach (var folder in expectedDocsFolders)
        {
            var fullPath = Path.Combine(_testOutputPath, folder.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(Directory.Exists(fullPath),
                $"Expected '{folder}' to exist in generated output but it was not found. " +
                "This confirms bug 1.2: docs/** is incorrectly excluded.");
        }
    }

    /// <summary>
    /// **Validates: Requirements 1.2**
    /// 
    /// Property: Template output SHOULD include docs/ files (REPO_CONVENTIONS.md, SECURITY.md).
    /// </summary>
    [Fact]
    public void GeneratedOutput_ShouldIncludeDocsFiles()
    {
        // Arrange & Act
        GenerateProject("TestProject");

        // Assert - docs files should exist
        var expectedDocsFiles = new[]
        {
            "docs/REPO_CONVENTIONS.md",
            "docs/SECURITY.md"
        };

        foreach (var file in expectedDocsFiles)
        {
            var fullPath = Path.Combine(_testOutputPath, file.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath),
                $"Expected '{file}' to exist in generated output but it was not found. " +
                "This confirms bug 1.2: docs/** is incorrectly excluded.");
        }
    }

    /// <summary>
    /// **Validates: Requirements 1.1**
    /// 
    /// Property: Template output SHOULD NOT contain Orders-specific sample files
    /// (API Endpoints and MCP tools).
    /// 
    /// On unfixed code, this FAILS because these files are NOT in the exclude array,
    /// so they get included (and renamed via sourceName) in generated output.
    /// </summary>
    [Fact]
    public void GeneratedOutput_ShouldNotContainOrdersApiSampleFiles()
    {
        // Arrange & Act
        GenerateProject("TestProject");

        // Assert - Orders-specific API sample files should NOT exist
        var unexpectedPaths = new[]
        {
            "src/TestProject.Api/Endpoints",
            "src/TestProject.Api/Mcp"
        };

        foreach (var path in unexpectedPaths)
        {
            var fullPath = Path.Combine(_testOutputPath, path.Replace('/', Path.DirectorySeparatorChar));
            Assert.False(Directory.Exists(fullPath),
                $"Expected '{path}' to NOT exist in generated output but it was found. " +
                "This confirms bug 1.1: Orders-specific sample files are not excluded.");
        }
    }

    /// <summary>
    /// **Validates: Requirements 1.1**
    /// 
    /// Property: Template output SHOULD NOT contain Orders-specific Domain sample files.
    /// </summary>
    [Fact]
    public void GeneratedOutput_ShouldNotContainOrdersDomainSampleFiles()
    {
        // Arrange & Act
        GenerateProject("TestProject");

        // Assert - Orders-specific Domain sample files should NOT exist
        var unexpectedFiles = new[]
        {
            "src/TestProject.Domain/Order.cs",
            "src/TestProject.Domain/OrderId.cs"
        };

        var unexpectedDirs = new[]
        {
            "src/TestProject.Domain/Pricing",
            "src/TestProject.Domain/Events"
        };

        foreach (var file in unexpectedFiles)
        {
            var fullPath = Path.Combine(_testOutputPath, file.Replace('/', Path.DirectorySeparatorChar));
            Assert.False(File.Exists(fullPath),
                $"Expected '{file}' to NOT exist in generated output but it was found. " +
                "This confirms bug 1.1: Orders-specific sample files are not excluded.");
        }

        foreach (var dir in unexpectedDirs)
        {
            var fullPath = Path.Combine(_testOutputPath, dir.Replace('/', Path.DirectorySeparatorChar));
            Assert.False(Directory.Exists(fullPath),
                $"Expected '{dir}' to NOT exist in generated output but it was found. " +
                "This confirms bug 1.1: Orders-specific sample files are not excluded.");
        }
    }

    /// <summary>
    /// **Validates: Requirements 1.1**
    /// 
    /// Property: Template output SHOULD NOT contain Orders-specific Application sample files.
    /// </summary>
    [Fact]
    public void GeneratedOutput_ShouldNotContainOrdersApplicationSampleFiles()
    {
        // Arrange & Act
        GenerateProject("TestProject");

        // Assert - Orders-specific Application sample directories should NOT exist
        var unexpectedDirs = new[]
        {
            "src/TestProject.Application/Commands",
            "src/TestProject.Application/Queries",
            "src/TestProject.Application/DTOs"
        };

        foreach (var dir in unexpectedDirs)
        {
            var fullPath = Path.Combine(_testOutputPath, dir.Replace('/', Path.DirectorySeparatorChar));
            Assert.False(Directory.Exists(fullPath),
                $"Expected '{dir}' to NOT exist in generated output but it was found. " +
                "This confirms bug 1.1: Orders-specific sample files are not excluded.");
        }
    }

    /// <summary>
    /// **Validates: Requirements 1.1**
    /// 
    /// Property: Template output SHOULD NOT contain Orders-specific Infrastructure sample files.
    /// </summary>
    [Fact]
    public void GeneratedOutput_ShouldNotContainOrdersInfrastructureSampleFiles()
    {
        // Arrange & Act
        GenerateProject("TestProject");

        // Assert - Orders-specific Infrastructure sample files/dirs should NOT exist
        var unexpectedFiles = new[]
        {
            "src/TestProject.Infrastructure/Persistence/EfOrderRepository.cs"
        };

        var unexpectedDirs = new[]
        {
            "src/TestProject.Infrastructure/Caching",
            "src/TestProject.Infrastructure/Messaging"
        };

        foreach (var file in unexpectedFiles)
        {
            var fullPath = Path.Combine(_testOutputPath, file.Replace('/', Path.DirectorySeparatorChar));
            Assert.False(File.Exists(fullPath),
                $"Expected '{file}' to NOT exist in generated output but it was found. " +
                "This confirms bug 1.1: Orders-specific sample files are not excluded.");
        }

        foreach (var dir in unexpectedDirs)
        {
            var fullPath = Path.Combine(_testOutputPath, dir.Replace('/', Path.DirectorySeparatorChar));
            Assert.False(Directory.Exists(fullPath),
                $"Expected '{dir}' to NOT exist in generated output but it was found. " +
                "This confirms bug 1.1: Orders-specific sample files are not excluded.");
        }
    }

    /// <summary>
    /// **Validates: Requirements 1.1**
    /// 
    /// Property: Template output SHOULD NOT contain Orders-specific test sample files.
    /// </summary>
    [Fact]
    public void GeneratedOutput_ShouldNotContainOrdersTestSampleFiles()
    {
        // Arrange & Act
        GenerateProject("TestProject");

        // Assert - Orders-specific test files should NOT exist
        var unexpectedFiles = new[]
        {
            "tests/TestProject.Domain.Tests/OrderTests.cs",
            "tests/TestProject.Application.Tests/PlaceOrderHandlerTests.cs"
        };

        foreach (var file in unexpectedFiles)
        {
            var fullPath = Path.Combine(_testOutputPath, file.Replace('/', Path.DirectorySeparatorChar));
            Assert.False(File.Exists(fullPath),
                $"Expected '{file}' to NOT exist in generated output but it was found. " +
                "This confirms bug 1.1: Orders-specific sample files are not excluded.");
        }
    }
}
