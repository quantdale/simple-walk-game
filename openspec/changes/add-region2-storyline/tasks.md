## 0. Gate
- [ ] 0.1 Michael approves Region 2 at the post-MVP decision gate (ROADMAP, D-011). Blocked until M9 exits (KI-002). **No task below may start before this.**

## 1. Domain (first slice)
- [ ] 1.1 `WorldDefinition` and Region 2 unlock rule (Region 1 closure); tests in `tests/WalkGame.Domain.Tests/WorldDefinitionTests.cs`
- [ ] 1.2 `Region2Catalog` prologue + 3 projects; validator-clean; `tests/WalkGame.Domain.Tests/Region2CatalogTests.cs`

## 2. Save
- [ ] 2.1 Schema bump, `ISaveMigration`, mature-save fixture test (`tests/WalkGame.Infrastructure.Tests/`)

## 3. Application and shell
- [ ] 3.1 Region offer on Home; region switch read model; EditMode tests

## 4. Verification
- [ ] 4.1 Full .NET suite, EditMode, content-compatibility, pacing and save-growth gates green; Region 1 pacing evidence byte-identical
