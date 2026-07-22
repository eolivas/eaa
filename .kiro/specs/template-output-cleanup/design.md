# Template Output Cleanup Bugfix Design

## Overview

The `.template.config/template.json` file has two configuration errors in its `sources[0].modifiers[0].exclude` array:

1. It **excludes** `docs/**`, which removes essential project documentation (ADRs, security docs, conventions) from generated projects.
2. It **does not exclude** Orders-specific sample files (endpoints, domain entities, application handlers, MCP tools, frontend features, and related test files) that serve as reference code but should not appear in a freshly scaffolded project.

The fix involves modifying the exclude array: remove the `docs/**` entry and add glob patterns for Orders-specific sample files across all layers (API endpoints, Domain entities, Application handlers, Infrastructure implementations, MCP tools, and test files that test sample code).

## Glossary

- **Bug_Condition (C)**: The condition where template output either includes Orders sample files or excludes the docs folder — caused by incorrect entries in the `exclude` array of `template.json`
- **Property (P)**: Template output SHALL contain `docs/` and SHALL NOT contain Orders-specific sample files
- **Preservation**: Existing exclusions (frontend, .kiro, .github, .vscode, .template.config, bin, obj, node_modules, docker-compose.yml) and the `sourceName` renaming mechanism must remain unchanged
- **template.json**: The `.template.config/template.json` file that configures `dotnet new` template behavior
- **sourceName**: The `dotnet new` template engine mechanism that renames all occurrences of a source name (here "Orders") to the user-provided project name
- **exclude array**: The `sources[0].modifiers[0].exclude` array in template.json that specifies glob patterns for files/folders to omit from generated output

## Bug Details

### Bug Condition

The bug manifests when a user generates a new project from the template using `dotnet new eaa-solution -n <ProjectName>`. The exclude array in template.json incorrectly includes `docs/**` (removing documentation from output) and does not include patterns for Orders-specific sample files (allowing sample code into output).

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type TemplateGenerationRequest
  OUTPUT: boolean

  LET excludeArray = template.json.sources[0].modifiers[0].exclude
  
  RETURN ("docs/**" IN excludeArray)
         OR (OrdersSampleFilePatterns NOT SUBSET OF excludeArray)
