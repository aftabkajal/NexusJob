import type { CSSProperties, ReactNode } from 'react'

import styles from './Container.module.css'

interface ContainerProps {
  children: ReactNode
  /** Optional extra class, appended after the base container class. */
  className?: string
  style?: CSSProperties
}

/**
 * The layout constraint: a fixed 1120px max-width, centred, with `gutter`
 * (32px) side padding. Every surface renders its content inside one of these.
 * Below 1120px the width holds and the page scrolls — there are no breakpoints.
 */
export function Container({ children, className, style }: ContainerProps) {
  const classNames = className ? `${styles.container} ${className}` : styles.container
  return (
    <div className={classNames} style={style}>
      {children}
    </div>
  )
}
