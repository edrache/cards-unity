# Claude Code project instructions

Read [AGENTS.md](AGENTS.md) once and follow its shared workflow, current project map, model selection, CLI/MCP decision table and verification rules. Read only relevant sections of `docs/gameplay-reference.md`; do not import that full reference into startup context.

## Model selection in this host

Apply the task tiers from AGENTS.md using models actually exposed by the current Claude runtime. When supported, select its small/fast model for bounded searches and mechanical edits, its balanced coding model for focused implementation, and its strongest reasoning model for difficult diagnosis, IK or rendering work. Resolve identifiers from the live tool/configuration; do not pass Codex model IDs to Claude tools or assume cross-provider access.

Delegate only when it saves total work, pass minimal context, and explicitly select a child model when supported. Otherwise continue locally. Do not claim to switch the active parent model. Keep one owner for editor mutations and let the parent integrate and commit.

## Entry checklist

1. Inspect `git status --short`, then relevant source and serialized settings.
2. Choose CLI for file work; use Unity MCP when live editor state or reference-preserving edits are needed. Documentation-only tasks do not need Unity.
3. Verify the affected behavior, review the full working tree and commit all outstanding non-ignored changes as authorized in AGENTS.md; respect requests to leave changes uncommitted.
4. Report changes, verification/limitations and commit ID in Polish. Do not push unless requested.

Keep shared project rules in AGENTS.md rather than duplicating them here.
