// FSD `shared` layer: cross-cutting primitives with no domain knowledge.
// One import surface for the layer — the design-token system and the UI kit.

export const APP_NAME = 'NexusJob'

// The generated API client is imported from the `shared/api` sub-path (AD-16),
// not re-exported through this top-level barrel.
export * from './lib'
export * from './tokens'
export * from './ui'
