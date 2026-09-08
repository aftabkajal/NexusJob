import { useEffect, useRef } from 'react'

import { Link, Navigate, useParams } from 'react-router'

import { useJobPosting } from '../../entities'
import { toApiError } from '../../shared/lib'

import styles from './PostingDetailPage.module.css'

/** Prescribed copy — used verbatim, do not reword. */
const NOT_AVAILABLE_MESSAGE = 'This posting is no longer available.'
const LOAD_FAILED_MESSAGE = "We couldn't load this posting. Please try again."

/**
 * `/job-postings/:id` — the public job posting detail surface, rendered into the
 * shell's `<Outlet/>` (already inside the 1120px `Container`, so this page adds
 * no `Container`). Open to everyone: no session check, no redirect beyond the
 * defensive missing-param guard.
 *
 * While `useJobPosting` is pending a `role="status"` skeleton stands in for the
 * title / company / description block. A `404` resolves to "This posting is no
 * longer available."; any other failure to "We couldn't load this posting.";
 * both carry a "Back to search" link to `/`. On success the posting renders as a
 * card with the title as the page `<h1>`, the company display name, and the
 * description with its line breaks preserved.
 *
 * When the query settles, focus moves to the landing element — the `<h1>` on
 * success, the message `<p>` on the error branch (each `tabIndex={-1}`) — so a
 * keyboard user who arrived via the publish redirect or a direct link is not
 * stranded on `<body>` (mirrors 2.1b's on-success focus move, now removed).
 */
export function PostingDetailPage() {
  const { id } = useParams<{ id: string }>()

  if (!id) {
    return <Navigate to="/" replace />
  }

  return <PostingDetail id={id} />
}

function PostingDetail({ id }: { id: string }) {
  const query = useJobPosting(id)

  const headingRef = useRef<HTMLHeadingElement>(null)
  const messageRef = useRef<HTMLParagraphElement>(null)

  // Claim focus once the query settles: the removed 2.1b confirmation panel used
  // to take focus on publish success, and without this a keyboard user landing
  // here (redirect or direct link) would be left on `<body>`.
  useEffect(() => {
    if (query.status === 'success') {
      headingRef.current?.focus()
    } else if (query.status === 'error') {
      messageRef.current?.focus()
    }
  }, [query.status])

  if (query.isPending) {
    return (
      <div className={styles.card} role="status">
        <span className={styles.srOnly}>Loading the job posting.</span>
        <span className={`${styles.skeletonLine} ${styles.skeletonTitle}`} aria-hidden="true" />
        <span className={`${styles.skeletonLine} ${styles.skeletonCompany}`} aria-hidden="true" />
        <span
          className={`${styles.skeletonLine} ${styles.skeletonDescription}`}
          aria-hidden="true"
        />
      </div>
    )
  }

  if (query.isError) {
    const message =
      toApiError(query.error)?.status === 404 ? NOT_AVAILABLE_MESSAGE : LOAD_FAILED_MESSAGE
    return (
      <div className={styles.card}>
        <p className={styles.message} tabIndex={-1} ref={messageRef}>
          {message}
        </p>
        <Link to="/" className={styles.backLink}>
          Back to search
        </Link>
      </div>
    )
  }

  const posting = query.data
  return (
    <article className={styles.card}>
      <h1 className={styles.title} tabIndex={-1} ref={headingRef}>
        {posting.title}
      </h1>
      <p className={styles.company}>{posting.companyName}</p>
      <p className={styles.description}>{posting.description}</p>
    </article>
  )
}
