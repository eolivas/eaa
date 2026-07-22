# Implementation Plan

## Overview

Fix the `.template.config/template.json` exclude array to (1) stop excluding the `docs/` folder and (2) add exclusions for Orders-specific sample files. The workflow follows the exploratory bugfix methodology: write tests to confirm the bug, write preservation tests, implement the fix, then validate.

## Tasks

- [ ] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Template Includes Orders Sample Files and Excludes Docs Folder
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Generate a project from the current template using `dotnet new eaa-solution -n TestProject -o ./test-output` and assert the expected (fixed) behavior
  - Test that generated output includes `docs/` folder with contents (docs/adr/, docs/cloud-topology/, docs/llm-cost/, docs/sizing/, docs/REPO_CONVENTIONS.md, docs/SECURITY.md)
  - Test that generated output does NOT contain Orders-specific sample files: `src/TestProject.Api/Endpoints/`, `src/TestProject.Api/Mcp/`, `src/TestProject.Domain/Order.cs`, `src/TestProject.Domain/OrderId.cs`, `src/TestProject.Domain/Pricing/`, `src/TestProject.Domain/Events/`, `src/TestProject.Application/Commands/`, `src/TestProject.Application/Queries/`, `src/TestProject.Application/DTOs/`, `src/TestProject.Infrastructure/Persistence/EfOrderRepository.cs`, `src/TestProject.Infrastructure/Caching/`, `src/TestProject.Infrastructure/Messaging/`, `tests/TestProject.Domain.Tests/OrderTests.cs`, `tests/TestProject.Application.Tests/PlaceOrderHandlerTests.cs`
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (docs/ missing from output AND Orders sample files present in output - this proves the bug exists)
  - Document counterexamples found: `docs/` absent, Orders sample files renamed to TestProject present in output
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2_

- [ ] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Existing Exclusions and Structural Scaffolding Remain Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: Generate project from UNFIXED template, confirm `frontend/` is NOT in output
  - Observe: Generate project from UNFIXED template, confirm `.kiro/`, `.github/`, `.vscode/`, `.template.config/` are NOT in output
  - Observe: Generate project from UNFIXED template, confirm `**/bin/**`, `**/obj/**`, `**/node_modules/**` are NOT in output
  - Observe: Generate project from UNFIXED template, confirm `docker-compose.yml` is NOT in output
  - Observe: Generate project from UNFIXED template, confirm `TestProject.sln`, `Directory.Build.props`, `global.json`, `nuget.config`, `.gitignore`, `README.md`, `CHANGELOG.md` ARE in output
  - Observe: Generate project from UNFIXED template, confirm all `.csproj` files (TestProject.Api, TestProject.Domain, TestProject.Application, TestProject.Infrastructure, test projects) ARE in output
  - Observe: Generate project from UNFIXED template, confirm `sourceName` renaming works ("Orders" replaced with "TestProject" in file names and contents)
  - Write property-based tests asserting: for all template generations, `frontend/**`, `.kiro/**`, `.github/**`, `.vscode/**`, `.template.config/**`, `**/bin/**`, `**/obj/**`, `**/node_modules/**`, `docker-compose.yml` remain excluded
  - Write property-based tests asserting: for all template generations, solution file, Directory.Build.props, global.json, nuget.config, .gitignore, README.md, CHANGELOG.md, .csproj files, Placeholder.cs files, Behaviours/, IApplicationEventPublisher.cs remain included
  - Write property-based tests asserting: sourceName renaming continues to function (all "Orders" occurrences replaced with project name)
  - Verify tests PASS on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [ ] 3. Fix template.json exclude array

  - [ ] 3.1 Implement the fix
    - Remove `docs/**` from the `sources[0].modifiers[0].exclude` array in `.template.config/template.json`
    - Add Orders-specific API sample exclusions: `src/Orders.Api/Endpoints/**`, `src/Orders.Api/Mcp/**`
    - Add Orders-specific Domain sample exclusions: `src/Orders.Domain/Order.cs`, `src/Orders.Domain/OrderId.cs`, `src/Orders.Domain/OrderLine.cs`, `src/Orders.Domain/OrderLineId.cs`, `src/Orders.Domain/OrderStatus.cs`, `src/Orders.Domain/CustomerId.cs`, `src/Orders.Domain/ProductId.cs`, `src/Orders.Domain/IOrderRepository.cs`, `src/Orders.Domain/Pricing/**`, `src/Orders.Domain/Events/**`
    - Add Orders-specific Application sample exclusions: `src/Orders.Application/Commands/**`, `src/Orders.Application/Queries/**`, `src/Orders.Application/DTOs/**`, `src/Orders.Application/Interfaces/IOrderExporter.cs`, `src/Orders.Application/Interfaces/IOrderReader.cs`, `src/Orders.Application/Interfaces/IOrderWriter.cs`
    - Add Orders-specific Infrastructure sample exclusions: `src/Orders.Infrastructure/Persistence/EfOrderRepository.cs`, `src/Orders.Infrastructure/Persistence/OrderEntityTypeConfiguration.cs`, `src/Orders.Infrastructure/Persistence/OrdersDbContext.cs`, `src/Orders.Infrastructure/Caching/**`, `src/Orders.Infrastructure/Messaging/**`, `src/Orders.Infrastructure/Specifications/**`
    - Add Orders-specific test sample exclusions: `tests/Orders.Domain.Tests/OrderTests.cs`, `tests/Orders.Domain.Tests/OrderFaker.cs`, `tests/Orders.Domain.Tests/PricingServiceTests.cs`, `tests/Orders.Domain.Tests/OrderRepositoryContractTests.cs`, `tests/Orders.Application.Tests/PlaceOrderHandlerTests.cs`, `tests/Orders.Infrastructure.Tests/EfOrderRepositoryContractTests.cs`
    - Validate resulting JSON is syntactically valid
    - _Bug_Condition: isBugCondition(input) where ("docs/**" IN excludeArray) OR (OrdersSampleFilePatterns NOT SUBSET OF excludeArray)_
    - _Expected_Behavior: exclude array does NOT contain "docs/**" AND excludeArray contains all OrdersSampleFilePatterns_
    - _Preservation: frontend/**, .kiro/**, .github/**, .vscode/**, .template.config/**, **/bin/**, **/obj/**, **/node_modules/**, docker-compose.yml remain in exclude array_
    - _Requirements: 2.1, 2.2, 3.1, 3.2, 3.3_

  - [ ] 3.2 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Template Includes Orders Sample Files and Excludes Docs Folder
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (docs present, sample files absent)
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed - docs included, Orders sample files excluded)
    - _Requirements: 2.1, 2.2_

  - [ ] 3.3 Verify preservation tests still pass
    - **Property 2: Preservation** - Existing Exclusions and Structural Scaffolding Remain Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions - existing exclusions intact, scaffolding still included, sourceName still works)
    - Confirm all tests still pass after fix (no regressions)

- [ ] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Task Dependency Graph

```json
{
  "waves": [
    ["1", "2"],
    ["3.1"],
    ["3.2", "3.3"],
    ["4"]
  ]
}
```

## Notes

- Tasks 1 and 2 are independent and can be worked on in parallel (both operate on UNFIXED code)
- Task 3.1 (the actual fix) must wait until tasks 1 and 2 are complete
- Tasks 3.2 and 3.3 verify the fix against the previously-written tests
- The fix is isolated to a single file: `.template.config/template.json`
- Clean up any test-output directories generated during test execution
