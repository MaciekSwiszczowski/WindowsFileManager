Single responsibility principle is very important. Always check if changes follow this principle before delivering a changeset.

Keep `SpecFileEntryViewModel` lean. Do not add display helper properties, per-row UI state, or cached formatting to it; the file table can target very large row counts, so extra properties increase memory use across many rows.
