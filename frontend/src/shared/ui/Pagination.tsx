import styles from './Pagination.module.css'

export interface PaginationProps {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
}

/**
 * Generic offset-pagination control (AD-16's `Page<T>` shape): Prev / Next
 * buttons around a "Page {page} of {totalPages}" label. No domain knowledge —
 * presentational only, driven entirely by props.
 */
export function Pagination({ page, pageSize, total, onPageChange }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  const isFirstPage = page <= 1
  const isLastPage = page >= totalPages

  return (
    <nav aria-label="Search results pages" className={styles.nav}>
      <button
        type="button"
        className={styles.button}
        disabled={isFirstPage}
        onClick={() => onPageChange(page - 1)}
      >
        Prev
      </button>
      <span className={styles.label}>
        Page {page} of {totalPages}
      </span>
      <button
        type="button"
        className={styles.button}
        disabled={isLastPage}
        onClick={() => onPageChange(page + 1)}
      >
        Next
      </button>
    </nav>
  )
}
