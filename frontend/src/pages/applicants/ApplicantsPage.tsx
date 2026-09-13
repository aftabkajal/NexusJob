import { useState } from 'react'

import { Link, Navigate, useParams } from 'react-router'

import { useApplicants, useSession } from '../../entities'
import { toApiError } from '../../shared/lib'
import { Pagination } from '../../shared/ui'

import styles from './ApplicantsPage.module.css'

/** Mirrors `MyApplicationsPage`'s server default — kept explicit here so both sides agree. */
const PAGE_SIZE = 20

/** Prescribed copy — used verbatim, do not reword. Byte-identical to
 * `PostingDetailPage`'s 404 copy: 3-4a's `/api/applications` returns the same
 * `404` for a missing posting id and for one owned by another Company, and
 * this page never distinguishes the two (Design Notes: no confirming a
 * not-owned posting exists). */
const NOT_AVAILABLE_MESSAGE = 'This posting is no longer available.'
const LOAD_FAILED_MESSAGE = "We couldn't load the applicants. Please try again."

/**
 * `/my-postings/:id/applicants` — the Company-only surface listing one of the
 * signed-in Company's own postings' applicants, rendered into the shell's
 * `<Outlet/>` (already inside the 1120px `Container`).
 *
 * Missing `:id` (an empty route segment) redirects to `/my-postings` without
 * ever mounting the session check or the query — mirrors `PostingDetailPage`'s
 * missing-param guard.
 *
 * `key={id}` forces a full remount of the subtree below (including its local
 * pagination state) if this route element is ever reached directly from one
 * `:id` to another without an intervening unmount — React Router re-renders
 * the same component instance across a param-only URL change by default, so
 * without this the previous posting's page number would carry over (review
 * finding).
 */
export function ApplicantsPage() {
  const { id } = useParams<{ id: string }>()

  if (!id) {
    return <Navigate to="/my-postings" replace />
  }

  return <ApplicantsList key={id} id={id} />
}

/**
 * Company-only session gate, checked before the applicants query ever mounts
 * — identical shape to `PostAJobPage`: render nothing while `session` is
 * pending/erroring (no flash on a cold load), `Navigate to="/"` for any
 * resolved non-Company viewer. The endpoint's own `401` / `403` remains the
 * real enforcement.
 */
function ApplicantsList({ id }: { id: string }) {
  const session = useSession()

  if (session.isPending || session.isError) {
    return null
  }

  if (session.data?.kind !== 'company') {
    return <Navigate to="/" replace />
  }

  return <ApplicantsForPosting id={id} />
}

/**
 * The query + rendering half of `ApplicantsList`, split into its own
 * component only so `useState` / `useApplicants` mount after (not
 * conditionally alongside) the session gate above — same reason
 * `MyApplicationsPage` splits its gate from `MyApplicationsList`.
 *
 * The "Back to My Postings" link is persistent above every branch below it —
 * the page never fetches the posting's own detail (Design Notes: the public
 * `GET /api/job-postings/{id}` has no ownership check, so calling it here
 * would leak a not-owned posting's title/existence alongside the correct
 * `404` from the applicants endpoint) — so there is no visible page title
 * beyond the screen-reader-only `<h1>`.
 */
function ApplicantsForPosting({ id }: { id: string }) {
  const [page, setPage] = useState(1)
  const query = useApplicants(id, page, PAGE_SIZE)
  const errorStatus = query.isError ? toApiError(query.error)?.status : undefined

  return (
    <div className={styles.page}>
      <h1 className={styles.srOnly}>Applicants</h1>
      <Link to="/my-postings" className={styles.backLink}>
        Back to My Postings
      </Link>

      <section className={styles.results} aria-label="Applicants" aria-live="polite">
        {query.isPending && (
          <div>
            <span className={styles.srOnly}>Loading applicants.</span>
            <div className={styles.list}>
              <div className={styles.row} aria-hidden="true">
                <div className={styles.info}>
                  <span className={`${styles.skeletonLine} ${styles.skeletonName}`} />
                  <span className={`${styles.skeletonLine} ${styles.skeletonEmail}`} />
                </div>
                <span className={`${styles.skeletonLine} ${styles.skeletonTimestamp}`} />
              </div>
              <div className={styles.row} aria-hidden="true">
                <div className={styles.info}>
                  <span className={`${styles.skeletonLine} ${styles.skeletonName}`} />
                  <span className={`${styles.skeletonLine} ${styles.skeletonEmail}`} />
                </div>
                <span className={`${styles.skeletonLine} ${styles.skeletonTimestamp}`} />
              </div>
              <div className={styles.row} aria-hidden="true">
                <div className={styles.info}>
                  <span className={`${styles.skeletonLine} ${styles.skeletonName}`} />
                  <span className={`${styles.skeletonLine} ${styles.skeletonEmail}`} />
                </div>
                <span className={`${styles.skeletonLine} ${styles.skeletonTimestamp}`} />
              </div>
            </div>
          </div>
        )}

        {query.isError && errorStatus === 404 && (
          <p className={styles.message}>{NOT_AVAILABLE_MESSAGE}</p>
        )}

        {query.isError && errorStatus !== 404 && (
          <div className={styles['error-banner']} role="alert">
            <p>{LOAD_FAILED_MESSAGE}</p>
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
          <p className={styles['empty-state']}>No applicants yet.</p>
        )}

        {query.isSuccess && query.data.total > 0 && (
          <>
            <div className={styles.list}>
              {query.data.items.map((item) => (
                <div className={styles.row} key={item.jobSeekerId}>
                  <div className={styles.info}>
                    <p className={styles.name}>{item.fullName}</p>
                    <p className={styles.email}>{item.email}</p>
                  </div>
                  <p className={styles.timestamp}>
                    {new Date(item.submittedAt).toLocaleDateString()}
                  </p>
                </div>
              ))}
            </div>
            <Pagination
              page={page}
              pageSize={PAGE_SIZE}
              total={query.data.total}
              onPageChange={setPage}
              label="Applicants pages"
            />
          </>
        )}
      </section>
    </div>
  )
}
