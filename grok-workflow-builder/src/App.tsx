import { useEffect, useMemo, useRef, useState } from 'react'
import { ArrowIcon, NodeIcon } from './icons'
import { downloadTextFile, toMarkdown } from './markdown'
import { SAMPLE_WORKFLOW } from './sample'
import { downloadScaffoldZip } from './scaffold'
import {
  kindLabel,
  NODE_H,
  NODE_W,
  PALETTE,
  slugify,
  uid,
  type NodeKind,
  type WorkflowDoc,
  type WorkflowNode,
} from './types'

type BoardMode = 'move' | 'connect'

const KIND_COLOR: Record<NodeKind, string> = {
  master: '#0F6E56',
  subagent: '#1F4E79',
  dependency: '#8A5A00',
  approval: '#9B2C2C',
  tool: '#3F3D56',
  channel: '#0E7490',
}

function loadDoc(): WorkflowDoc {
  try {
    const raw = localStorage.getItem('grok-workflow-doc-v1')
    if (raw) return JSON.parse(raw) as WorkflowDoc
  } catch {
    /* ignore */
  }
  return structuredClone(SAMPLE_WORKFLOW)
}

export default function App() {
  const [doc, setDoc] = useState<WorkflowDoc>(loadDoc)
  const [selectedId, setSelectedId] = useState<string | null>('n_master')
  const [mode, setMode] = useState<BoardMode>('move')
  const [connectFrom, setConnectFrom] = useState<string | null>(null)
  const [status, setStatus] = useState(
    'Drag components onto the board, connect them, then export Markdown or an Agent SDK scaffold.',
  )
  const [previewMd, setPreviewMd] = useState(false)
  const [paletteDrag, setPaletteDrag] = useState<{
    kind: NodeKind
    x: number
    y: number
  } | null>(null)
  const boardRef = useRef<HTMLDivElement | null>(null)
  const dragRef = useRef<{ id: string; ox: number; oy: number } | null>(null)
  const paletteDragRef = useRef<{
    kind: NodeKind
    startX: number
    startY: number
    moved: boolean
  } | null>(null)

  useEffect(() => {
    localStorage.setItem('grok-workflow-doc-v1', JSON.stringify(doc))
  }, [doc])

  useEffect(() => {
    function onMove(e: PointerEvent) {
      const drag = paletteDragRef.current
      if (!drag) return
      if (
        Math.abs(e.clientX - drag.startX) > 4 ||
        Math.abs(e.clientY - drag.startY) > 4
      ) {
        drag.moved = true
      }
      setPaletteDrag({ kind: drag.kind, x: e.clientX, y: e.clientY })
    }

    function onUp(e: PointerEvent) {
      const drag = paletteDragRef.current
      paletteDragRef.current = null
      setPaletteDrag(null)
      if (!drag) return

      const board = boardRef.current
      if (!board) return
      const rect = board.getBoundingClientRect()
      const overBoard =
        e.clientX >= rect.left &&
        e.clientX <= rect.right &&
        e.clientY >= rect.top &&
        e.clientY <= rect.bottom

      if (drag.moved && overBoard) {
        addNodeAt(
          drag.kind,
          e.clientX - rect.left + board.scrollLeft,
          e.clientY - rect.top + board.scrollTop,
        )
        return
      }

      if (!drag.moved) {
        addNodeAt(
          drag.kind,
          board.scrollLeft + 220 + doc.nodes.length * 12,
          board.scrollTop + 120 + doc.nodes.length * 12,
        )
      }
    }

    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
    return () => {
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', onUp)
    }
  }, [doc.nodes.length])

  const selected =
    doc.nodes.find((n) => n.id === selectedId) ?? doc.nodes[0] ?? null

  const bounds = useMemo(() => {
    if (doc.nodes.length === 0) return { width: 720, height: 480 }
    return {
      width: Math.max(720, ...doc.nodes.map((n) => n.x + NODE_W + 48)),
      height: Math.max(480, ...doc.nodes.map((n) => n.y + NODE_H + 48)),
    }
  }, [doc.nodes])

  const markdown = useMemo(() => toMarkdown(doc), [doc])

  function updateNode(id: string, patch: Partial<WorkflowNode>) {
    setDoc((prev) => ({
      ...prev,
      nodes: prev.nodes.map((n) => (n.id === id ? { ...n, ...patch } : n)),
    }))
  }

  function addNodeAt(kind: NodeKind, x: number, y: number) {
    const id = uid('n')
    const node: WorkflowNode = {
      id,
      kind,
      title: kindLabel(kind),
      description: '',
      dependencies: '',
      requiresApproval: kind === 'approval',
      x: Math.max(8, x - NODE_W / 2),
      y: Math.max(8, y - NODE_H / 2),
    }
    setDoc((prev) => ({ ...prev, nodes: [...prev.nodes, node] }))
    setSelectedId(id)
    setStatus(`Added ${kindLabel(kind)}. Edit it in the inspector.`)
  }

  function removeSelected() {
    if (!selected) return
    const id = selected.id
    const remaining = doc.nodes.filter((n) => n.id !== id)
    setDoc((prev) => ({
      ...prev,
      nodes: prev.nodes.filter((n) => n.id !== id),
      edges: prev.edges.filter((e) => e.from !== id && e.to !== id),
    }))
    setSelectedId(remaining[0]?.id ?? null)
    setConnectFrom(null)
    setStatus('Removed node and its connections.')
  }

  function allowBoardDrag(e: React.DragEvent) {
    e.preventDefault()
    e.stopPropagation()
    e.dataTransfer.dropEffect = 'copy'
  }

  function onDrop(e: React.DragEvent) {
    e.preventDefault()
    e.stopPropagation()
    const kind = (e.dataTransfer.getData('application/x-grok-kind') ||
      e.dataTransfer.getData('text/plain')) as NodeKind
    if (!kind || !PALETTE.some((p) => p.kind === kind)) return
    const rect = boardRef.current?.getBoundingClientRect()
    if (!rect) return
    addNodeAt(
      kind,
      e.clientX - rect.left + (boardRef.current?.scrollLeft ?? 0),
      e.clientY - rect.top + (boardRef.current?.scrollTop ?? 0),
    )
  }

  function onNodePointerDown(id: string, e: React.PointerEvent) {
    if (mode === 'connect') {
      e.stopPropagation()
      if (!connectFrom) {
        setConnectFrom(id)
        setStatus('Now click the target node to draw an arrow.')
        return
      }
      if (connectFrom === id) {
        setConnectFrom(null)
        setStatus('Connection cancelled.')
        return
      }
      const exists = doc.edges.some((edge) => edge.from === connectFrom && edge.to === id)
      if (!exists) {
        setDoc((prev) => ({
          ...prev,
          edges: [...prev.edges, { id: uid('e'), from: connectFrom, to: id }],
        }))
        setStatus('Connected nodes with an arrow.')
      } else {
        setStatus('That connection already exists.')
      }
      setConnectFrom(null)
      return
    }

    const node = doc.nodes.find((n) => n.id === id)
    if (!node) return
    dragRef.current = { id, ox: e.clientX - node.x, oy: e.clientY - node.y }
    ;(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId)
  }

  function onBoardPointerMove(e: React.PointerEvent) {
    const drag = dragRef.current
    if (!drag) return
    setDoc((prev) => ({
      ...prev,
      nodes: prev.nodes.map((n) =>
        n.id === drag.id
          ? {
              ...n,
              x: Math.max(0, e.clientX - drag.ox),
              y: Math.max(0, e.clientY - drag.oy),
            }
          : n,
      ),
    }))
  }

  function autoLayout() {
    const ranks = new Map<string, number>()
    const incoming = new Map<string, string[]>()
    for (const n of doc.nodes) incoming.set(n.id, [])
    for (const e of doc.edges) incoming.get(e.to)?.push(e.from)

    const roots = doc.nodes.filter((n) => (incoming.get(n.id) ?? []).length === 0)
    const queue = roots.map((n) => n.id)
    for (const id of queue) ranks.set(id, 0)
    while (queue.length) {
      const id = queue.shift()!
      const rank = ranks.get(id) ?? 0
      for (const e of doc.edges.filter((edge) => edge.from === id)) {
        const next = Math.max(ranks.get(e.to) ?? 0, rank + 1)
        if (!ranks.has(e.to) || next > (ranks.get(e.to) ?? 0)) {
          ranks.set(e.to, next)
          queue.push(e.to)
        }
      }
    }
    for (const n of doc.nodes) if (!ranks.has(n.id)) ranks.set(n.id, 0)

    const byRank = new Map<number, string[]>()
    for (const [id, rank] of ranks) {
      const list = byRank.get(rank) ?? []
      list.push(id)
      byRank.set(rank, list)
    }

    setDoc((prev) => ({
      ...prev,
      nodes: prev.nodes.map((n) => {
        const rank = ranks.get(n.id) ?? 0
        const siblings = byRank.get(rank) ?? [n.id]
        const order = siblings.indexOf(n.id)
        return {
          ...n,
          x: 40 + order * (NODE_W + 36),
          y: 28 + rank * (NODE_H + 48),
        }
      }),
    }))
    setStatus('Applied automatic layered layout.')
  }

  const edgePaths = doc.edges.map((edge) => {
    const from = doc.nodes.find((n) => n.id === edge.from)
    const to = doc.nodes.find((n) => n.id === edge.to)
    if (!from || !to) return null
    const x1 = from.x + NODE_W / 2
    const y1 = from.y + NODE_H
    const x2 = to.x + NODE_W / 2
    const y2 = to.y
    const mid = (y1 + y2) / 2
    return {
      id: edge.id,
      d: `M ${x1} ${y1} C ${x1} ${mid}, ${x2} ${mid}, ${x2} ${y2}`,
    }
  })

  return (
    <div className={`app-shell ${paletteDrag ? 'is-placing' : ''}`}>
      {paletteDrag ? (
        <div
          className="place-ghost"
          style={{
            left: paletteDrag.x + 12,
            top: paletteDrag.y + 12,
            borderColor: KIND_COLOR[paletteDrag.kind],
          }}
        >
          <NodeIcon
            kind={paletteDrag.kind}
            color={KIND_COLOR[paletteDrag.kind]}
            size={16}
          />
          <span>{kindLabel(paletteDrag.kind)}</span>
        </div>
      ) : null}
      <header className="hero">
        <div className="hero-copy">
          <p className="eyebrow">Grok · Agent SDK</p>
          <h1>Bot Workflow Builder</h1>
          <p className="lede">
            Compose master agents, sub-agents, tools, channels, dependencies, and
            approval gates — then download Markdown or a ready-to-fill Agent SDK scaffold.
          </p>
        </div>
        <div className="hero-actions">
          <button
            className="btn primary"
            onClick={() => {
              downloadTextFile(
                `${slugify(doc.name)}.md`,
                toMarkdown(doc),
                'text/markdown;charset=utf-8',
              )
              setStatus(`Downloaded ${slugify(doc.name)}.md`)
            }}
          >
            Download Markdown
          </button>
          <button
            className="btn secondary"
            onClick={async () => {
              await downloadScaffoldZip(doc)
              setStatus('Downloaded Agent SDK scaffold zip.')
            }}
          >
            Download Agent SDK Zip
          </button>
        </div>
      </header>

      <section className="workflow-meta">
        <label>
          <span>Workflow name</span>
          <input
            value={doc.name}
            onChange={(e) => setDoc((p) => ({ ...p, name: e.target.value }))}
            placeholder="Name this bot workflow"
          />
        </label>
        <label className="grow">
          <span>Summary</span>
          <input
            value={doc.summary}
            onChange={(e) => setDoc((p) => ({ ...p, summary: e.target.value }))}
            placeholder="What does this workflow do?"
          />
        </label>
        <p className="status" role="status">
          {status}
        </p>
      </section>

      <div className="workspace">
        <aside className="palette">
          <h2>Components</h2>
          <p className="hint">Drag onto the canvas. Each kind has its own icon.</p>
          <div className="palette-list">
            {PALETTE.map((item) => (
              <div
                key={item.kind}
                className="palette-item"
                title="Drag onto the canvas, or click to place"
                onPointerDown={(e) => {
                  if (e.button !== 0) return
                  e.preventDefault()
                  paletteDragRef.current = {
                    kind: item.kind,
                    startX: e.clientX,
                    startY: e.clientY,
                    moved: false,
                  }
                  setPaletteDrag({
                    kind: item.kind,
                    x: e.clientX,
                    y: e.clientY,
                  })
                }}
              >
                <span
                  className="icon-tile"
                  style={{ color: KIND_COLOR[item.kind] }}
                >
                  <NodeIcon kind={item.kind} color={KIND_COLOR[item.kind]} />
                </span>
                <span>
                  <strong>{item.label}</strong>
                  <small>{item.hint}</small>
                </span>
              </div>
            ))}
            <div className="palette-item static">
              <span className="icon-tile">
                <ArrowIcon />
              </span>
              <span>
                <strong>Connection Arrow</strong>
                <small>Use Connect mode, then click source → target</small>
              </span>
            </div>
          </div>

          <div className="mode-row">
            <button
              className={`btn ${mode === 'move' ? 'primary' : 'ghost'}`}
              onClick={() => {
                setMode('move')
                setConnectFrom(null)
                setStatus('Move mode: drag nodes on the board.')
              }}
            >
              Move
            </button>
            <button
              className={`btn ${mode === 'connect' ? 'primary' : 'ghost'}`}
              onClick={() => {
                setMode('connect')
                setConnectFrom(null)
                setStatus('Connect mode: click source, then target.')
              }}
            >
              Connect
            </button>
          </div>
          <div className="mode-row">
            <button className="btn ghost" onClick={autoLayout}>
              Auto-layout
            </button>
            <button
              className="btn ghost"
              onClick={() => {
                setDoc(structuredClone(SAMPLE_WORKFLOW))
                setSelectedId('n_master')
                setConnectFrom(null)
                setMode('move')
                setStatus('Restored the sample Support Triage workflow.')
              }}
            >
              Reset sample
            </button>
          </div>
        </aside>

        <main className="board-panel">
          <div className="panel-head">
            <h2>Canvas</h2>
            <span>
              {doc.nodes.length} nodes · {doc.edges.length} arrows
            </span>
          </div>
          <div
            ref={boardRef}
            className="board"
            onDragEnter={allowBoardDrag}
            onDragOver={allowBoardDrag}
            onDrop={onDrop}
            onPointerMove={onBoardPointerMove}
            onPointerUp={() => {
              dragRef.current = null
            }}
            onClick={() => setConnectFrom(null)}
          >
            <svg
              className="edges"
              width={bounds.width}
              height={bounds.height}
            >
              <defs>
                <marker
                  id="arrowhead"
                  markerWidth="8"
                  markerHeight="8"
                  refX="6"
                  refY="3"
                  orient="auto"
                >
                  <path d="M0,0 L6,3 L0,6 Z" fill="#5B6B73" />
                </marker>
              </defs>
              {edgePaths.map((item) =>
                item ? (
                  <path
                    key={item.id}
                    d={item.d}
                    fill="none"
                    stroke="#5B6B73"
                    strokeWidth={1.8}
                    markerEnd="url(#arrowhead)"
                  />
                ) : null,
              )}
            </svg>
            <div
              className="nodes-layer"
              style={{ width: bounds.width, height: bounds.height }}
              onDragEnter={allowBoardDrag}
              onDragOver={allowBoardDrag}
              onDrop={onDrop}
            >
              {doc.nodes.map((node) => {
                const active =
                  selected?.id === node.id || connectFrom === node.id
                return (
                  <div
                    key={node.id}
                    className={`board-node ${active ? 'active' : ''}`}
                    style={{
                      left: node.x,
                      top: node.y,
                      borderColor: active
                        ? KIND_COLOR[node.kind]
                        : undefined,
                    }}
                    onClick={(e) => {
                      e.stopPropagation()
                      setSelectedId(node.id)
                      if (mode !== 'connect') {
                        setStatus(`Selected ${kindLabel(node.kind)}.`)
                      }
                    }}
                    onPointerDown={(e) => onNodePointerDown(node.id, e)}
                  >
                    <div className="board-node-top">
                      <NodeIcon
                        kind={node.kind}
                        color={KIND_COLOR[node.kind]}
                        size={18}
                      />
                      <span>{kindLabel(node.kind)}</span>
                      {(node.requiresApproval || node.kind === 'approval') && (
                        <em className="gate">Gate</em>
                      )}
                    </div>
                    <strong>{node.title}</strong>
                  </div>
                )
              })}
            </div>
          </div>
        </main>

        <aside className="inspector">
          <h2>Inspector</h2>
          {selected ? (
            <div className="inspector-card">
              <div className="inspector-title">
                <NodeIcon
                  kind={selected.kind}
                  color={KIND_COLOR[selected.kind]}
                  size={18}
                />
                <span>{kindLabel(selected.kind)}</span>
              </div>
              <label>
                <span>Title</span>
                <input
                  value={selected.title}
                  onChange={(e) =>
                    updateNode(selected.id, { title: e.target.value })
                  }
                />
              </label>
              <label>
                <span>Description</span>
                <textarea
                  rows={3}
                  value={selected.description}
                  onChange={(e) =>
                    updateNode(selected.id, { description: e.target.value })
                  }
                />
              </label>
              <label>
                <span>Dependencies placeholder</span>
                <input
                  value={selected.dependencies}
                  placeholder="npm package, MCP, API…"
                  onChange={(e) =>
                    updateNode(selected.id, { dependencies: e.target.value })
                  }
                />
              </label>
              <label className="toggle-row">
                <span>Manual approval</span>
                <input
                  type="checkbox"
                  checked={selected.requiresApproval}
                  onChange={(e) =>
                    updateNode(selected.id, {
                      requiresApproval: e.target.checked,
                    })
                  }
                />
              </label>
              <button className="btn ghost danger" onClick={removeSelected}>
                Remove node
              </button>
            </div>
          ) : (
            <p className="hint">Drag a component onto the canvas to begin.</p>
          )}

          <div className="legend">
            <h3>Icon legend</h3>
            {PALETTE.map((item) => (
              <div key={item.kind} className="legend-row">
                <NodeIcon kind={item.kind} color={KIND_COLOR[item.kind]} size={16} />
                <span>{item.label}</span>
              </div>
            ))}
            <div className="legend-row">
              <ArrowIcon size={16} />
              <span>Workflow connection</span>
            </div>
          </div>

          <button
            className="btn ghost"
            onClick={() => setPreviewMd((v) => !v)}
          >
            {previewMd ? 'Hide Markdown preview' : 'Preview Markdown'}
          </button>
        </aside>
      </div>

      {previewMd && (
        <section className="preview">
          <h2>Markdown preview</h2>
          <pre>{markdown}</pre>
        </section>
      )}
    </div>
  )
}
