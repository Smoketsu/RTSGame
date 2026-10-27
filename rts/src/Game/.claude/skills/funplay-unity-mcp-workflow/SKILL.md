---
name: funplay-unity-mcp-workflow
description: Efficient workflow for using Unity MCP to edit, import, compile, inspect, and test Unity projects, including screenshot and Game View recording verification.
---
<!-- Funplay Unity MCP managed project skills -->
<!-- Funplay Unity MCP skill version: unity-mcp-workflow@1.0.7 -->

# Unity MCP Workflow

Use this skill when Codex or another AI agent is working in a Unity project and needs to verify code, prefabs, UI, Play Mode behavior, screenshots, Game View recordings, scene hierarchy, console logs, domain reloads, or MCP connection issues.

## MCP-First Unity UI Operations

- Do not use computer use (desktop mouse/keyboard automation) to operate Unity unless necessary. When assembling, modifying, inspecting or validating UI, prefer Unity MCP whenever it can complete the step, including hierarchy/component/prefab reads and edits, compilation/Play state, clicks/scrolling, screenshots and recordings.
- Check the connected project's tools/list and, when available, `get_tool_capabilities`. A tool missing from exposure, compilation/domain reload or a temporary disconnection is not evidence of a missing capability: check exposure/readiness and recover status first. Respect custom allowlists; do not widen exposure or use another interaction method to bypass restrictions.
- Prefer specialized MCP tools; for project-specific gaps they do not cover, use a permitted, guarded `execute_code` call through Unity Editor APIs when it can perform the step reliably. Computer use is a fallback only for a confirmed MCP capability gap, or an explicit user request: explain the uncovered step before using it, limit it to that step, and return to MCP readback/validation when available. If recovery fails, report the connection blocker rather than silently switching methods or repeating uncertain mutations.
- A confirmed blocking native Unity modal that MCP cannot dismiss is a narrow computer-use exception. Explain the modal and handle only that blocker, then return to MCP and recheck project identity, Editor state and the interrupted operation. Do not infer permission to save, discard, overwrite, install, authorize or accept security changes merely to unblock Unity; ask when the consequential choice is not already authorized. Never replay an uncertain mutation.
- This routing applies to operating Unity, not ordinary source-file editing or viewing supplied design references and already-captured images/videos with appropriate file or media tools.


## Version And Package Compatibility

For package setup, unresolved types or version-dependent APIs, read [Project compatibility and package readiness](references/unity-mcp-workflow/project-compatibility.md). Distinguish requested, resolved and actually loaded packages before using their APIs. Keep the current MCP session and the project's Unity version; this skill does not require Unity 6 or the Unity CLI.

For identity guards, strict input, PlayerLoop stalls, owned verification cleanup, serialized-reference protection and hash-validated continuation, read [Execution trust and faithful verification](references/unity-mcp-workflow/execution-trust.md). Keep compile, resource, input, render and application acceptance separate.


## Operating Loop

1. Establish context.
   - Confirm the Unity project root and active scene.
   - Check that Unity MCP is reachable before assuming Editor state.
   - Inspect hierarchy, prefab paths, selected objects, and relevant component references through MCP.
   - If the user names an object, treat the name as a hint and verify the real Unity object path before editing.
2. Choose the edit surface.
   - Edit source files with normal repo tools, then trigger Unity recompilation.
   - Edit scene objects through Unity APIs, mark the scene dirty, and save the scene.
   - Edit prefab fields with `set_prefab_property(ies)` when available. Use `PrefabUtility.LoadPrefabContents`, `SaveAsPrefabAsset`, and `UnloadPrefabContents` for structural changes.
   - Unless the user explicitly requests a full rebuild, preserve the existing hierarchy when editing UI or GameObject prefabs and modify only the required objects, components, and serialized fields; do not recreate the entire prefab.
   - Edit ScriptableObject assets through `SerializedObject`, `EditorUtility.SetDirty`, and `AssetDatabase.SaveAssetIfDirty` / `SaveAssets`.
   - Never patch `.unity`, `.prefab`, or `.asset` YAML with shell text tools.
   - If the user is looking at an open scene instance, update the visible scene instance as well as the prefab asset when appropriate.