END FUNCTION
```

Where `OrdersSampleFilePatterns` includes:
- `src/Orders.Api/Endpoints/**`
- `src/Orders.Api/Mcp/**`
- `src/Orders.Domain/Order.cs`, `src/Orders.Domain/OrderId.cs`, `src/Orders.Domain/OrderLine.cs`, `src/Orders.Domain/OrderLineId.cs`, `src/Orders.Domain/OrderStatus.cs`, `src/Orders.Domain/CustomerId.cs`, `src/Orders.Domain/ProductId.cs`, `src/Orders.Domain/IOrderRepository.cs`
- `src/Orders.Domain/Pricing/**`
- `src/Orders.Domain/Events/**`
- `src/Orders.Application/Commands/**`
- `src/Orders.Application/Queries/**`
- `src/Orders.Application/DTOs/**`
- `src/Orders.Application/Interfaces/IOrderExporter.cs`, `src/Orders.Application/Interfaces/IOrderReader.cs`, `src/Orders.Application/Interfaces/IOrderWriter.cs`
- `src/Orders.Infrastructure/Persistence/EfOrderRepository.cs`, `src/Orders.Infrastructure/Persistence/OrderEntityTypeConfiguration.cs`, `src/Orders.Infrastructure/Persistence/OrdersDbContext.cs`
- `src/Orders.Infrastructure/Caching/**`
- `src/Orders.Infrastructure/Messaging/**`
- `src/Orders.Infrastructure/Specifications/**`
- `tests/Orders.Domain.Tests/OrderTests.cs`, `tests/Orders.Domain.Tests/OrderFaker.cs`, `tests/Orders.Domain.Tests/PricingServiceTests.cs`, `tests/Orders.Domain.Tests/OrderRepositoryContractTests.cs`
- `tests/Orders.Application.Tests/PlaceOrderHandlerTests.cs`
- `tests/Orders.Infrastructure.Tests/EfOrderRepositoryContractTests.cs`
- `frontend/**` (already excluded — no change needed)

### Examples

- **Example 1**: User runs `dotnet new eaa-solution -n Billing`. Output contains `src/Billing.Api/Endpoints/BillingEndpoints.cs` (renamed from OrdersEndpoints.cs). **Expected**: No Endpoints folder or file in output.
- **Example 2**: User runs `dotnet new eaa-solution -n Billing`. Output does NOT contain `docs/adr/ADR-001-clean-architecture.md`. **Expected**: `docs/` folder and all contents present.
- **Example 3**: User runs `dotnet new eaa-solution -n Billing`. Output contains `src/Billing.Domain/Order.cs` (entity). **Expected**: Only `Placeholder.cs` remains in Domain layer root.
- **Edge case**: User runs `dotnet new eaa-solution -n Billing`. Output correctly excludes `frontend/`, `.github/`, and `docker-compose.yml` (unchanged behavior).

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- The `sourceName` mechanism must continue to rename "Orders" to the user-provided project name in all file names and file contents
- The following exclusions must remain in the exclude array: `frontend/**`, `.kiro/**`, `.github/**`, `.vscode/**`, `.template.config/**`, `**/bin/**`, `**/obj/**`, `**/node_modules/**`, `docker-compose.yml`
- The template metadata (author, classifications, identity, name, shortName, description, tags, preferNameDirectory) must remain unchanged
- Structural scaffolding files must continue to be included: solution file, `Directory.Build.props`, `global.json`, `nuget.config`, `.gitignore`, `README.md`, `CHANGELOG.md`
- Project files (`.csproj`) for all layers must continue to be included
- Non-sample infrastructure files (`Placeholder.cs`, `Behaviours/`, `Interfaces/IApplicationEventPublisher.cs`) must continue to be included
- Test project structure and placeholder tests must continue to be included

**Scope:**
All inputs that do NOT involve the two bugs (docs exclusion and missing Orders sample exclusion) should be completely unaffected by this fix. This includes:
- Template metadata fields
- The `sourceName` renaming mechanism
- All other existing exclude patterns
- The overall template engine behavior

## Hypothesized Root Cause

Based on the bug description, the most likely issues are:

1. **Overly broad docs exclusion**: The `docs/**` entry was likely added to keep internal developer docs out of templates, but it removes ALL documentation — including architectural decision records and conventions that are meant to scaffold new projects.

2. **Missing sample file exclusions**: The template was initially created when the project had fewer sample files, and as the Orders sample code grew (endpoints, domain entities, handlers, MCP tools), corresponding exclude entries were never added to `template.json`.

3. **No distinction between scaffolding and sample code**: The template treats all source code equally — it lacks a mechanism to distinguish between structural scaffolding (empty layers with Placeholder.cs) and reference/sample implementations (full Order domain, handlers, endpoints).

4. **Untested template output**: The template likely has no automated validation of its output, so these configuration errors went unnoticed as the project evolved.

## Correctness Properties

Property 1: Bug Condition - Template excludes Orders sample files

_For any_ template generation where the bug condition holds (the exclude array contains the corrected patterns), the generated output SHALL NOT contain any Orders-specific sample files (endpoints, domain entities, application handlers/queries/DTOs, MCP tools, or related tests) while retaining structural scaffolding (Placeholder.cs, project files, Behaviours, architectural tests).

**Validates: Requirements 2.1**

Property 2: Bug Condition - Template includes docs folder

_For any_ template generation where the bug condition holds (the exclude array no longer contains `docs/**`), the generated output SHALL include the `docs/` folder with all its contents: `docs/adr/`, `docs/cloud-topology/`, `docs/llm-cost/`, `docs/sizing/`, `docs/bounded-contexts.md`, `docs/BRANCH_PROTECTION.md`, `docs/REPO_CONVENTIONS.md`, and `docs/SECURITY.md`.

**Validates: Requirements 2.2**

Property 3: Preservation - Existing exclusions remain intact

_For any_ template generation after the fix, the generated output SHALL continue to exclude `frontend/**`, `.kiro/**`, `.github/**`, `.vscode/**`, `.template.config/**`, `**/bin/**`, `**/obj/**`, `**/node_modules/**`, and `docker-compose.yml`, preserving all pre-existing non-docs exclusion behavior.

**Validates: Requirements 3.1, 3.2, 3.3**

Property 4: Preservation - Structural scaffolding remains included

_For any_ template generation after the fix, the generated output SHALL continue to include the solution file, `Directory.Build.props`, `global.json`, `nuget.config`, `.gitignore`, `README.md`, `CHANGELOG.md`, all `.csproj` project files, and non-sample source files (Placeholder.cs, Behaviours, IApplicationEventPublisher.cs, architectural tests).

**Validates: Requirements 3.4**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `.template.config/template.json`

**Section**: `sources[0].modifiers[0].exclude`

**Specific Changes**:

1. **Remove `docs/**` from exclude array**: This allows the docs folder and all its contents to be included in template output.

2. **Add Orders-specific API sample exclusions**:
   - `"src/Orders.Api/Endpoints/**"` — removes sample endpoints
   - `"src/Orders.Api/Mcp/**"` — removes sample MCP tools

3. **Add Orders-specific Domain sample exclusions**:
   - `"src/Orders.Domain/Order.cs"`
   - `"src/Orders.Domain/OrderId.cs"`
   - `"src/Orders.Domain/OrderLine.cs"`
   - `"src/Orders.Domain/OrderLineId.cs"`
   - `"src/Orders.Domain/OrderStatus.cs"`
   - `"src/Orders.Domain/CustomerId.cs"`
   - `"src/Orders.Domain/ProductId.cs"`
   - `"src/Orders.Domain/IOrderRepository.cs"`
   - `"src/Orders.Domain/Pricing/**"`
   - `"src/Orders.Domain/Events/**"`

4. **Add Orders-specific Application sample exclusions**:
   - `"src/Orders.Application/Commands/**"`
   - `"src/Orders.Application/Queries/**"`
   - `"src/Orders.Application/DTOs/**"`
   - `"src/Orders.Application/Interfaces/IOrderExporter.cs"`
   - `"src/Orders.Application/Interfaces/IOrderReader.cs"`
   - `"src/Orders.Application/Interfaces/IOrderWriter.cs"`

5. **Add Orders-specific Infrastructure sample exclusions**:
   - `"src/Orders.Infrastructure/Persistence/EfOrderRepository.cs"`
   - `"src/Orders.Infrastructure/Persistence/OrderEntityTypeConfiguration.cs"`
   - `"src/Orders.Infrastructure/Persistence/OrdersDbContext.cs"`
   - `"src/Orders.Infrastructure/Caching/**"`
   - `"src/Orders.Infrastructure/Messaging/**"`
   - `"src/Orders.Infrastructure/Specifications/**"`

6. **Add Orders-specific test sample exclusions**:
   - `"tests/Orders.Domain.Tests/OrderTests.cs"`
   - `"tests/Orders.Domain.Tests/OrderFaker.cs"`
   - `"tests/Orders.Domain.Tests/PricingServiceTests.cs"`
   - `"tests/Orders.Domain.Tests/OrderRepositoryContractTests.cs"`
   - `"tests/Orders.Application.Tests/PlaceOrderHandlerTests.cs"`
   - `"tests/Orders.Infrastructure.Tests/EfOrderRepositoryContractTests.cs"`

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Generate a project from the current (unfixed) template and inspect the output to confirm (a) docs are missing and (b) Orders sample files are present. This can be done by running `dotnet new eaa-solution -n TestProject -o ./test-output` and listing the contents.

**Test Cases**:
1. **Docs Missing Test**: Generate project, assert `docs/` folder does not exist in output (will fail — confirms bug 1.2)
2. **Sample Endpoints Present Test**: Generate project, assert `src/TestProject.Api/Endpoints/` exists in output (will fail — confirms bug 1.1)
3. **Sample Domain Entities Present Test**: Generate project, assert `src/TestProject.Domain/Order.cs` exists in output (will fail — confirms bug 1.1)
4. **Sample MCP Tools Present Test**: Generate project, assert `src/TestProject.Api/Mcp/` exists in output (will fail — confirms bug 1.1)

**Expected Counterexamples**:
- `docs/` folder is absent from generated output
- Orders sample files (renamed to TestProject) are present in generated output
- Possible cause: incorrect exclude array configuration in template.json

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed template produces the expected behavior.

**Pseudocode:**
```
FOR ALL templateGeneration WHERE isBugCondition(templateGeneration) DO
  output := dotnet_new_fixed(templateGeneration)
  ASSERT "docs/" IN output.directories
  ASSERT "docs/adr/" IN output.directories
  ASSERT "docs/REPO_CONVENTIONS.md" IN output.files
  ASSERT "src/*/Endpoints/" NOT IN output.directories
  ASSERT "src/*/Mcp/" NOT IN output.directories
  ASSERT "src/*/Domain/Order.cs" NOT IN output.files
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed template produces the same result as the original template.

