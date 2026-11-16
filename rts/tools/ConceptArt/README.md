<!-- rts/tools/ConceptArt/README.md: what generate.py does, its usage line and how the API credential is supplied. -->
# Concept art generator

`generate.py` reads a concept art round's `shots.json` (for example `rts/docs/art/concept/round1/shots.json`) and calls the OpenAI image API once per shot. Each shot's prompt is the round's style text followed by the shot's own text. It tries the models in `MODELS` in order (default `gpt-image-2.6-sunburst`, then `gpt-image-2.5-sunburst`, then `gpt-image-2`) and uses the first one the API accepts. It then writes the PNG original to a scratch folder and a JPEG with its longest side at most 1536 pixels into the round folder. Render quality is medium, unless a shot has its own `quality` field (low, medium or high), which overrides it for that shot; the request timeout is 600 seconds. A shot with a non-empty `references` list (repository-relative image paths) is sent to the image edit endpoint with those images, so the result stays faithful to them.

Usage:

```
python3 rts/tools/ConceptArt/generate.py <round folder> [shot id ...] [--scratch DIR]
```

- `<round folder>`: folder that holds `shots.json`; the JPEGs are written here.
- `shot id`: optional list of shot IDs to generate; with none, every shot is generated.
- `--scratch DIR`: folder for the PNG originals; defaults to a new temporary folder.

The script sends a placeholder bearer token (`Bearer dummy`). The agent proxy supplies the real API credential, so no key is stored in the repository or needed on the command line.

The script is used by the `concept-artist` subagent. See `rts/docs/art/CONCEPT_BRIEF.md` for the art direction and the per-image checklist.
