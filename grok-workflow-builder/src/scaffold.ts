import JSZip from 'jszip'
import { toMarkdown } from './markdown'
import { slugify, toFolderId, type WorkflowDoc, type WorkflowNode } from './types'

function masterInstructions(doc: WorkflowDoc, master: WorkflowNode | undefined): string {
  const subs = doc.nodes.filter((n) => n.kind === 'subagent')
  const approvals = doc.nodes.filter(
    (n) => n.kind === 'approval' || n.requiresApproval,
  )
  const deps = doc.nodes
    .filter((n) => n.dependencies.trim())
    .map((n) => `- ${n.title}: ${n.dependencies.trim()}`)

  return [
    `# ${doc.name || 'Grok Bot'}`,
    '',
    doc.summary.trim() || 'Orchestrate the workflow defined in the builder.',
    '',
    '## Role',
    master?.description.trim() ||
      'You are the master agent. Classify work, delegate to sub-agents, and respect approval gates.',
    '',
    '## Sub-agents',
    ...(subs.length
      ? subs.map((s) => `- **${s.title}**: ${s.description || 'Specialist helper.'}`)
      : ['- (none defined)']),
    '',
    '## Manual approvals',
    ...(approvals.length
      ? approvals.map(
          (a) =>
            `- **${a.title}**: pause for human confirmation before continuing.`,
        )
      : ['- None required.']),
    '',
    '## Dependencies',
    ...(deps.length ? deps : ['- None listed.']),
    '',
    '## Rules',
    '- Prefer tools and sub-agents over guessing.',
    '- Never bypass a manual approval gate.',
    '- Keep replies concise and actionable.',
    '',
  ].join('\n')
}

function agentTs(doc: WorkflowDoc): string {
  return `import { defineAgent } from "@grok/agent-sdk";

// Scaffolded from Grok Bot Workflow Builder: ${doc.name || 'Untitled'}
export default defineAgent({
  model: {
    id: "grok-4.7",
    params: [
      { id: "reasoning_effort", value: "high" },
      { id: "fast", value: "true" },
    ],
  },
});
`
}

function subagentFiles(node: WorkflowNode): Record<string, string> {
  const id = toFolderId(node.title, node.id)
  return {
    [`bot/subagents/${id}/agent.ts`]: `import { defineAgent } from "@grok/agent-sdk";

export default defineAgent({
  description: ${JSON.stringify(node.description || node.title)},
  model: {
    id: "grok-4.7",
    params: [
      { id: "reasoning_effort", value: "high" },
      { id: "fast", value: "true" },
    ],
  },
});
`,
    [`bot/subagents/${id}/instructions.md`]: `# ${node.title}

${node.description || 'Specialist sub-agent.'}

${node.dependencies.trim() ? `## Dependencies\n\n${node.dependencies.trim()}\n` : ''}${
      node.requiresApproval
        ? '\n## Manual approval\n\nRequire human confirmation before side effects.\n'
        : ''
    }`,
  }
}

function toolFile(node: WorkflowNode): Record<string, string> {
  const name = toFolderId(node.title, node.id)
  const needsApproval = node.requiresApproval
  return {
    [`bot/tools/${name}.ts`]: `import { defineTool } from "@grok/agent-sdk";

// ${node.description || node.title}
export default defineTool({
  name: ${JSON.stringify(name)},
  description: ${JSON.stringify(node.description || `Run ${node.title}`)},
  execution: "server",
  parameters: {
    type: "object",
    properties: {
      input: { type: "string", description: "Primary tool input" },
      ${needsApproval ? 'approved: { type: "boolean", description: "Set true only after manual approval" },' : ''}
    },
    required: ["input"${needsApproval ? ', "approved"' : ''}],
  },
  async execute(args) {
    ${
      needsApproval
        ? `if (!args.approved) {
      return { ok: false, error: "Manual approval required before running ${name}." };
    }`
        : ''
    }
    // TODO: implement ${name}
    return {
      ok: true,
      echo: args.input,
      note: "Scaffolded placeholder from Grok Bot Workflow Builder",
    };
  },
});
`,
  }
}

function channelFile(node: WorkflowNode): Record<string, string> {
  const name = toFolderId(node.title, 'channel')
  return {
    [`bot/channels/${name}.ts`]: `import { defineChannel } from "@grok/agent-sdk";

// ${node.description || node.title}
// Replace with slackChannel / githubChannel / custom HTTP as needed.
export default defineChannel({
  name: ${JSON.stringify(name)},
});
`,
  }
}

function dependencyNote(node: WorkflowNode): Record<string, string> {
  const name = toFolderId(node.title, node.id)
  return {
    [`bot/mcp-connections/${name}.md`]: `# Dependency: ${node.title}

${node.description || ''}

Declared package / service: \`${node.dependencies.trim() || 'TBD'}\`

Wire this as \`bot/mcp-connections/${name}.ts\` with \`defineConnection\` once the endpoint is known.
`,
  }
}