**Pseudocode:**
```
FOR ALL templateGeneration WHERE NOT isBugCondition(templateGeneration) DO
  ASSERT dotnet_new_original(templateGeneration).excludedFiles
         = dotnet_new_fixed(templateGeneration).excludedFiles
  -- i.e., frontend, .kiro, .github, .vscode, .template.config, bin, obj, 
  --       node_modules, docker-compose.yml are still excluded
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It can generate many template configurations and verify exclusion rules hold
- It catches edge cases where a new exclusion pattern might accidentally match scaffolding files
- It provides strong guarantees that non-buggy behavior is unchanged

**Test Plan**: Observe behavior on UNFIXED code first for existing exclusions and structural scaffolding, then write tests to verify this continues after fix.

**Test Cases**:
1. **Frontend Exclusion Preservation**: Verify `frontend/**` is still excluded from generated output
2. **Config Folder Exclusion Preservation**: Verify `.kiro/**`, `.github/**`, `.vscode/**`, `.template.config/**` are still excluded
3. **Build Artifact Exclusion Preservation**: Verify `**/bin/**`, `**/obj/**`, `**/node_modules/**` are still excluded
4. **Docker Compose Exclusion Preservation**: Verify `docker-compose.yml` is still excluded
5. **Scaffolding Inclusion Preservation**: Verify solution file, Directory.Build.props, global.json, nuget.config, .gitignore, README.md, CHANGELOG.md are included
6. **Project File Inclusion Preservation**: Verify all `.csproj` files are still included
7. **Placeholder File Preservation**: Verify `Placeholder.cs` files in each layer are still included

### Unit Tests

- Validate the JSON structure of template.json after modification (valid JSON, correct schema)
- Verify the exclude array contains all expected Orders-specific patterns
- Verify the exclude array does NOT contain `docs/**`
- Verify the exclude array still contains all preservation patterns

### Property-Based Tests

- Generate random file paths and verify they match/don't-match the exclude globs correctly
- Test that no scaffolding file path matches any of the new exclude patterns
- Test that all identified Orders sample file paths match at least one exclude pattern

### Integration Tests

- Run `dotnet new eaa-solution -n TestProject` and verify full output structure
- Verify docs folder is present with expected contents
- Verify no Orders-specific sample files remain in output
- Verify sourceName renaming still works (e.g., `TestProject.sln` exists)
- Verify structural scaffolding (Placeholder.cs, Behaviours) is present
