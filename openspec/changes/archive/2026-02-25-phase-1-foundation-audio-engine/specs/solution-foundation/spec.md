## ADDED Requirements

### Requirement: Solution structure is correctly scaffolded
The solution SHALL contain two projects: `VideoJam` (WPF Application, .NET 10, win-x64) and `VideoJam.Tests` (xUnit Test Project, .NET 10), linked by `VideoJam.sln` at the repository root.

#### Scenario: Solution builds cleanly from the command line
- **WHEN** `dotnet build VideoJam.sln` is run on a machine with .NET 10 SDK
- **THEN** the build succeeds with zero errors and zero warnings

#### Scenario: Both projects are included in the solution
- **WHEN** `VideoJam.sln` is opened in Visual Studio
- **THEN** both `VideoJam` and `VideoJam.Tests` appear in Solution Explorer

---

### Requirement: NuGet package versions are centrally pinned
All third-party NuGet package versions SHALL be declared in `Directory.Build.props` at the repository root. Individual `.csproj` files SHALL reference packages by name only, without version numbers.

#### Scenario: Package versions are locked
- **WHEN** a developer adds a package reference to any project
- **THEN** the version is resolved from `Directory.Build.props`, not from a floating range or individual project file

#### Scenario: Pinned versions match the specification
- **WHEN** `Directory.Build.props` is inspected
- **THEN** the following packages are pinned at the specified major versions: NAudio (2.2.x), LibVLCSharp (3.x), VideoLAN.LibVLC.Windows (3.x), Microsoft.Extensions.Logging (8.x), xUnit (2.x), xunit.runner.visualstudio (2.x), Microsoft.NET.Test.Sdk (current)

---

### Requirement: Source folder structure matches the specification
The `VideoJam/` project SHALL contain the following top-level source folders, each with a stub `.cs` file for every planned class: `Engine/`, `Model/`, `Services/`, `UI/`, `Input/`.

#### Scenario: All planned stub files are present and compile
- **WHEN** `dotnet build VideoJam/VideoJam.csproj` is run
- **THEN** the build succeeds and all stub class files (empty or skeleton implementations) are compiled without errors

#### Scenario: Folder layout matches the Technical Specification
- **WHEN** the project directory is inspected
- **THEN** the folders and file names match the component map in Technical Spec §3.1

---

### Requirement: Project targets win-x64 self-contained publish
The `VideoJam` project SHALL be configured for `win-x64` self-contained publish with `PublishSingleFile=false`.

#### Scenario: Publish produces a runnable output folder
- **WHEN** `dotnet publish VideoJam/VideoJam.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false` is run
- **THEN** the publish succeeds and the output folder contains `VideoJam.exe` and all required DLLs
