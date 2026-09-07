# Claude Code project instructions

Read and follow [AGENTS.md](AGENTS.md) before working in this repository. It is the canonical project guide for language, architecture, scenes, controls, Unity MCP, verification, and asset handling.

## Claude-specific entry checklist

1. Read `AGENTS.md` and inspect `git status --short`.
2. Check the relevant scripts and serialized scene settings before implementing changes.
3. For editor work, discover the configured `unity-mcp` relay tools and inspect the current Unity state.
4. Verify the changed behavior and commit all outstanding repository changes, including pre-existing changes and new non-ignored files. The user explicitly authorizes committing the full working tree after completing work; do not restrict commits to your own edits. Respect an explicit request to leave changes uncommitted.
5. Report results and commit identifiers in Polish. Do not push unless requested.

Use the relay's current tool definitions rather than assuming the older Coplay MCP API. Keep code, documentation, and commit messages in English.

When the project evolves, update shared guidance in `AGENTS.md`; keep this file focused on the Claude Code entry point.