3. Execute changes.
   - Prefer structured query/edit/audit tools for supported work. Use a guarded `execute_code` batch only for project-specific gaps, after resolving full type names and assemblies when needed.
   - Use null guards for every object, component, asset, and path lookup.
   - Return explicit missing-path/object/component messages that include the expected path and the scene or prefab searched.
   - Return concise before/after values from snippets.
   - Save only the assets or scenes intentionally modified.
   - Do not run self-healing fallback loops; if a reference, path, package, or tool is missing, report it once and stop or skip that item.
4. Validate.
   - Read back the changed objects through MCP.
   - For code or resource edits, use `prepare_editor` and poll its durable operation ID to verified readiness in the intended mode, then inspect compilation errors and console errors.
   - For runtime behavior, enter Play Mode or inspect live objects when needed.
   - If MCP is unreachable, do not claim scene, prefab, asset, or runtime verification.
   - Report exactly what was verified and what still requires device, store, network, or manual validation.

## Unity Serialized Asset Safety

- Do not use shell text tools, scripts, or patches to modify `.unity`, `.prefab`, or `.asset` files. These are Unity-owned serialized assets; changing them outside Unity can corrupt file IDs, prefab overrides, references, import state, or scene dirtiness.
- Shell tools may inspect or locate serialized Unity assets, but scene, prefab, and ScriptableObject modifications must go through Unity MCP tools or Editor APIs.
- For scenes, modify live objects through Unity APIs, mark only the touched scene dirty, and save that scene.
- For prefabs, use Prefab Mode tools or `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`.
- For ScriptableObjects or other `.asset` files, load the asset with `AssetDatabase`, modify serialized properties through `SerializedObject` when possible, mark that asset dirty, and save only that asset.
- If Unity readback and raw file text disagree, trust Unity readback and investigate the asset path instead of hand-editing YAML.

## Tool Exposure

- With default `core` exposure, prefer structured inspection/editing, audits, durable preparation, unified task reads, project-aware UI creation and visual evidence. `execute_code` remains a fallback for project-specific gaps.
- `full` retains legacy status and compile/Play tools, history, project-default configuration, preview management, explicit recording markers and specialized diagnostics. Check exposure before choosing those workflows; do not silently widen a custom list.
- Use `get_tool_capabilities` to distinguish implemented/enabled/exposed tools. Respect customized allowlists; report missing exposure rather than claiming an implementation does not exist.

## MCP Call Pattern

If native MCP tools are not directly available, probe the local HTTP endpoint. The port is
per project, so read it from the Funplay MCP Server window (it is also the port in the
configured client entry) instead of assuming a fixed one:

```bash
PORT=24312 # replace with the port shown in the Funplay MCP Server window
curl -sS -m 1 -X POST http://127.0.0.1:$PORT/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

For multi-line `execute_code` calls over curl, generate JSON with a real encoder instead of hand-escaping C#:

```bash
node - <<'NODE'
const code = String.raw`
using UnityEngine;

public class InspectSomething
{
    public static string Run()
    {
        var obj = GameObject.Find("PracticeInGameUiRoot");
        return obj != null ? obj.name : "not found";
    }
}
`;
const payload = {
  jsonrpc: "2.0",
  id: 1,
  method: "tools/call",
  params: { name: "execute_code", arguments: { code } }
};
process.stdout.write(JSON.stringify(payload));
NODE
```

## Recommended `execute_code` Template

For non-trivial snippets, prefer `IFunplayCommand` over the legacy `public static string Run()` template. `execute_code` auto-adds `using Funplay.Editor.Tools.Scripting;` when `IFunplayCommand` is used, but include it explicitly in generated snippets for readability:

```csharp
using Funplay.Editor.Tools.Scripting;
using UnityEngine;

