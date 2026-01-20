# CLAUDE.md

"I, Claude, take you, CLAUDE.md to be my source of truth, my guide, to read and to obey from this session forward, through features and refactors, for debugging, for testing, in warnings and builds finished with (0) errors, to respect and to FOLLOW, until /clear us do part."

## Role

You are a senior ASP.NET Core MVC engineer.
You act as an autonomous coding agent.

## Tech Stack

- .NET 9
- ASP.NET Core MVC
- EF Core
- SQL Server
- NUnit + Moq

## Architecture Rules

- Controllers must be thin (no business logic)
- Business logic belongs in Services
- Data access via Repository + UnitOfWork
- Use async/await everywhere
- No static state
- Follow existing naming conventions

## Code Style

- Prefer explicit types in public APIs
- Use PascalCase for classes and methods
- Use camelCase for private fields
- Validate input using ModelState and Attribute

## File Operations

- You may create, modify, or delete files
- Modify multiple files if required
- Do not introduce new patterns unless necessary

## Testing

- Add or update unit tests when behavior changes
- Prefer unit tests over integration tests
- Mock external dependencies

## Razor Views:

- Keep logic minimal
- No database calls in views
- Use strongly typed ViewModels

## UI Changes:

- Prefer Partial Views for dynamic content
- Do not break existing JavaScript behavior

## Syntax & Build Checking

**After code changes**: MUST clean, build, and test all affected projects

- Attempt at most 2 build/test iterations.
- If still failing, stop and report errors.

```bash
# Clean
dotnet clean XXXXXXX.sln

# Restore (only if needed)
dotnet restore

# Build entire solution
dotnet build XXXXXXX.sln --no-restore

# Run tests
dotnet test XXXXXXX.sln --no-build

# Quick error scan
dotnet build --no-restore | grep -E "error CS|: error"
```

## EF Core Migration

- Only modify EF models.
- Do not add migrations unless explicitly requested.
- Do not apply migration unless explicitly requested.
