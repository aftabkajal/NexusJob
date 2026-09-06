// Negative test for the design-token Stylelint gate (Story 1.2), mirroring
// `check-fsd-gate.mjs`.
//
// Writes a fixture `.css` file under `src/` (outside the exempt
// `src/shared/tokens/` tree) containing a raw hex colour and a raw `px`
// `font-size`, lints it with Stylelint via its Node API and the project config,
// then fails unless Stylelint reported BOTH a `color-no-hex` and a
// `declaration-property-value-disallowed-list` violation. Guards against the
// token rules being disabled or the ignore glob being widened to cover `src/`.

import { mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { dirname } from 'node:path'
import stylelint from 'stylelint'

const fixture = 'src/__token_gate_probe__.css'
mkdirSync(dirname(fixture), { recursive: true })
writeFileSync(fixture, '.token-gate-probe {\n  color: #abc123;\n  font-size: 15px;\n}\n')

let result
try {
  result = await stylelint.lint({ files: fixture })
} finally {
  rmSync(fixture, { force: true })
}

const warnings = result.results.flatMap((entry) => entry.warnings)
const flagged = (rule) => warnings.some((warning) => warning.rule === rule)
const hasHex = flagged('color-no-hex')
const hasDisallowedPx = flagged('declaration-property-value-disallowed-list')

if (!hasHex || !hasDisallowedPx) {
  console.error(
    'Design-token gate FAILED: Stylelint did not reject the fixture ' +
      `(color-no-hex=${hasHex}, declaration-property-value-disallowed-list=${hasDisallowedPx}).`,
  )
  console.error(JSON.stringify(warnings, null, 2))
  process.exit(1)
}

console.log(
  `Design-token gate OK: Stylelint rejected the fixture (${warnings.length} warning(s), ` +
    'including color-no-hex and declaration-property-value-disallowed-list).',
)
