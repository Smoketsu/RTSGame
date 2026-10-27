<!-- Funplay Unity MCP managed project skills -->
<!-- Funplay Unity MCP skill version: unity-mcp-workflow@1.0.7 -->

# Execution Trust And Faithful Verification

Read this for source-backed restoration, risky serialized-reference edits, real input validation, stalled Play Mode or continued work across reloads. This does not authorize changing another project's state, widening exposure, installing dependencies or synthesizing application-ready flags.

## Identity and recovery

Independently match initialize.funplay.context.project_path/project_id to the user's intended project before any mutation. A friendly endpoint name, an open socket or an old screenshot is not proof of identity. Prefer generated project-scoped URLs; legacy URLs require expected_context for writes. Keep project/session/reload identity on evidence and reject stale instance/capture receipts. Context is a misrouting guard, not authentication.

Use the current tools/list and exposed get_tool_capabilities. Distinguish implemented, enabled, exposed and runtime-available backends. Recover prepare_editor/get_task using the original task ID or request_key; honor bounded waits and backoff. Never replay an interrupted arbitrary edit just because the response was lost. If a recovery tool is unavailable, use the exposed exact-state reader or report the blocker, not a guessed hidden route.

## Acceptance layers

Report compilation/import, resource provenance, configured components, API/event dispatch, actual input edges, actual rendered output and end-to-end application behavior separately. Editor ready means import/compile/mode readiness only. Require multiple actual PlayerLoop frame/clock samples; advancing wall time or encoded video frames does not prove gameplay Update advanced. Inspect pause, timeScale, focus and runInBackground without changing them through a status read.

Select strict input_route when fidelity matters. UI EventSystem dispatch, direct Button.onClick and physics messages are distinct test paths, not legacy UnityEngine.Input injection. Unsupported legacy polling must be reported. New Input System synthetic input must show Down/Held/Up on distinct real PlayerLoop frames; even those edges do not prove the application's handler accepted the action. Never patch gameplay input or fabricate focus/SDK-ready/outcome state to make a test pass.

## Owned verification and resources

Use the actual authored scene and real camera. When exposed, verification_scope manages explicitly enrolled jobs and an explicitly requested temporary background flag, with a bounded deadline and compare-before-restore cleanup. End explicitly; inspect cleanup_pending and native job status. A read-only get_task never cleans, cancels, restores or clears dirty state. A scope does not roll back business data, network effects, arbitrary callbacks or user scene edits. Stop actual producers before taking final state; do not clear flags to simulate disposal.

For unresolved serialized references, distinguish missing scripts from unresolved GUID/fileID slots. Before a selective asset write, capture the exact file hash and persistent IDs. Prefer patch_serialized_reference for one ObjectReference change when available: disposable-copy preflight, untouched PPtr comparison and post-import readback must pass before accepting a live save. Ambiguous/unsupported serialization must fail closed. Exact-byte recovery cannot undo arbitrary serialization callback side effects; inspect rollback_verified, backup_path and cleanup_pending. Never text-patch Unity YAML to repair a live graph.

For visual defects, inspect_render_bindings traces the real runtime instance/prefab override through shared material slots, particle trails, shaders and textures plus camera/canvas/mask context. Do not instantiate Renderer.material during inspection. Shader.isSupported and a fixture overlay are not rendered acceptance. Work in coherent code/resource/render batches, preserve original correct assets, and compare the actual scene output after each batch.

## Evidence and continuation

Review finalized videos with their real presentation timestamps, geometry and source hash. If Unity's decoder is unavailable, use already-installed ffmpeg only with explicit external-decoder opt-in; do not install software or change Play state just to decode. Extracted historical frames cannot be used as current click captures.

When exposed, save_verification_evidence/read_verification_evidence reuse static inventories only after file/meta/import-dependency/configuration hashes match. Checkpoints record caller-authored next steps, blockers and unverified layers, not independently verified acceptance. Always refresh runtime/frame/input/application observations. A changed input or incomplete scan invalidates the relevant claim; do not rescan unrelated unchanged resources merely to restate old evidence.

A confirmed blocking native Unity modal that MCP cannot handle may use computer use for that modal only. Explain the blocker first; preserve save/discard/overwrite/install/security authorization boundaries. Then return to MCP, recheck identity/state and read back the interrupted operation. A temporary disconnection alone is not a reason to switch methods.

## Source boundaries

Original Funplay guidance derived from local restoration evidence and public Unity API behavior. Official plugin compatibility provenance remains pinned at cf6b2da24e424b0a60d560a57f39f676cb6f79f3; no third-party skill prose or scripts are copied.