function approvalNote(node: WorkflowNode): Record<string, string> {
  const name = toFolderId(node.title, node.id)
  return {
    [`bot/approvals/${name}.md`]: `# Manual Approval: ${node.title}

${node.description || 'Human gate before continuing the workflow.'}

## Checklist
- [ ] Review model output
- [ ] Confirm side effects are intended
- [ ] Record who approved and when

Gate any tool that runs after this node until approval is present.
`,
  }
}

export function buildScaffoldFiles(doc: WorkflowDoc): Record<string, string> {
  const files: Record<string, string> = {}
  const master = doc.nodes.find((n) => n.kind === 'master')

  files['WORKFLOW.md'] = toMarkdown(doc)
  files['README.md'] = `# ${doc.name || 'Grok Bot'}

Scaffold generated by **Grok Bot Workflow Builder**.

## Quick start

\`\`\`bash
npm install
npx --yes "@grok/agent-sdk@0.6.0" validate
npx --yes "@grok/agent-sdk@0.6.0" serve --dev
\`\`\`

## Layout

- \`bot/agent.ts\` — master agent
- \`bot/instructions.md\` — always-on prompt
- \`bot/subagents/\` — delegated specialists
- \`bot/tools/\` — tools
- \`bot/channels/\` — intake surfaces
- \`bot/mcp-connections/\` — dependency placeholders
- \`bot/approvals/\` — manual approval checklists
- \`WORKFLOW.md\` — source workflow export

See [Agent SDK docs](https://cursor.com/docs/bdk).
`

  files['package.json'] = JSON.stringify(
    {
      name: slugify(doc.name || 'grok-bot'),
      private: true,
      type: 'module',
      scripts: {
        validate: 'npx --yes "@grok/agent-sdk@0.6.0" validate',
        serve: 'npx --yes "@grok/agent-sdk@0.6.0" serve --dev',
      },
      dependencies: {
        '@grok/agent-sdk': '^0.6.0',
      },
    },
    null,
    2,
  )

  files['bot/agent.ts'] = agentTs(doc)
  files['bot/instructions.md'] = masterInstructions(doc, master)

  for (const node of doc.nodes) {
    if (node.kind === 'subagent') Object.assign(files, subagentFiles(node))
    if (node.kind === 'tool') Object.assign(files, toolFile(node))
    if (node.kind === 'channel') Object.assign(files, channelFile(node))
    if (node.kind === 'dependency') Object.assign(files, dependencyNote(node))
    if (node.kind === 'approval') Object.assign(files, approvalNote(node))
  }

  return files
}

export async function downloadScaffoldZip(doc: WorkflowDoc): Promise<void> {
  const zip = new JSZip()
  const root = slugify(doc.name || 'grok-bot')
  const files = buildScaffoldFiles(doc)
  for (const [path, contents] of Object.entries(files)) {
    zip.file(`${root}/${path}`, contents)
  }
  const blob = await zip.generateAsync({ type: 'blob' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${root}-agent-sdk-scaffold.zip`
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