public class CommandScript : IFunplayCommand
{
    public void Execute(ExecutionContext ctx)
    {
        var root = GameObject.Find("PracticeInGameUiRoot");
        if (root == null)
        {
            ctx.LogWarning("PracticeInGameUiRoot not found");
            ctx.ReturnValue = "missing root";
            return;
        }

        ctx.RegisterObjectModification(root);
        ctx.Log("Found {0}, active={1}", root.name, root.activeInHierarchy);
        ctx.ReturnValue = new
        {
            name = root.name,
            active = root.activeInHierarchy
        };
    }
}
```

Use `ctx.RegisterObjectCreation(obj)`, `ctx.RegisterObjectModification(obj)`, and `ctx.DestroyObject(obj)` instead of direct Undo calls when possible. Use `ctx.Log`, `ctx.LogWarning`, and `ctx.LogError` for output returned in the MCP response without polluting the Unity Console.

## Unity C# Patterns

Add explicit `using` directives or use fully qualified types for project code. `execute_code` does not auto-inject project namespaces by default:

```csharp
var root = UnityEngine.GameObject.Find("PracticeInGameUiRoot");
var rect = root.GetComponent<UnityEngine.RectTransform>();
```

Use Unity null semantics for `UnityEngine.Object` references:

```csharp
if (image == null)
{
    return "Image missing";
}
```

Do not use `??=` to lazily resolve or rebind `UnityEngine.Object` references. Unity's destroyed or unbound serialized references can be fake-null: `field == null` returns true through Unity's overloaded operator, while C# `??=` can still treat the managed wrapper as non-null and skip the fallback assignment. Use an explicit Unity-null check instead:

```csharp
if (_hud == null)
{
    _hud = GetComponentInChildren<MyHud>(true);
}
```

For prefab edits:

```csharp
var path = "Assets/MyGame/UI/Prefabs/PF_PracticeInGameUiRoot.prefab";
var prefab = UnityEditor.PrefabUtility.LoadPrefabContents(path);
try
{
    var target = prefab.transform.Find("SafeArea/SwingCancelZone");
    if (target == null)
    {
        return "SwingCancelZone not found in prefab";
    }

    var rect = target.GetComponent<UnityEngine.RectTransform>();
    var before = rect.anchoredPosition;
    rect.anchoredPosition = new UnityEngine.Vector2(-76f, 448f);

    UnityEditor.EditorUtility.SetDirty(rect);
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefab, path);
    UnityEditor.AssetDatabase.SaveAssets();
    return "Prefab saved: pos " + before + " -> " + rect.anchoredPosition;
}
finally
{
    UnityEditor.PrefabUtility.UnloadPrefabContents(prefab);
}
```

For scene edits:

```csharp
var obj = UnityEngine.GameObject.Find("PracticeInGameUiRoot/SafeArea/SwingCancelZone");
if (obj == null)
{
    return "Scene object not found";
}

