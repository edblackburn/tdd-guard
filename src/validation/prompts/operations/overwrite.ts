import { COUNT_NEW_TESTS, MATCH_FAILURE_TYPE } from '../shared'

export const OVERWRITE = `## Analyzing Overwrite Operations

This section shows an existing file being replaced in full. Compare the old content with the new content to identify what is being added, removed, or modified.

### Your Task
You are reviewing a Write operation that overwrites an existing file. Determine if this violates TDD principles.

**IMPORTANT**: First identify if this is a test file or implementation file by checking the file path for \`.test.\`, \`.spec.\`, or \`test/\`.

${COUNT_NEW_TESTS}
The New File Content shows the ENTIRE file, including tests carried over unchanged from the old content. A carried-over test is NOT an addition; do not count it as a new test or describe it as "added".

### Analyzing Test File Overwrites

Adding ONE **new** test (as counted above) is allowed without a failing test output. This is the foundation of TDD.

### Analyzing Implementation File Overwrites

${MATCH_FAILURE_TYPE}
**Exceptions during refactor (tests green)**:
- Adding types, interfaces, or constants is always allowed; no runtime behavior by construction.
- Extracting helpers or functions whose behavior already lives elsewhere (not net-new logic) is allowed.

## Changes to Review
`
