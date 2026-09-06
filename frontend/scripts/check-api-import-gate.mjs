// Negative test for the AD-16 generated-client import restriction, mirroring
// `check-fsd-gate.mjs` / `check-tokens-gate.mjs`.
//
// Writes a fixture under `src/widgets/` (outside any `*/api` segment) that
// imports `shared/api`, lints it with the project's own eslint.config.js via
// the ESLint API, and fails unless `no-restricted-imports` reports an error.
// Guards against that rule being removed or its override being widened.

import { mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { dirname } from 'node:path'
import { ESLint } from 'eslint'

const fixture = 'src/widgets/__api_import_gate_probe__.ts'
mkdirSync(dirname(fixture), { recursive: true })
writeFileSync(fixture, "import '../shared/api'\nexport const probe = 1\n")

let results
try {
  const eslint = new ESLint()
  results = await eslint.lintFiles([fixture])
} finally {
  rmSync(fixture, { force: true })
}

const messages = results.flatMap((result) => result.messages)
const errorCount = results.reduce((total, result) => total + result.errorCount, 0)
const restrictedImportError = messages.some(
  (message) => message.ruleId === 'no-restricted-imports' && message.severity === 2,
)

if (errorCount === 0 || !restrictedImportError) {
  console.error(
    'API import gate FAILED: eslint did not reject a `shared/api` import from outside an ' +
      '*/api segment via the no-restricted-imports rule.',
  )
  console.error(JSON.stringify(messages, null, 2))
  process.exit(1)
}

console.log(
  `API import gate OK: no-restricted-imports rejected the shared/api import (${errorCount} error(s)).`,
)
