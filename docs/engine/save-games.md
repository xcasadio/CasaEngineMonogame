# Save games

A generic runtime service that saves the player's game to named slots and loads it back
(`CasaEngine/Framework/SaveGames`, ADR-0044). Modelled on Unreal's `USaveGame` / `SaveGameToSlot`,
Godot's `user://` and Unity's `persistentDataPath`: the game fills a save-game object, the service writes
it to a slot in a per-user folder, in readable JSON or compact binary.

## Usage

The game implements `ISaveGameData`. `Serialize` is called both to write and to read: each
`archive.Value(name, ref field)` writes the field when saving and overwrites it when loading.

```csharp
public sealed class MySave : ISaveGameData
{
    public int Hp;
    public string MapName = string.Empty;
    public readonly uint[] Flags = new uint[64];

    public int LatestDataVersion => 2;

    public void Serialize(SaveGameArchive archive)
    {
        archive.Value("hp", ref Hp);
        archive.Value("map", ref MapName);

        archive.BeginObject("progress");
        archive.Value("flags", Flags); // fixed length: a file with another length is refused
        archive.EndObject();

        if (archive.DataVersion >= 2)
        {
            // fields added in data version 2 go here
        }
    }
}

SaveGameService saves = GameSettings.SaveGames;

SaveGameSaveResult saved = saves.Save("slot1", mySave, SaveGameFormat.Binary,
    new Dictionary<string, string> { ["chapter"] = "3" });

SaveGameLoadResult loaded = saves.TryLoad("slot1", out MySave data);
if (loaded.IsLoaded)
{
    // validate every value, then apply `data` as a whole (see "Trust model")
}

IReadOnlyList<SaveGameSlotInfo> slots = saves.ListSlots(); // name, format, version, metadata, write time
saves.Delete("slot1");
```

- `Value` exists for `bool`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `float`, `string`, and for
  fixed-length arrays of `byte`, `short`, `ushort`, `int`, `uint` and `long`. `BeginObject` / `EndObject`
  nest fields (at most `SaveGameArchive.MaxObjectDepth`, 32 levels).
- The calls must be the same, in the same order, in both directions: the binary format is positional, only
  JSON finds fields by name. Field names are unique inside one object.
- Slot names match `^[a-z0-9_-]{1,32}$` and are not Windows reserved device names. A refused name throws
  `ArgumentException`.

## Where files go

`LocalApplicationData/<ProjectName>/SaveGames/<slot>.sav`, resolved at the first I/O call (not at
construction), from `GameSettings.ProjectSettings.ProjectName`. A default, empty or invalid project name
throws `InvalidOperationException` at that first call; it is never silently sanitized. Writes made under
AppData from a sandboxed tool (for example the Claude app) can be virtualized: run manual checks outside it.

## Formats

- **JSON**: `{ "container": "casaengine-savegame", "containerVersion": 1, "dataVersion": n,
  "metadata": { … }, "data": { … } }`, UTF-8 without BOM, integers written as integers, floats written so
  they read back bit for bit. Meant for debugging and hand editing; it has no checksum.
- **Binary**: magic `CESG`, container version, data version, metadata pairs, then the positional
  little-endian payload and a CRC-32 over everything before it.
- Writing the same object twice gives the same bytes in both formats. The format is detected when reading.

## Results and errors

- `Save` returns `Saved`, `TooLarge` (the serialized save exceeds `MaxSlotSizeBytes`, 1 MiB),
  `InvalidData` (a refused value, such as a non-finite float or a null string) or `IoError`. Nothing is
  written unless the result is `Saved`; a failed write leaves the previous slot content.
- `TryLoad` returns `Loaded`, `NotFound`, `TooLarge`, `Corrupted` (binary truncated or CRC mismatch),
  `UnsupportedContainer`, `NewerDataVersion` (written by a newer build: refused), `InvalidData` (the message
  names the field) or `IoError`. `data` is `default` for every result other than `Loaded`: the game never
  sees a half-filled object.
- `Delete` returns `Deleted`, `NotFound` or `IoError`. `ListSlots` lists every slot, marking unreadable ones
  with their reason instead of throwing; `NotFound` is not logged, every other failure is.
- Developer errors throw (invalid slot name, invalid project name, unbalanced `BeginObject`/`EndObject`, a
  duplicate JSON field name, a negative `LatestDataVersion`), and an exception thrown by the game's own
  `Serialize` propagates.

## Trust model and consumer contract

- CRC-32 detects accidental corruption only; save files are not authenticated; all bounds checks and consumer validation must hold for attacker-crafted files.
- Reading is defensive: a file never chooses what type is created (the caller names it), sizes are capped
  and checked before any allocation, and an unreadable file becomes a result, never an exception into the
  game loop.
- The archive only guarantees each value's shape and the range of its C# type. **The game validates what
  every value means** (ids, indices, counters, coordinates) after `Loaded` and **before** touching its live
  state, then applies the object as a whole or not at all.
- Metadata is untrusted plain text: bound its length when displaying it, and display it with MGUI's inline
  formatting disabled.

## Limits

- Synchronous API; no encryption, compression, cloud saves or file authentication.
- The replace-on-write keeps either the old or the new content as far as the file system guarantees it; the
  durability of the rename itself is not guaranteed. Orphaned `.tmp` files are not cleaned up.
- One storage backend (files), one local user.
