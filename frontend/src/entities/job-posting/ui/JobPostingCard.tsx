import { Link } from 'react-router'

import styles from './JobPostingCard.module.css'

export interface JobPostingCardProps {
  id: string
  title: string
  companyName: string
  description: string
}

/**
 * `job-card` — a single whole-card link to a posting's detail view
 * (`/job-postings/{id}`). No nested interactive elements (no Apply control —
 * that's Epic 3 scope): the entire card is the one `<a>`. Resting state is
 * border-only; hover adds the DESIGN.md two-layer elevation with no
 * border/colour change.
 */
export function JobPostingCard({ id, title, companyName, description }: JobPostingCardProps) {
  return (
    <Link to={`/job-postings/${id}`} className={styles.card}>
      <h3 className={styles.title}>{title}</h3>
      <p className={styles.company}>{companyName}</p>
      <p className={styles.description}>{description}</p>
    </Link>
  )
}
