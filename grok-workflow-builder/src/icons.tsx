import type { NodeKind } from './types'

type IconProps = {
  kind?: NodeKind
  color?: string
  size?: number
}

export function NodeIcon({ kind = 'master', color = 'currentColor', size = 22 }: IconProps) {
  const common = {
    width: size,
    height: size,
    viewBox: '0 0 24 24',
    fill: 'none' as const,
    stroke: color,
    strokeWidth: 1.7,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true as const,
  }

  if (kind === 'master') {
    return (
      <svg {...common}>
        <circle cx="12" cy="8" r="3.2" />
        <path d="M5 19c1.4-3.2 3.6-4.8 7-4.8s5.6 1.6 7 4.8" />
        <path d="M12 11.2V14" />
        <path d="M9.2 16.5h5.6" />
      </svg>
    )
  }

  if (kind === 'subagent') {
    return (
      <svg {...common}>
        <circle cx="8" cy="9" r="2.4" />
        <circle cx="16" cy="9" r="2.4" />
        <path d="M3.8 18c.9-2.2 2.4-3.3 4.2-3.3s3.3 1.1 4.2 3.3" />
        <path d="M11.8 18c.9-2.2 2.4-3.3 4.2-3.3s3.3 1.1 4.2 3.3" />
      </svg>
    )
  }

  if (kind === 'dependency') {
    return (
      <svg {...common}>
        <rect x="4" y="4" width="7" height="7" rx="1.2" />
        <rect x="13" y="13" width="7" height="7" rx="1.2" />
        <path d="M11 7.5h2.2A3.3 3.3 0 0 1 16.5 10.8V13" />
      </svg>
    )
  }

  if (kind === 'approval') {
    return (
      <svg {...common}>
        <path d="M12 3.5l7 3v5.2c0 4.2-2.8 7.2-7 8.8-4.2-1.6-7-4.6-7-8.8V6.5l7-3z" />
        <path d="M9.2 12.1l1.9 1.9 3.8-3.8" />
      </svg>
    )
  }

  if (kind === 'tool') {
    return (
      <svg {...common}>
        <path d="M14.5 5.2a3.4 3.4 0 0 0-4.7 4.7L4 15.7 8.3 20l5.8-5.8a3.4 3.4 0 0 0 4.7-4.7l-2.3 2.3-2.2-2.2 2.2-2.4z" />
      </svg>
    )
  }

  return (
    <svg {...common}>
      <path d="M5 8.5h8.5a3 3 0 0 1 0 6H10" />
      <path d="M14 15.5H8.5a3 3 0 0 1 0-6H12" />
      <circle cx="7.2" cy="12.5" r="1.2" fill={color} stroke="none" />
      <circle cx="16.8" cy="11.5" r="1.2" fill={color} stroke="none" />
    </svg>
  )
}

export function ArrowIcon({ color = 'currentColor', size = 22 }: { color?: string; size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
    >
      <path d="M4 12h14" />
      <path d="M14 7l5 5-5 5" />
      <circle cx="4" cy="12" r="1.4" fill={color} stroke="none" />
    </svg>
  )
}
