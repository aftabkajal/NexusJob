import { useState } from 'react'

import { Link, Navigate } from 'react-router'

import { useMyJobPostings, useSession } from '../../entities'
import { Pagination } from '../../shared/ui'

import styles from './MyPostingsPage.module.css'

/** Mirrors `MyApplicationsPage`'s server default — kept explicit here so both sides agree. */
const PAGE_SIZE = 20

/**
 * `/my-postings` — the Company-only surface listing the signed-in Company's
 * own postings, rendered into the shell's `<Outlet/>` (already inside the
 * 1120px `Container`).
 *
 * Guard mirrors `PostAJobPage` exactly: while `session` is pending or has
 * errored the page renders nothing (no flash on a cold load — `AppShell` and
 * this page mount together on a hard refresh / bookmark / typed URL); once
 * resolved, anyone but a Company is sent to `/` without the list ever
 * mounting its query. The endpoint's own `401` / `403` is the real
 * enforcement.
 */
export function MyPostingsPage() {
  const session = useSession()

  if (session.isPending || session.isError) {
    return null
  }

  if (session.data?.kind !== 'company') {
    return <Navigate to="/" replace />
  }

  return <MyPostingsList />
}

/**
 * List rendering mirrors `MyApplicationsList`'s structure: skeleton rows
 * while pending, an alert banner with retry on error, the empty-state copy
 * when `total === 0`, and one whole-row `<Link>` per item plus `Pagination`
 * on success. No URL-driven page state (in-memory `page`, mirrors
 * `MyApplicationsList`).
 */
function MyPostingsList() {
  const [page, setPage] = useState(1)
  const query = useMyJobPostings(page, PAGE_SIZE)

  return (
    <div className={styles.page}>
      <section className={styles.results} aria-label="My postings" aria-live="polite">
        <h1 className={styles.srOnly}>My Postings</h1>

        {query.isPending && (
          <div>
            {/* The visually-hidden loading label announces through the
             * section's own `aria-live="polite"` — nesting a second
             * `role="status"` region inside it risks duplicate or
             * inconsistent announcements across screen readers. */}
            <span className={styles.srOnly}>Loading your postings.</span>
            <div className={styles.skeletonRow} aria-hidden="true">
              <span className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
              <span className={`${styles.skeletonLine} ${styles.skeletonCreatedAt}`} />
            </div>
            <div className={styles.skeletonRow} aria-hidden="true">
              <span className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
              <span className={`${styles.skeletonLine} ${styles.skeletonCreatedAt}`} />
            </div>
            <div className={styles.skeletonRow} aria-hidden="true">
              <span className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
              <span className={`${styles.skeletonLine} ${styles.skeletonCreatedAt}`} />
            </div>
          </div>
        )}

        {query.isError && (
          <div className={styles['error-banner']} role="alert">
            <p>We couldn't load your postings. Please try again.</p>
            <button
              type="button"
              className={styles['retry-button']}
              disabled={query.isFetching}
              onClick={() => query.refetch()}
            >
              {query.isFetching ? 'Retrying…' : 'Retry'}
            </button>
          </div>
        )}

        {query.isSuccess && query.data.total === 0 && (
          <div className={styles.catalog}>
            <p className={styles['empty-state']}>You haven't posted a job yet.</p>
            <Link to="/post-a-job" className={styles.searchLink}>
              Post a Job
            </Link>
          </div>
        )}

        {query.isSuccess && query.data.total > 0 && (
          <>
            {query.data.items.map((item) => (
              <Link to={`/my-postings/${item.id}/applicants`} key={item.id} className={styles.row}>
                <h2 className={styles.title}>{item.title}</h2>
                <p className={styles.createdAt}>
                  Posted {new Date(item.createdAt).toLocaleDateString()}
                </p>
              </Link>
            ))}
            <Pagination
              page={page}
              pageSize={PAGE_SIZE}
              total={query.data.total}
              onPageChange={setPage}
              label="My postings pages"
            />
          </>
        )}
      </section>
    </div>
  )
}
