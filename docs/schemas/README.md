# Schema Artifacts

OpenPlanTrace keeps the current public JSON schema artifacts in this folder.

During alpha development, generated historical `openplantrace.scan.v*.schema.json`
snapshots are not kept forever in the repository because each scan contract is
large and quickly dominates the project line count. Use Git history or tagged
releases for older scan schemas. Keep the current and immediately preceding scan
schemas, compact scan schema, and active non-scan contracts here.

Current structural contracts are `openplantrace.scan.v72` and
`openplantrace.structure.v2`. Structure v2 carries the mixed straight/circular
path topology while placement remains conservatively line-only.