var rect = obj.GetComponent<UnityEngine.RectTransform>();
var before = rect.sizeDelta;
UnityEditor.Undo.RecordObject(rect, "Update cancel zone");
rect.sizeDelta = new UnityEngine.Vector2(220f, 116f);
UnityEditor.EditorUtility.SetDirty(rect);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(obj.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(obj.scene);
return "Scene saved: size " + before + " -> " + rect.sizeDelta;
```

## Recompile And Reload

After external C# or asset file edits:

1. Call `prepare_editor` with target=edit or play, refresh_assets=true and a unique request_key.
2. Retain data.task.task_id and use `get_task` with bounded waiting and the last revision. If HTTP drops during reload, reconnect and repeat the status read with that handle or kind=editor and the original request_key.
3. Continue only when operation.status=ready, current_editor.ready=true and current_editor.is_playing matches the intended mode.
4. Stop on failed/cancelled/interrupted status; inspect compiler errors, phase history, deadline and current state before deciding the next action.
5. Check console/runtime initialization separately. Preparation does not prove business logic, visual fidelity or event routing.

Do not replay an interrupted arbitrary mutation. Its outcome is unknown until exact object/asset readback establishes what executed.

If durable preparation is unavailable in an older/customized configuration, use the legacy exit/request_recompile/wait/error-check/enter sequence and explicitly read back state after reconnection. `get_reload_recovery_status` is historical information, not a readiness flag.

## Verification Checklist

Use structured component/property readback that returns exact values and persistence state. Resolve all candidates with `find_game_objects`, choose the verified ID, and query `get_component_properties`; never select the first duplicate name. Only use a guarded readback snippet when the structured API cannot express the project-specific check.

For UI work, verify prefab or scene hierarchy, sprite references, anchors, sorting order, active state, text fit, and button listeners. A populated `Content` hierarchy does not prove the user can see the UI.

For gameplay or network work, verify object identity, ownership, live instance existence, transform values, animation state, visibility, and whether client-side filters are discarding valid data.

## Failure Handling

- If MCP is unreachable, say so and fall back only to safe filesystem inspection or code edits. Do not claim scene, prefab, or runtime verification without Unity readback.
- If an object lookup fails, inspect hierarchy and prefab contents instead of inventing a path.
- If multiple matching objects exist, print their paths and choose the one matching the user-visible UI or current scene.
- If a reference, package, tool, or path is missing, return one clear error and stop or skip that item. Do not loop through guessed fallback paths, create replacement objects silently, or report success after a best-effort fallback.
- If compile errors appear after a change, fix them before Play Mode validation.
- When Unity and text files disagree for serialized scene or prefab state, trust Unity readback and inspect the asset path.

## Structured UI Workflows

- Short MCP tasks briefly wait for completion (wait_seconds defaults to 2; zero returns immediately). For longer tasks use `get_task(data.task.task_id, wait_seconds=20, after_revision=<last revision>)`; it waits for completion or a meaningful state change. Honor poll_after_ms on unchanged responses instead of making the model poll every second. wait_complete is not proof of success: inspect native status, errors and ready/complete/restoration fields. A read_timeout carries only the snapshot_at observation. Cancellation of the HTTP wait does not cancel the task. On lost preparation/preview responses recover through kind + the original request_key; do not replay mutations. Recording and Test Runner starts return immediately, as do preparation/preview starts without a recovery key.
- Inspect before modifying: use `find_game_objects` with component/property filters and projections, `inspect_ui_sprites` for Image/effective Sprite/importer/border/local-ID associations, and `find_project_types` for exact type and assembly names. Check ambiguity, partial errors, scan completeness and pagination; an incomplete scan is not proof of absence. Component setters distinguish live in-memory readback from saved/reimported prefab values.
- Run `audit_ui` on relevant live roots or saved prefabs/scenes; small scans can finish in one call, otherwise read status and finding pages through `get_task`. It checks missing borders, missing/required references, transparent raycast blockers, text/clipping and layout conflicts without fixing or saving assets. Review measured evidence and contextual warnings; suppress intentional exceptions only with an explicit project reason. Do not invent border values or infer design fidelity from a clean audit.
- Before creating new UI, read `get_ui_defaults`. `create_project_ui` can reuse templates and retain their prefab connection, label bindings, font/material and authored geometry. Explicit overrides take precedence; existing template component types are not converted. `configure_ui_defaults` changes project-scoped authoring preferences, so use it only when that shared convention is intended. Tied/incomplete convention scans or missing TMP resources require a deliberate choice/action, never a silent legacy fallback. This is Edit Mode authoring: save the intended scene/prefab explicitly and preserve existing UI when revising it.
- When preview management is exposed (Full by default), use `start_ui_preview_session` with verified prefab_paths and/or a project scene_template, optionally enter_play_mode and target width/height. It needs saved clean original scenes and no open Prefab Stage; do not save/discard unrelated user work merely to satisfy this precondition. Retain session_id and data.task.task_id; use `get_task` until ready. Business data and initialization remain project-specific; entering the scene may run lifecycle code.
- End the matching session with `end_ui_preview_session`, then inspect scenes_restored, view_restored, selection_restored, assets_cleaned and warnings. Do not claim full restoration from a success envelope. Changed scene setup, dirty preview or modified temporary scene requires inspection; discard_preview_changes applies only to the owned preview scene and must reflect an intended discard. Network/save-game effects and source asset edits are not rolled back. Preserve user-created files and changed window choices; report recovery still needed.
- Use screenshot `geometry`, not an unrelated `Screen` size: render size and returned image size can differ. Pass coordinate_space=image_pixels, origin=top_left and a fresh capture_id to click/drag/scroll or `raycast_at_point` when measuring a screenshot. `get_object_screen_bounds` and `get_visual_coordinates` share the mapping. Expired IDs or changed mode/view/scene/camera viewport/render dimensions require a fresh capture, not clamping or guessing. Geometry validity does not prove animated content stayed unchanged.


## Game View Recording

Use `capture_game_view` for static layout or a single visual state. Use `record_game_view` when the task needs evidence over time, such as animation, transitions, or a reproducible interaction sequence; do not record every routine UI edit.

1. Prepare. Use `prepare_editor` targeting play (or a ready preview session) and verify current readiness. Recording requires a graphics-enabled macOS or Windows Unity Editor with a visible, rendering Game tab. Keep that tab visible and its resolution unchanged throughout capture; hiding it or resizing the source can fail the recording. The MP4 includes overlay UI but no audio.
2. Start a short, bounded clip before performing the relevant actions. For example, call `record_game_view` with:

   ```json
   {"action":"start","duration_seconds":10,"fps":15,"max_dimension":1280}
   ```

   Save `data.recording_id` from the response, then perform the interaction. Start returns immediately; recording stops automatically at the duration limit. These are the default settings; accepted ranges are 1-120 seconds, 1-60 fps, and a 128-1920 pixel maximum edge. Aspect ratio is preserved without upscaling. Prefer a shorter clip or lower sampling rate/resolution if capture overhead is disruptive.
3. After performing the interactions, use `get_task` with the returned data.task.task_id for bounded status waits. To finish early, use `record_game_view` with `{"action":"stop","recording_id":"<returned id>"}`, then query the matching task if finalization is still pending. Legacy action=status remains compatible. Always pass the saved ID so a stale request cannot inspect or stop a newer recording. If another recording is already active, report it rather than stopping someone else's capture.
4. Check the receipt, not just `success`. While `data.status` is `recording` or `stopping`, the file is not ready. Read the MP4 only when `data.ready=true`; a `success=true` status query can still describe a failed recording. Stop polling on terminal `completed`, `interrupted`, or `failed` status and inspect `error`, `stop_reason`, and the actual captured extent (`frame_count`, `elapsed_seconds`, `last_frame_seconds`). Leaving Play Mode or reloading scripts finalizes early; recover the receipt after reload and treat any usable partial clip as partial evidence, not a complete test.
5. Review the actual file at `data.path`, under `<UnityProject>/Library/FunplayMcp/Recordings/`. MCP returns a local-file receipt, not video bytes or base64; the client must have access to that filesystem and a video viewer. A remote MCP connection alone does not provide file access. If video viewing is unavailable, inspect extracted frames when supported and state their limits, or report that the clip was saved but not reviewed. Do not claim to have watched an inaccessible clip or upload project footage without authorization.

- Report reproduction steps, clip path, observed result, and interruptions or unverified portions. Combine visual evidence with Unity state readback and console checks.
- Use `mark_recording` when exposed (Full by default) for named before/after project actions. Click, drag and `simulate_ui_scroll` tools record automatic markers in Core; a marker identifies dispatch, not proof that the intended behavior succeeded.
- Once ready, use `extract_recording_frames` with recording_id and 1..16 timestamps; if still pending use `get_task`, then inspect images with `get_recording_frame`. Check requested_seconds, actual_seconds and delta_seconds: it selects the first decoded frame at or after the request, not an exact-time guarantee. Markers after last_frame_seconds have no captured frame. Historical frame geometry is not valid for live input.
- Frame extraction can be cancelled; cleanup deletes only that job's generated PNGs, never the video. Preserve needed evidence before cleanup. Native decoder support depends on Unity version; interrupted/failed extraction and partial clips are not complete verification. Sparse frames cannot establish motion or timing between samples.
- Capture is best-effort with real elapsed timestamps, not guaranteed target-fps sampling. Use it for visual behavior, not frame-accurate performance measurement; use Profiler and device tests for performance.
- If the tool, platform, or rendering prerequisites are unavailable, report the limitation and use screenshots or state checks only for what they can establish. Do not loop on terminal failures or install recording dependencies merely to bypass the limitation.


## Metadata

- Original skill id: `unity-mcp-workflow`
- Skill version: `1.0.7`
- Platform: `claude`
- Source repository: `https://github.com/FunplayAI/funplay-unity-mcp`
