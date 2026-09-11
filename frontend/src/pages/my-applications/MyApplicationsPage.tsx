import { useState } from 'react'

import { Link, Navigate } from 'react-router'

import { useMyApplications, useSession } from '../../entities'
import { Pagination } from '../../shared/ui'

import styles from './MyApplicationsPage.module.css'

/** 3.3a's server default — kept explicit here so both sides agree. */
const PAGE_SIZE = 20

/**
 * `/my-applications` — the Job-Seeker-only surface listing the postings the
 * signed-in caller has applied to, rendered into the shell's `<Outlet/>`
 * (already inside the 1120px `Container`).
 *
 * Guard mirrors `PostAJobPage` exactly: while `session` is pending or has
 * errored the page renders nothing (no flash on a cold load — `AppShell` and
 * this page mount together on a hard refresh / bookmark / typed URL); once
 * resolved, anyone but a Job Seeker is sent to `/` without the list ever
 * mounting its query. The endpoint's own `401` / `403` is the real
 * enforcement.
 */
export function MyApplicationsPage() {
  const session = useSession()

  if (session.isPending || session.isError) {
    return null
  }

  if (session.data?.kind !== 'jobSeeker') {
    return <Navigate to="/" replace />
  }

  return <MyApplicationsList />
}

/**
 * List rendering mirrors `HomePage`'s results section structure: skeleton
 * rows while pending, an alert banner with retry on error, the empty-state
 * copy when `total === 0`, and one row per item plus `Pagination` on success.
 * No URL-driven page state (in-memory `page`, mirrors `HomePage`).
 */
function MyApplicationsList() {
  const [page, setPage] = useState(1)
  const query = useMyApplications(page, PAGE_SIZE)

  return (
    <div className={styles.page}>
      <section className={styles.results} aria-label="My applications" aria-live="polite">
        <h1 className={styles.srOnly}>My Applications</h1>

        {query.isPending && (
          <div>
            {/* The visually-hidden loading label announces through the
             * section's own `aria-live="polite"` — nesting a second
             * `role="status"` region inside it risks duplicate or
             * inconsistent announcements across screen readers. */}
            <span className={styles.srOnly}>Loading your applications.</span>
            <div className={styles.skeletonRow} aria-hidden="true">
              <span className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
              <span className={`${styles.skeletonLine} ${styles.skeletonAppliedAt}`} />
            </div>
            <div className={styles.skeletonRow} aria-hidden="true">
              <span className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
              <span className={`${styles.skeletonLine} ${styles.skeletonAppliedAt}`} />
            </div>
            <div className={styles.skeletonRow} aria-hidden="true">
              <span className={`${styles.skeletonLine} ${styles.skeletonTitle}`} />
              <span className={`${styles.skeletonLine} ${styles.skeletonAppliedAt}`} />
            </div>
          </div>
        )}

        {query.isError && (
          <div className={styles['error-banner']} role="alert">
            <p>We couldn't load your applications. Please try again.</p>
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
            <p className={styles['empty-state']}>You haven't applied to anything yet.</p>
            <Link to="/" className={styles.searchLink}>
              Search
            </Link>
          </div>
        )}

        {query.isSuccess && query.data.total > 0 && (
          <>
            {query.data.items.map((item) => (
              <Link
                to={`/job-postings/${item.jobPostingId}`}
                key={item.applicationId}
                className={styles.row}
              >
                <h2 className={styles.title}>{item.jobPostingTitle}</h2>
                <p className={styles.appliedAt}>
                  Applied {new Date(item.submittedAt).toLocaleDateString()}
                </p>
              </Link>
            ))}
            <Pagination
              page={page}
              pageSize={PAGE_SIZE}
              total={query.data.total}
              onPageChange={setPage}
              label="My applications pages"
            />
          </>
        )}
      </section>
    </div>
  )
}
