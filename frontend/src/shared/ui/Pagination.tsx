import styles from './Pagination.module.css'

export interface PaginationProps {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
  /** Accessible name for the pages landmark. Defaults to the Home search
   * results' label so existing callers are unaffected; a consumer paginating
   * a different list (e.g. My Applications) should pass its own. */
  label?: string
}

/**
 * Generic offset-pagination control (AD-16's `Page<T>` shape): Prev / Next
 * buttons around a "Page {page} of {totalPages}" label. No domain knowledge —
 * presentational only, driven entirely by props.
 */
export function Pagination({
  page,
  pageSize,
  total,
  onPageChange,
  label = 'Search results pages',
}: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  const isFirstPage = page <= 1
  const isLastPage = page >= totalPages

  return (
    <nav aria-label={label} className={styles.nav}>
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
