# Grok Bot Workflow Builder

Visual authoring UI for composing Grok / Agent SDK bot workflows.

## Features

- Drag-and-drop components: **Master Agent**, **Sub-Agent**, **Tool**, **Channel**, **Dependency**, **Manual Approval**
- Distinct SVG icons per component type
- Connect mode for workflow arrows
- Inspector fields for descriptions, dependency placeholders, and approval gates
- **Download Markdown** — Mermaid graph + connection table + scaffold hints
- **Download Agent SDK Zip** — `bot/` tree stubs (`agent.ts`, instructions, subagents, tools, channels, approvals)

## Run

```bash
npm install
npm run dev
```

Open the printed local URL (default `http://localhost:5173`).

## Build

```bash
npm run build
npm run preview
```

## Workflow → Agent SDK mapping

| Node | Scaffold target |
| --- | --- |
| Master Agent | `bot/agent.ts`, `bot/instructions.md` |
| Sub-Agent | `bot/subagents/<id>/` |
| Tool | `bot/tools/<name>.ts` |
| Channel | `bot/channels/<name>.ts` |
| Dependency | `bot/mcp-connections/<name>.md` placeholder |
| Manual Approval | `bot/approvals/<name>.md` checklist |

Generated scaffolds are starting points — review and wire real MCP/channel implementations before production use.
