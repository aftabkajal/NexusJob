import styles from './JobPostingCard.module.css'

/**
 * Loading placeholder for `JobPostingCard`: same `.card` shell (no `Link`),
 * three shimmer blocks sized to the title / company / two description lines.
 * Mirrors `PostingDetailPage.module.css`'s inlined skeleton — no shared
 * `Skeleton` primitive (Story 2.3's call, per Design Notes).
 */
export function JobPostingCardSkeleton() {
  return (
    <div className={styles.card} aria-hidden="true">
      <span className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
      <span className={`${styles.skeletonLine} ${styles.skeletonCompany}`} />
      <span className={`${styles.skeletonLine} ${styles.skeletonDescriptionOne}`} />
      <span className={`${styles.skeletonLine} ${styles.skeletonDescriptionTwo}`} />
    </div>
  )
}
