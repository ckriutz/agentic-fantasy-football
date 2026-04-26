# Plan: Move Agent File Storage to Azure Blob Storage

## Problem
Agent data files (`bootstrap.md`, `profile.json`, logos) and draft state (`draft-state.json`) are currently written to the local filesystem. This ties the app to a single machine and makes it harder to run in different environments (e.g., Docker, cloud, multiple dev machines). The repo's own plan.md already calls this out as a desired change (line 413-414).

## Proposed Approach
Introduce an `IAgentStorage` abstraction that replaces direct `File.*` calls. Provide two implementations:
1. **`LocalFileAgentStorage`** — wraps the existing filesystem behavior (keeps things working as-is for local dev).
2. **`BlobAgentStorage`** — reads/writes to Azure Blob Storage using the `Azure.Storage.Blobs` SDK.

This is a straightforward swap. The files are small (markdown, JSON, images), access patterns are simple (read/write whole files), and Azure Blob Storage handles this well.

## Will This Work?
**Yes.** Here's why it's a good fit:

- **Low volume**: ~10 agents, each with 2-3 small files. Writes happen during bootstrap and occasional updates. Reads happen at agent startup and during season play.
- **Simple access patterns**: Every operation is "read entire file" or "write entire file" — no partial reads, no appends, no file locks. Blob Storage is ideal for this.
- **Agents reading/updating bootstrap.md**: Works fine. An agent reads the blob, modifies content in memory, writes the whole blob back. Since each agent only writes to its own files, there are no concurrency conflicts.
- **Images**: Blob Storage is perfect for binary content like logos. You can also generate SAS URLs or use public containers to serve logos directly to a future front-end.
- **Draft state**: `draft-state.json` is read/written sequentially during the draft (one pick at a time), so blob read/write works without contention issues.

## Blob Storage Organization

### Container
One container for the whole league — configured via `AgentStorage:ContainerName` (default: `"agentic-league"`).

### Blob naming convention
Blobs use virtual "folders" via `/` separators in the blob name. The structure mirrors what's on disk today:

```
agentic-league/                          ← container
  agents/
    agent-01/
      bootstrap.md                       ← agent's strategy & notes
      profile.json                       ← agent identity & state
      logo.png                           ← team logo (binary)
    agent-02/
      bootstrap.md
      profile.json
      logo.png
    ...
  draft/
    draft-state.json                     ← draft progress tracker
```

The **agent ID** is the folder key — it's already sanitized in code (alphanumeric, hyphens, underscores only). So the blob path for agent-01's bootstrap is just `agents/agent-01/bootstrap.md`.

### How agents find their files
Agents don't need to know about blob storage at all. The tools (`BootstrapTools`, `AgentProfileTools`) already take `agentId` as a parameter, and they build the path internally. Today that path resolves to `./agents/{agentId}/bootstrap.md` on disk. After this change, the same path string (`agents/{agentId}/bootstrap.md`) becomes the blob name instead. The agent just calls `WriteAgentBootstrap("agent-01", content)` and the storage layer handles it.

### How YOU configure it
In `appsettings.json`:

```json
{
  "AgentStorage": {
    "Provider": "AzureBlob",
    "ContainerName": "agentic-league",
    "ConnectionString": ""
  }
}
```

- **`Provider`**: `"Local"` (default, uses filesystem) or `"AzureBlob"`.
- **`ContainerName`**: The blob container name. Only used when Provider is AzureBlob.
- **`ConnectionString`**: Set via environment variable `AZURE_STORAGE_CONNECTION_STRING` (never in appsettings). The appsettings entry is just a placeholder.

For local dev, you don't need to change anything — it defaults to `"Local"` and writes to disk exactly as it does today.

### Naming rules summary
| File | Blob path | Built from |
|------|-----------|------------|
| Agent bootstrap | `agents/{agentId}/bootstrap.md` | `agentId` param |
| Agent profile | `agents/{agentId}/profile.json` | `agentId` param |
| Agent logo | `agents/{agentId}/logo.png` | `agentId` param |
| Draft state | `draft/draft-state.json` | hardcoded |

The `agentId` is sanitized (regex strips anything that isn't `[a-zA-Z0-9\-_]`), so blob names are always safe.

## Files to Change

### New Files
- `src/AgenticLeague/Storage/IAgentStorage.cs` — interface with `ReadAsync`, `WriteAsync`, `ExistsAsync`, `ReadBytesAsync`, `WriteBytesAsync`
- `src/AgenticLeague/Storage/LocalFileAgentStorage.cs` — wraps current `File.*` logic
- `src/AgenticLeague/Storage/BlobAgentStorage.cs` — Azure Blob implementation

### Modified Files
- `src/AgenticLeague/Tools/BootstrapTools.cs` — inject `IAgentStorage` instead of using `File.*` directly
- `src/AgenticLeague/Tools/AgentProfileTools.cs` — inject `IAgentStorage` instead of using `File.*` directly
- `src/AgenticLeague/DraftRunner.cs` — inject `IAgentStorage` for `draft-state.json` read/write
- `src/AgenticLeague/Agents/FantasyAgent.cs` — wire up `IAgentStorage` when constructing tools
- `src/AgenticLeague/Program.cs` — register `IAgentStorage` in DI based on config
- `src/AgenticLeague/AgenticLeague.csproj` — add `Azure.Storage.Blobs` package

### Optional / Later
- `src/AgenticLeague/Tools/ImageGenerationTool.cs` — could download generated images and store them in blob storage instead of just returning URLs (addresses the "logos don't last long" note in plan.md line 414)

## Todos

1. **create-storage-interface** — Define `IAgentStorage` interface
2. **create-local-impl** — Implement `LocalFileAgentStorage` (wrap existing behavior)
3. **create-blob-impl** — Implement `BlobAgentStorage` using `Azure.Storage.Blobs`
4. **refactor-bootstrap-tools** — Update `BootstrapTools` to use `IAgentStorage`
5. **refactor-profile-tools** — Update `AgentProfileTools` to use `IAgentStorage`
6. **refactor-draft-runner** — Update `DraftRunner` to use `IAgentStorage`
7. **wire-up-di** — Register storage in DI, configure provider toggle
8. **update-fantasy-agent** — Pass storage through when constructing tools
9. **add-blob-package** — Add `Azure.Storage.Blobs` to csproj
10. **build-and-verify** — Build and verify no regressions

## Notes
- No database changes needed — this is purely a file storage concern.
- The `LoadPrompt` method in `FantasyAgent.cs` reads embedded prompt files from the build output. These are static and ship with the app — they should stay as local files, not moved to blob storage.
- The path convention in blob storage would mirror the current local structure: `agents/{agentId}/bootstrap.md`, `agents/{agentId}/profile.json`, `draft-state.json`.
