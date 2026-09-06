import js from '@eslint/js'
import boundaries from 'eslint-plugin-boundaries'
import globals from 'globals'
import tseslint from 'typescript-eslint'

/**
 * Flat config (ESLint 9). The load-bearing part is the `boundaries/dependencies`
 * rule: it encodes the Feature-Sliced Design layer order
 *
 *   app -> pages -> widgets -> features -> entities -> shared
 *
 * as downward-only imports, at severity `error`, so `npm run lint` exits
 * non-zero on any upward cross-layer import (AD-2 / AD-16).
 *
 * Note: `eslint-plugin-boundaries` 7.x replaced the pre-7 `element-types` /
 * `rules` / string-selector API (the shape sketched in the spec's Design Notes)
 * with `dependencies` / `policies` / entity selectors. The encoded layer order
 * and severity are identical.
 */
const FSD_LAYERS = ['app', 'pages', 'widgets', 'features', 'entities', 'shared']

// Each layer may import from every layer strictly below it. The last layer
// (`shared`) has nothing below it, so it produces no downward policy here - its
// self-only policy is added separately below.
const downwardPolicies = FSD_LAYERS.flatMap((layer, index) => {
  const below = FSD_LAYERS.slice(index + 1)
  return below.length === 0
    ? []
    : [{ from: { element: { type: layer } }, allow: { to: { element: { types: { anyOf: below } } } } }]
})

export default tseslint.config(
  {
    // Only the NSwag-generated client file is un-linted (it carries its own
    // eslint-disable); `tsc` still type-checks it and CI's drift gate is its
    // source of truth. The hand-written `src/shared/api/index.ts` barrel stays
    // linted and inside the FSD boundary rules.
    ignores: ['dist/**', 'node_modules/**', 'src/shared/api/nexus-api-client.ts'],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2023,
      globals: { ...globals.browser },
    },
  },
  {
    // Build/tooling scripts and flat-config files run on Node.
    files: ['**/*.{js,mjs,cjs}', 'scripts/**/*'],
    languageOptions: {
      globals: { ...globals.node },
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    plugins: {
      boundaries,
    },
    settings: {
      'import/resolver': {
        typescript: { alwaysTryTypes: true, project: './tsconfig.app.json' },
      },
      'boundaries/elements': FSD_LAYERS.map((layer) => ({
        type: layer,
        pattern: `src/${layer}/**`,
      })),
    },
    rules: {
      'boundaries/dependencies': [
        'error',
        {
          default: 'disallow',
          policies: [
            ...downwardPolicies,
            // `shared` is the floor: it may depend on itself and nothing above.
            { from: { element: { type: 'shared' } }, allow: { to: { element: { type: 'shared' } } } },
          ],
        },
      ],
      // AD-16: the NSwag-generated client (`shared/api`) is a client-side API
      // description, not a general primitive. It may be resolved only from an
      // `entities/*/api` or `features/*/api` segment (cleared in the override
      // below); every other importer fails here.
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['**/shared/api', '**/shared/api/*', 'shared/api', 'shared/api/*'],
              message:
                'Import the generated API client only from an entities/*/api or features/*/api segment (AD-16).',
            },
          ],
        },
      ],
    },
  },
  {
    // The AD-16 exception: API segments own the generated client.
    files: ['src/entities/*/api/**', 'src/features/*/api/**'],
    rules: {
      'no-restricted-imports': 'off',
    },
  },
)
