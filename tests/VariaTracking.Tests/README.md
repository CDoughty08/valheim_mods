# VariaTracking regression checks

Run from the repository root with .NET 10 SDK:

```powershell
dotnet run --project tests/VariaTracking.Tests/VariaTracking.Tests.csproj -c Release
```

This console runner tests creature classification, XP, geometry, and trophy and name knowledge. Game API stubs supply the test state. Geometry checks compare angles and distances against separate calculations; XP checks use several frame durations. A failed test exits with a nonzero code. The runner does not load Unity or test Harmony patch installation or the game's faction rules.

To build against the installed game and check the ServerSync assembly merge:

```powershell
dotnet build VariaTracking/VariaTracking.csproj -c Release -p:DeployMod=false
```

See the [mod README](../../VariaTracking/README.md) for in-game checks.

The movement XP tests also cover VariaWeight: frame-time overshoot, changing loads, temporary skill bonuses, and character changes. Tests for selecting the nearest creatures compare the result against a full sort, using random inputs, tied distances, and different dot limits.
