# Bugfix Requirements Document

## Introduction

When generating a new project using the `dotnet new eaa-solution` template, two problems occur in the generated output:

1. **Orders sample files are included** — The template generates Orders-specific files (endpoints, domain entities, application handlers, infrastructure, MCP tools, frontend features) that are sample/reference code and should not appear in a freshly scaffolded project.
2. **The `docs/` folder is excluded** — The template's exclude list prevents the `docs/` folder from being included in generated projects, even though it contains essential documentation (ADRs, cloud topology, LLM cost estimation, sizing, REPO_CONVENTIONS.md, SECURITY.md) that every new project should start with.

The root cause is the `sources[0].modifiers[0].exclude` array in `.template.config/template.json`, which excludes `docs/**` and does not exclude the Orders-specific sample files that should be removed from template output.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a user runs `dotnet new eaa-solution -n <ProjectName>` THEN the system generates Orders-specific sample files (e.g., OrdersEndpoints.cs, OrderMcpTools.cs, Orders domain entities, Orders application handlers, Orders frontend feature) renamed to the new project name

1.2 WHEN a user runs `dotnet new eaa-solution -n <ProjectName>` THEN the system does NOT include the `docs/` folder and its contents (ADR documents, cloud-topology, llm-cost, sizing, REPO_CONVENTIONS.md, SECURITY.md) in the generated output

### Expected Behavior (Correct)

2.1 WHEN a user runs `dotnet new eaa-solution -n <ProjectName>` THEN the system SHALL generate only the project structure (solution file, project scaffolding with empty layers, configuration files) without any Orders-specific sample files

2.2 WHEN a user runs `dotnet new eaa-solution -n <ProjectName>` THEN the system SHALL include the `docs/` folder with all its contents: `docs/adr/`, `docs/cloud-topology/`, `docs/llm-cost/`, `docs/sizing/`, `docs/REPO_CONVENTIONS.md`, and `docs/SECURITY.md`

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a user runs `dotnet new eaa-solution -n <ProjectName>` THEN the system SHALL CONTINUE TO rename all "Orders" occurrences to the provided project name via the `sourceName` mechanism

3.2 WHEN a user runs `dotnet new eaa-solution -n <ProjectName>` THEN the system SHALL CONTINUE TO exclude `frontend/**`, `.kiro/**`, `.github/**`, `.vscode/**`, `.template.config/**`, `**/bin/**`, `**/obj/**`, and `**/node_modules/**` from the generated output

3.3 WHEN a user runs `dotnet new eaa-solution -n <ProjectName>` THEN the system SHALL CONTINUE TO exclude `docker-compose.yml` from the generated output

3.4 WHEN a user runs `dotnet new eaa-solution -n <ProjectName>` THEN the system SHALL CONTINUE TO generate the solution file, Directory.Build.props, global.json, nuget.config, .gitignore, README.md, and CHANGELOG.md
