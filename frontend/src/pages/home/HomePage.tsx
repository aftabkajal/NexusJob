import { useState, type FormEvent } from 'react'

import { JobPostingCard, JobPostingCardSkeleton, useJobPostingSearch } from '../../entities'
import { Pagination } from '../../shared/ui'

import styles from './HomePage.module.css'

/** 2.3a's server default — kept explicit here so both sides agree. */
const PAGE_SIZE = 20

/**
 * `/` — the Home / Search landing surface, open to everyone. Home is both
 * landing and results: there is no separate results route and no URL-driven
 * search state (no query params, no history entries per keystroke or page
 * change).
 *
 * `useJobPostingSearch` runs unconditionally starting from an empty
 * `submittedQuery` — 2.3a's "missing/empty query = browse everything"
 * semantics — so the card stack renders immediately on load, before any
 * submit. Typing in the search input never triggers a search by itself; only
 * a form submit updates `submittedQuery` (trimmed) and resets `page` to 1.
 */
export function HomePage() {
  const [keyword, setKeyword] = useState('')
  const [submittedQuery, setSubmittedQuery] = useState('')
  const [page, setPage] = useState(1)

  const query = useJobPostingSearch(submittedQuery, page, PAGE_SIZE)

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmed = keyword.trim()
    setKeyword(trimmed)
    setSubmittedQuery(trimmed)
    setPage(1)
  }

  return (
    <div className={styles.page}>
      <section className={styles.hero}>
        <h1 className={styles.headline}>Find your next role. Post your next hire.</h1>
        <p className={styles.subline}>
          NexusJob connects companies and job seekers through a single, focused hiring
          workflow.
        </p>
        <form className={styles.search} role="search" onSubmit={handleSubmit}>
          <input
            type="search"
            className={styles['search-input']}
            aria-label="Search open postings"
            placeholder="Search open postings by title or keyword"
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
          />
          <button type="submit" className={styles['search-button']}>
            Search
          </button>
        </form>
        <p className={styles['hero-note']}>
          Browsing and searching do not require an account.
        </p>
      </section>
      <section className={styles.results} aria-label="Open postings" aria-live="polite">
        <h2 className={styles.srOnly}>Search results</h2>
        {query.isPending && (
          <>
            <JobPostingCardSkeleton />
            <JobPostingCardSkeleton />
            <JobPostingCardSkeleton />
          </>
        )}

        {query.isError && (
          <div className={styles['error-banner']} role="alert">
            <p>We couldn't load postings. Please try again.</p>
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

        {query.isSuccess && query.data.total === 0 && submittedQuery === '' && (
          <div className={styles.catalog}>
            <p className={styles['empty-state']}>No open postings yet. Check back soon.</p>
          </div>
        )}

        {query.isSuccess && query.data.total === 0 && submittedQuery !== '' && (
          <div className={styles.catalog}>
            <p className={styles['empty-state']}>
              No postings match &quot;{submittedQuery}.&quot; Try a different term.
            </p>
          </div>
        )}

        {query.isSuccess && query.data.total > 0 && (
          <>
            {query.data.items.map((item) => (
              <JobPostingCard
                key={item.id}
                id={item.id}
                title={item.title}
                companyName={item.companyName}
                description={item.description}
              />
            ))}
            <Pagination
              page={page}
              pageSize={PAGE_SIZE}
              total={query.data.total}
              onPageChange={setPage}
            />
          </>
        )}
      </section>
    </div>
  )
}
