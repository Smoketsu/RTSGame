<!-- Funplay Unity MCP managed project skills -->
<!-- Funplay Unity MCP skill version: unity-mcp-workflow@1.0.7 -->

# Project Compatibility And Package Readiness

Read this for package installation, unresolved namespaces, version-dependent APIs or project setup. It does not authorize installing packages or changing project settings outside the user's task.

## Establish the actual environment

- Read `ProjectSettings/ProjectVersion.txt`, requested dependencies in `Packages/manifest.json`, and resolved dependencies in `Packages/packages-lock.json`. These describe different stages; a lock-file entry alone does not prove the assembly is loaded in the connected Editor.
- Query the active Unity session and use `find_project_types` when exposed to resolve the exact namespace and assembly before generating project-specific code. Check assembly-definition references, Editor-only boundaries and the installed package's public API. Gate Unity 6 or newer package examples against the actual project; Funplay also supports Unity 2022.3.
- Identify the relevant UI framework, render pipeline, input system and existing project services. A package recommendation is not a requirement to replace working project conventions. Report an unavailable type or unsupported version rather than reflecting into internal APIs or repeatedly guessing names.
- Scope editable asset searches to the intended `Assets` directories. Inspect all plausible matches instead of choosing the first GUID; package examples and read-only assets are not project-owned defaults.

## Package operations are asynchronous

1. Confirm the required package ID/version and intended add/remove/upgrade scope. Reuse installed compatible dependencies. Do not install Localization, TMP examples, a CLI or a render pipeline merely because a reference mentions it.
2. Prefer the exposed MCP package operation. If an authorized gap requires an Editor API, use `UnityEditor.PackageManager.Client` and let the Editor update loop process its request. Never busy-wait on `IsCompleted` on the main thread or treat the initial request handle as completion.
3. Observe the request's terminal result with a bounded deadline. After reload, recover available receipts and inspect the resolved package/version; a lost response is not permission to repeat a mutation. A `prepare_editor` request key does not make arbitrary `Client.Add` calls replay-safe.
4. Use `prepare_editor` / `get_task` to establish current readiness in the intended mode. Confirm the required types are loaded and check import/compilation errors before using them. Report requested, resolved and loaded state separately when they disagree.

Use the current Editor session rather than opening a second process on its project. Only an explicitly needed, separately owned headless bootstrap may launch the Editor directly: asynchronous UPM work must keep the process alive without `-quit`, yield to Editor updates, enforce a deadline and exit its own process after completion. Never call `EditorApplication.Exit` on the user's interactive session.

## Sources and adaptation

This is original Funplay workflow guidance, informed by Unity's official plugin 0.1.8-beta at the pinned revision:

- [Package management](https://github.com/Unity-Technologies/unity-agent-plugin/tree/cf6b2da24e424b0a60d560a57f39f676cb6f79f3/skills/unity-package-management)
- [Editor search](https://github.com/Unity-Technologies/unity-agent-plugin/tree/cf6b2da24e424b0a60d560a57f39f676cb6f79f3/skills/generate-editor-search-query)
