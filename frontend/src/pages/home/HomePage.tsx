import styles from './HomePage.module.css'

/**
 * The Home / Search landing surface, open to everyone. In story 1.2 the search
 * bar is visual only (no submit behaviour — real search is Epic 2) and the
 * browse list shows the empty-catalog state.
 */
export function HomePage() {
  return (
    <div className={styles.page}>
      <section className={styles.hero}>
        <h1 className={styles.headline}>Find your next role. Post your next hire.</h1>
        <p className={styles.subline}>
          NexusJob connects companies and job seekers through a single, focused hiring
          workflow.
        </p>
        <div className={styles.search} role="search">
          <input
            type="search"
            className={styles['search-input']}
            aria-label="Search open postings"
            placeholder="Search open postings by title or keyword"
          />
          <button type="button" className={styles['search-button']}>
            Search
          </button>
        </div>
        <p className={styles['hero-note']}>
          Browsing and searching do not require an account.
        </p>
      </section>
      <section className={styles.catalog} aria-label="Open postings">
        <p className={styles['empty-state']}>No open postings yet. Check back soon.</p>
      </section>
    </div>
  )
}
