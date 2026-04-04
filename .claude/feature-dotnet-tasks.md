---
status: Stable
updated: 2026-04-05 00:45h
---

# Feature: Auto-discover standard .NET CLI tasks from .csproj

Implemented in `CsprojDiscoverer.cs`. Auto-generates `dotnet: watch`, `dotnet: test`, and `dotnet: test (watch)` for web/test projects based on SDK attribute and PackageReferences.

## Future ideas (not yet implemented)

| Task | Command | Condition |
|---|---|---|
| `dotnet: publish` | `dotnet publish -c Release` | Always |
| `dotnet: ef database update` | `dotnet ef database update` | Has EF Core tools PackageReference |
| `dotnet: run` | `dotnet run` | Already covered by launchSettings.json profiles |
| `dotnet: format` | `dotnet format` | Always |
| `dotnet: user-secrets` | `dotnet user-secrets` | Interactive, needs external terminal |
