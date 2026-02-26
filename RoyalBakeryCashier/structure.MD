# RoyalBakeryCashier — Project Structure & Collaboration Guide

Short, actionable rules and a canonical layout to avoid merge conflicts and keep feature work aligned across the team.
This solution targets .NET 9 and uses .NET MAUI.

---

## Quick overview
- Solution: `RoyalBakeryCashier.sln`
- App project: `RoyalBakeryCashier` (MAUI, UI)
- Data project: `RoyalBakeryCashier.Data` (EF Core DbContext & migrations)
- Tests (optional): `RoyalBakeryCashier.Tests`
- Recommended pattern: MVVM (Views in `Pages`, viewmodels in `ViewModels`, models in `Models`, services in `Services`)

---

## Canonical directory layout
- `RoyalBakeryCashier/`
  - `App.xaml`
  - `MauiProgram.cs`
  - `Pages/` (XAML pages + code-behind)
    - `PosPage.xaml`
    - `PosPage.xaml.cs`
    - ...
  - `ViewModels/`
    - `PosViewModel.cs`
    - ...
  - `Models/`
    - `Item.cs`
    - `CartItem.cs`
    - ...
  - `Services/`
    - `IInventoryService.cs`
    - `InventoryService.cs`
    - ...
  - `Resources/`
    - `Images/`
    - `Fonts/`
    - `Styles/`
  - `wwwroot/` (if needed)
- `RoyalBakeryCashier.Data/`
  - `StockDbContext.cs`
  - `Entities/`
  - `Migrations/`
- `RoyalBakeryCashier.Tests/`
- `.editorconfig`
- `.gitignore`
- `README.md`

Use backticks for filenames when referencing resources (for example: `PosPage.xaml`, `StockDbContext.cs`).

---

## Coding & naming conventions
- Public types / members: `PascalCase`.
- Private fields: `_camelCase`.
- Async methods: suffix with `Async`.
- ViewModels implement `INotifyPropertyChanged`.
- Collections bound to UI: `ObservableCollection<T>`.
- Prefer bindings & commands over code-behind.

---

## XAML / MAUI guidelines
- Use bindings, `Command`s and `ICommand` for interactions.
- Use `x:Name` only when needed.
- Centralize colors/styles in `Resources/Styles`.
- Images: `Resources/Images`; fonts: `Resources/Fonts`.
- Register services and DbContext in `MauiProgram.cs`.

---

## Data / EF Core
- Keep `DbContext` and migrations inside `RoyalBakeryCashier.Data`.
- Migration workflow:
  - Create migrations: `dotnet ef migrations add <Name> -p RoyalBakeryCashier.Data -s RoyalBakeryCashier`
  - Apply locally: `dotnet ef database update -p RoyalBakeryCashier.Data -s RoyalBakeryCashier`
- Migration coordination:
  - Prefer one migration per feature.
  - If multiple devs create migrations, coordinate to rebase/sequence them or assign a migration owner.

---

## Git & branching rules
- Branch naming:
  - `main` (protected), `develop` (optional)
  - `feature/<issue>-short-desc`
  - `bugfix/<issue>-short-desc`
- Workflow:
  1. Pull latest `main` before creating a branch.
  2. Create small, focused branches.
  3. Rebase onto `main` (team policy) before PR.
- PR checklist:
  - CI green, linked issue, testing steps, screenshots for UI changes.
  - At least one approving review.

---

## Merge & conflict avoidance
- Pull and rebase frequently.
- Keep XAML/UI changes small to reduce conflicts.
- Coordinate on shared resources (`Resources/Styles`, images, migrations).
- Resolve resource conflicts by picking the higher-quality or updated asset and document decision in PR.

---

## Visual Studio etiquette
- Before large changes: __Build Solution__ and __Run__ to verify.
- Use __Team Explorer > Branches__ to manage branches.
- Configure code style via __Tools > Options > Text Editor > C# > Code Style__ or maintain `.editorconfig`.
- Use __Manage NuGet Packages__ to update/add packages.

---

## CI / Tests
- CI pipeline should restore, build, and run tests.
- Run static analyzers if configured.
- Tests live in `RoyalBakeryCashier.Tests`.

---

## Developer workflow (short)
1. Pull latest `main`.
2. Create `feature/<ticket>-desc`.
3. Implement small commits with clear messages.
4. Run `dotnet build` and tests locally.
5. Push branch and open PR with description & testing steps.
6. Address review comments, rebase if required, merge when green & approved.

---

## Useful commands
- Build: `dotnet build`
- Run app: `dotnet run -p RoyalBakeryCashier`
- Add migration: `dotnet ef migrations add Name -p RoyalBakeryCashier.Data -s RoyalBakeryCashier`
- Apply migrations: `dotnet ef database update -p RoyalBakeryCashier.Data -s RoyalBakeryCashier`
- Run tests: `dotnet test`

---

If you want, I can also:
- Create a repo-ready `.editorconfig`, `.gitignore`, and `README.md`.
- Generate a `PR_TEMPLATE.md`.
- Scan the workspace for remaining references to `RoyalBakeryItemSale` and prepare a safe rename plan.