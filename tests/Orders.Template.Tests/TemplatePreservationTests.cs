using System.Diagnostics;
using FsCheck;
using FsCheck.Xunit;

namespace Orders.Template.Tests;

/// <summary>
/// Preservation Property Tests - Property 2
/// 
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
///
/// These tests verify that existing template behavior is preserved:
/// - Existing exclusions (frontend, .kiro, .github, .vscode, .template.config, bin, obj, node_modules, docker-compose.yml) remain excluded
/// - Structural scaffolding files (solution, Directory.Build.props, global.json, nuget.config, .gitignore, README.md, CHANGELOG.md, .csproj files, Placeholder.cs, Behaviours, IApplicationEventPublisher.cs) remain included
/// - sourceName renaming continues to function (all "Orders" occurrences replaced with project name)
///
/// These tests MUST PASS on UNFIXED code to establish the baseline.
/// They will be re-run after the fix to confirm no regressions.
/// </summary>
public class TemplatePreservationTests : IDisposable
{
    private readonly string _testOutputPath;
    private readonly string _templateSourcePath;

    public TemplatePreservationTests()
    {
        _testOutputPath = Path.Combine(Path.GetTempPath(), $"template-preservation-{Guid.NewGuid():N}");
        _templateSourcePath = FindRepoRoot();
    }

    public void Dispose()
    {
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

        var fallback = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
        if (File.Exists(Path.Combine(fallback, "Orders.sln")))
            return fallback;

        throw new InvalidOperationException("Could not find repository root (Orders.sln)");
    }

