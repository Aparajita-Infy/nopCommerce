export type NodeKind =
  | 'master'
  | 'subagent'
  | 'dependency'
  | 'approval'
  | 'tool'
  | 'channel'

export type WorkflowNode = {
  id: string
  kind: NodeKind
  title: string
  description: string
  dependencies: string
  requiresApproval: boolean
  x: number
  y: number
}

export type WorkflowEdge = {
  id: string
  from: string
  to: string
}

export type WorkflowDoc = {
  name: string
  summary: string
  nodes: WorkflowNode[]
  edges: WorkflowEdge[]
}

export type PaletteItem = {
  kind: NodeKind
  label: string
  hint: string
}

export const NODE_W = 176
export const NODE_H = 76

export const PALETTE: PaletteItem[] = [
  {
    kind: 'master',
    label: 'Master Agent',
    hint: 'Root orchestrator — bot/agent.ts',
  },
  {
    kind: 'subagent',
    label: 'Sub-Agent',
    hint: 'Specialist — bot/subagents/<id>/',
  },
  {
    kind: 'dependency',
    label: 'Dependency',
    hint: 'Package, MCP, or external service',
  },
  {
    kind: 'approval',
    label: 'Manual Approval',
    hint: 'Human gate before the next step',
  },
  {
    kind: 'tool',
    label: 'Tool',
    hint: 'Server/agent tool — bot/tools/',
  },
  {
    kind: 'channel',
    label: 'Channel',
    hint: 'Slack, GitHub, or HTTP entry',
  },
]

export function kindLabel(kind: NodeKind): string {
  switch (kind) {
    case 'master':
      return 'Master'
    case 'subagent':
      return 'Sub-Agent'
    case 'dependency':
      return 'Dependency'
    case 'approval':
      return 'Approval'
    case 'tool':
      return 'Tool'
    case 'channel':
      return 'Channel'
  }
}

export function slugify(value: string): string {
  return (
    value
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-|-$/g, '') || 'workflow'
  )
}

export function toFolderId(title: string, fallback: string): string {
  const cleaned = title
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_|_$/g, '')
  return cleaned || fallback
}

export function uid(prefix: string): string {
  return `${prefix}_${Math.random().toString(36).slice(2, 9)}`
}
