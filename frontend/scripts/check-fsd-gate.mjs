// Negative test for the Feature-Sliced Design boundary gate (AD-2 / AD-16).
//
// Writes a fixture file with a known UPWARD cross-layer import
// (`entities` importing from `features`), lints it with the project's own
// eslint.config.js via the ESLint API, and fails unless the
// boundaries/dependencies rule reports an error. Guards against that rule being
// silently disabled or misconfigured (e.g. imports no longer resolving to an
// element type).

import { mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { dirname } from 'node:path'
import { ESLint } from 'eslint'

const fixture = 'src/entities/__fsd_gate_probe__.ts'
mkdirSync(dirname(fixture), { recursive: true })
writeFileSync(fixture, "import '../features'\nexport const probe = 1\n")

let results
try {
  const eslint = new ESLint()
  results = await eslint.lintFiles([fixture])
} finally {
  rmSync(fixture, { force: true })
}

const messages = results.flatMap((result) => result.messages)
const errorCount = results.reduce((total, result) => total + result.errorCount, 0)
const boundariesError = messages.some(
  (message) => message.ruleId === 'boundaries/dependencies' && message.severity === 2,
)

if (errorCount === 0 || !boundariesError) {
  console.error(
    'FSD boundary gate FAILED: eslint did not reject an upward "entities -> features" import ' +
      'via the boundaries/dependencies rule.',
  )
  console.error(JSON.stringify(messages, null, 2))
  process.exit(1)
}

console.log(
  `FSD boundary gate OK: boundaries/dependencies rejected the upward import (${errorCount} error(s)).`,
)
