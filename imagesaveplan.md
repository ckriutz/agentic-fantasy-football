# Image Save Plan

The cleanest approach is to make `ImageGenerationTool` agent-scoped, save the generated image into `AgentData/{agentId}`, and return a local filename like `logo.png` instead of the temporary URL.

1. In `FantasyAgent.CreateFantasyAgentAsync`, construct the tool with the current `agentId`, for example `new ImageGenerationTool(agentId)`.
2. In `ImageGenerationTool`, keep the existing `POST images/generations`, but after parsing the returned `url`, immediately `GET` that URL, read the bytes, and write them to `Path.Combine(_rootPath, "AgentData", safeAgentId, fileName)`.
3. Return `fileName` or a relative path like `logo.png` from `GenerateImage`, not the remote URL.
4. Update the agent instructions in `FantasyAgent.cs` so they say the tool returns the saved image name/path, and the agent should store that in `bootstrap.md` and `profile.json`.

A straightforward shape would be:

```csharp
public sealed class ImageGenerationTool
{
    private readonly string _rootPath;
    private readonly string _agentId;
    private readonly HttpClient _httpClient;

    public ImageGenerationTool(string agentId, ILogger<ImageGenerationTool>? logger = null, string? rootPath = null)
    {
        _agentId = GetSafeAgentId(agentId);
        _rootPath = rootPath ?? Directory.GetCurrentDirectory();
        ...
    }

    public async Task<string> GenerateImage(string description)
    {
        // 1. create image
        // 2. parse URL
        // 3. download bytes from URL
        // 4. save as AgentData/{agentId}/logo.png
        // 5. return "logo.png"
    }
}
```

Two implementation details matter:

- Sanitize the agent id the same way `AgentProfileTools` does, ideally by extracting that logic into a shared helper so both classes use the same folder rules.
- Pick the file extension from the download response `Content-Type` such as `image/png` or `image/jpeg`, with a fallback like `.png`.

That gives the agent a stable, local asset reference and removes the dependency on the temporary external URL.