    private void GenerateProject(string projectName)
    {
        // Clean up any previous output
        if (Directory.Exists(_testOutputPath))
        {
            Directory.Delete(_testOutputPath, recursive: true);
        }

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

    private IEnumerable<string> GetAllRelativePaths()
    {
        if (!Directory.Exists(_testOutputPath))
            return Enumerable.Empty<string>();

        var allFiles = Directory.GetFiles(_testOutputPath, "*", SearchOption.AllDirectories);
        var allDirs = Directory.GetDirectories(_testOutputPath, "*", SearchOption.AllDirectories);

        return allFiles.Concat(allDirs)
            .Select(p => Path.GetRelativePath(_testOutputPath, p).Replace('\\', '/'))
            .ToList();
    }

    #region Property Tests: Existing Exclusions Remain Excluded (Requirements 3.1, 3.2, 3.3)

    /// <summary>
    /// **Validates: Requirements 3.2**
    /// 
    /// Property: For all template generations, frontend/** remains excluded from output.
    /// </summary>
    [Property(MaxTest = 3)]
    public Property FrontendFolder_IsExcluded_ForAllProjectNames()
    {
        var validNames = new[] { "TestProject", "MyApp", "Billing", "Inventory", "Shipping" };

        return Prop.ForAll(
            Gen.Elements(validNames).ToArbitrary(),
            projectName =>
            {
                GenerateProject(projectName);
                var paths = GetAllRelativePaths();

                var hasFrontend = paths.Any(p =>
                    p.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) ||
                    p.Equals("frontend", StringComparison.OrdinalIgnoreCase));

                return (!hasFrontend).Label($"frontend/ should NOT exist in output for project '{projectName}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.2**
    /// 
    /// Property: For all template generations, .kiro/**, .github/**, .vscode/**, .template.config/** remain excluded from output.
    /// </summary>
    [Property(MaxTest = 3)]
    public Property ConfigFolders_AreExcluded_ForAllProjectNames()
    {
        var validNames = new[] { "TestProject", "MyApp", "Billing", "Inventory", "Shipping" };
        var excludedFolders = new[] { ".kiro", ".github", ".vscode", ".template.config" };

        return Prop.ForAll(
            Gen.Elements(validNames).ToArbitrary(),
            projectName =>
            {
                GenerateProject(projectName);
                var paths = GetAllRelativePaths();

                var violations = excludedFolders
                    .Where(folder => paths.Any(p =>
                        p.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase) ||
                        p.Equals(folder, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                return (violations.Count == 0)
                    .Label($"Excluded folders found in output for '{projectName}': {string.Join(", ", violations)}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.2**
    /// 
    /// Property: For all template generations, **/bin/**, **/obj/**, **/node_modules/** remain excluded from output.
    /// </summary>
    [Property(MaxTest = 3)]
    public Property BuildArtifactFolders_AreExcluded_ForAllProjectNames()
    {
        var validNames = new[] { "TestProject", "MyApp", "Billing", "Inventory", "Shipping" };
        var excludedPatterns = new[] { "/bin/", "/obj/", "/node_modules/" };

        return Prop.ForAll(
            Gen.Elements(validNames).ToArbitrary(),
            projectName =>
            {
                GenerateProject(projectName);
                var paths = GetAllRelativePaths();

                var violations = paths
                    .Where(p => excludedPatterns.Any(pattern =>
                        p.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                        p.StartsWith(pattern.TrimStart('/'), StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                return (violations.Count == 0)
                    .Label($"Build artifact paths found in output for '{projectName}': {string.Join(", ", violations.Take(5))}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.3**
    /// 
    /// Property: For all template generations, docker-compose.yml remains excluded from output.
    /// </summary>
    [Property(MaxTest = 3)]
    public Property DockerComposeYml_IsExcluded_ForAllProjectNames()
    {
        var validNames = new[] { "TestProject", "MyApp", "Billing", "Inventory", "Shipping" };

        return Prop.ForAll(
            Gen.Elements(validNames).ToArbitrary(),
            projectName =>
            {
                GenerateProject(projectName);
                var paths = GetAllRelativePaths();

                var hasDockerCompose = paths.Any(p =>
                    p.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase));

                return (!hasDockerCompose).Label($"docker-compose.yml should NOT exist in output for project '{projectName}'");
            });
    }

    #endregion

    #region Property Tests: Structural Scaffolding Remains Included (Requirement 3.4)

    /// <summary>
    /// **Validates: Requirements 3.4**
    /// 
    /// Property: For all template generations, solution file, Directory.Build.props, global.json, nuget.config, .gitignore, README.md, CHANGELOG.md are included in output.
    /// </summary>
    [Property(MaxTest = 3)]
    public Property ScaffoldingRootFiles_AreIncluded_ForAllProjectNames()
    {
        var validNames = new[] { "TestProject", "MyApp", "Billing", "Inventory", "Shipping" };

        return Prop.ForAll(
            Gen.Elements(validNames).ToArbitrary(),
            projectName =>
            {
                GenerateProject(projectName);

                var expectedFiles = new[]
                {
                    $"{projectName}.sln",
                    "Directory.Build.props",
                    "global.json",
                    "nuget.config",
                    ".gitignore",
                    "README.md",
                    "CHANGELOG.md"
                };

                var missingFiles = expectedFiles
                    .Where(f => !File.Exists(Path.Combine(_testOutputPath, f)))
                    .ToList();

                return (missingFiles.Count == 0)
                    .Label($"Missing scaffolding files for '{projectName}': {string.Join(", ", missingFiles)}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.4**
    /// 
    /// Property: For all template generations, all .csproj project files are included in output.
    /// </summary>
    [Property(MaxTest = 3)]
    public Property ProjectFiles_AreIncluded_ForAllProjectNames()
    {
        var validNames = new[] { "TestProject", "MyApp", "Billing", "Inventory", "Shipping" };

        return Prop.ForAll(
            Gen.Elements(validNames).ToArbitrary(),
            projectName =>
            {
                GenerateProject(projectName);

                var expectedCsprojs = new[]
                {
                    $"src/{projectName}.Api/{projectName}.Api.csproj",
                    $"src/{projectName}.Domain/{projectName}.Domain.csproj",
                    $"src/{projectName}.Application/{projectName}.Application.csproj",
                    $"src/{projectName}.Infrastructure/{projectName}.Infrastructure.csproj",
                    $"tests/{projectName}.Api.Tests/{projectName}.Api.Tests.csproj",
                    $"tests/{projectName}.Domain.Tests/{projectName}.Domain.Tests.csproj",
                    $"tests/{projectName}.Application.Tests/{projectName}.Application.Tests.csproj",
                    $"tests/{projectName}.Architecture.Tests/{projectName}.Architecture.Tests.csproj",
                    $"tests/{projectName}.Infrastructure.Tests/{projectName}.Infrastructure.Tests.csproj"
                };

                var missingProjects = expectedCsprojs
                    .Where(f => !File.Exists(Path.Combine(_testOutputPath, f.Replace('/', Path.DirectorySeparatorChar))))
                    .ToList();

                return (missingProjects.Count == 0)
                    .Label($"Missing .csproj files for '{projectName}': {string.Join(", ", missingProjects)}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.4**
    /// 
    /// Property: For all template generations, Placeholder.cs files, Behaviours/, and IApplicationEventPublisher.cs remain included.
    /// </summary>
    [Property(MaxTest = 3)]
    public Property NonSampleSourceFiles_AreIncluded_ForAllProjectNames()
    {
        var validNames = new[] { "TestProject", "MyApp", "Billing", "Inventory", "Shipping" };

        return Prop.ForAll(
            Gen.Elements(validNames).ToArbitrary(),
            projectName =>
            {
                GenerateProject(projectName);

                var expectedFiles = new[]
                {
                    $"src/{projectName}.Domain/Placeholder.cs",
                    $"src/{projectName}.Application/Placeholder.cs",
                    $"src/{projectName}.Application/Behaviours/LoggingBehaviour.cs",
                    $"src/{projectName}.Application/Behaviours/ValidationBehaviour.cs",
                    $"src/{projectName}.Application/Interfaces/IApplicationEventPublisher.cs"
                };

                var missingFiles = expectedFiles
                    .Where(f => !File.Exists(Path.Combine(_testOutputPath, f.Replace('/', Path.DirectorySeparatorChar))))
                    .ToList();

                return (missingFiles.Count == 0)
                    .Label($"Missing non-sample source files for '{projectName}': {string.Join(", ", missingFiles)}");
            });
    }

    #endregion

    #region Property Tests: sourceName Renaming Continues to Function (Requirement 3.1)

    /// <summary>
    /// **Validates: Requirements 3.1**
    /// 
    /// Property: For all template generations, sourceName renaming works - all "Orders" occurrences
    /// are replaced with the project name in file names and file contents.
    /// </summary>
    [Property(MaxTest = 3)]
    public Property SourceNameRenaming_ReplacesOrders_ForAllProjectNames()
    {
        var validNames = new[] { "TestProject", "MyApp", "Billing", "Inventory", "Shipping" };

        return Prop.ForAll(
            Gen.Elements(validNames).ToArbitrary(),
            projectName =>
            {
                GenerateProject(projectName);

                // Verify file names use the project name (not "Orders")
                var slnExists = File.Exists(Path.Combine(_testOutputPath, $"{projectName}.sln"));
                var apiCsprojExists = File.Exists(Path.Combine(_testOutputPath, "src", $"{projectName}.Api", $"{projectName}.Api.csproj"));
                var domainCsprojExists = File.Exists(Path.Combine(_testOutputPath, "src", $"{projectName}.Domain", $"{projectName}.Domain.csproj"));

                // Verify file contents use the project name
                var slnContent = File.ReadAllText(Path.Combine(_testOutputPath, $"{projectName}.sln"));
                var contentHasProjectName = slnContent.Contains(projectName);
                var contentHasNoOrders = !slnContent.Contains("Orders");

                var allChecks = slnExists && apiCsprojExists && domainCsprojExists && contentHasProjectName && contentHasNoOrders;

                return allChecks.Label(
                    $"sourceName renaming failed for '{projectName}': " +
                    $"sln={slnExists}, apiCsproj={apiCsprojExists}, domainCsproj={domainCsprojExists}, " +
                    $"contentHasName={contentHasProjectName}, noOrders={contentHasNoOrders}");
            });
    }

    #endregion
}
