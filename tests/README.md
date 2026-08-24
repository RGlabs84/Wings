# Off-game test harnesses

A Valheim mod cannot normally be tested without Valheim: the code is bound to Unity types the
build only sees as reference assemblies, and the plugin only ever runs inside a game process.
That leaves the parts most likely to break silently — serialization, config migration, the
export written for outside readers — verified by nothing but launching the game and looking.

These harnesses close that gap. Each one is a plain net8 console program that **compiles the
mod's real source files**, pulled in with `<Compile Include="../../FlightLog.cs" />`, against
small stand-ins for the game surface those files touch (`Player`, `ZNet`, `ZRoutedRpc`,
`ZoneSystem`, `EnvMan`, BepInEx's `ConfigFile`). Nothing here is a copy of the shipped code, and
that is the whole point: a test that exercises a copy proves nothing about what ships.

## Running them

```bash
tests/run-tests.sh
```

Or one at a time:

```bash
dotnet run --project tests/FlightLogTests/FlightLogTests.csproj -c Release
dotnet run --project tests/ConfigMigrationTests/ConfigMigrationTests.csproj -c Release
```

Every check prints PASS or FAIL and the run exits non-zero if anything failed.

## What each one covers

### `ConfigMigrationTests`

Runs the real `ConfigMigration.Begin` → *(simulated bind)* → `Finish` cycle against committed
copies of real config files in `fixtures/`:

- `v1-stock-2.0.0.cfg` — a stock 2.0.0 file, upgrading to the 2.0.1 balance
- `v0-unstamped-1.1.x.cfg` — a 1.1.x file with no version stamp, crossing **two** migration
  steps in one pass

It checks that stock values are rebased onto the new defaults, that values an admin actually
changed survive untouched, that a backup is written before anything is modified, that a second
run is a no-op, and that a missing config file (fresh install) is handled without a migration.

The fixtures are committed rather than read out of a Gale profile on purpose. The first version
of this harness read the live profile, which made it depend on one machine and on that profile
never being edited — and it would have kept "passing" by reading a file that had drifted.

**When defaults change:** bump `CurrentConfigVersion`, add the rebase rows, then add the new
values to the `NewDefaults` table at the top of `Program.cs`. That table is what the harness
binds against, so it has to mirror `ModConfig.cs` by hand — the one piece of duplication here,
and the reason a failure in this harness sometimes means the harness is stale rather than the
migration being wrong. Check both.

### `FlightLogTests`

Drives the real tracker (`FlightLog`), the real save format (`FlightSaga`) and the real export
writer (`FlightReport`) through a simulated flight:

- tracking — time, distance, altitude, records, landing surfaces, biomes crossed
- persistence — round-trip through `Player.m_customData`, unknown keys from a newer build,
  a corrupt saga, and the guard that stops one character's saga being written onto another
- guards — a portal jump must not become a distance or speed record
- the client → server RPC, its throttle, and a player who has opted out
- both export shapes: an empty server and a populated one, checked for valid JSON, unit-suffixed
  field names, ISO-8601 timestamps and the `_notes` guards BarrkBOT reads
- a server restart, rebuilt from `flight_registry.dat`
- a player name full of JSON metacharacters

It also writes the two sample exports to a temp folder (the path is printed at the end). Those
are what to hand BarrkBOT when the export shape changes — its reader consumes them and reports
back the sentences it would actually produce.

## Adding a harness

Copy either `.csproj`, point `<Compile Include>` at the source files you want under test, and
stub only what those files actually touch. Then add the folder name to the loop in
`run-tests.sh`.

Keep them out of the plugin build: `WingsoftheValkyrie.csproj` has a `<Compile Remove="tests/**" />`
because the SDK globs `**/*.cs` by default and would otherwise try to compile the stubs into
the mod.
