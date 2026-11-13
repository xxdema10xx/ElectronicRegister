# MT-5B candidate R3 — strict full-column owner index, review only

R3B stopped during source review, before executing candidate-r2 SQL.
R2's owner-index predicate ignores information_schema.STATISTICS.SUB_PART.
It therefore accepts a UNIQUE index on prefixes of institution_id and id,
although MySQL cannot use prefix indexes to support this foreign key.
The actual MT4C baseline has full-length columns; its schema must stay intact.

This separate revision changes only 000's ownership-principal index check:

```sql
HAVING COUNT(*)=2 AND SUM(SUB_PART IS NOT NULL)=0
  AND GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX)='institution_id,id'
```

NON_UNIQUE=0 and grouping per table/index remain mandatory. Exactly two complete
columns in the correct order are required, irrespective of index name. Extra,
reversed, nonunique and prefix indexes cannot satisfy this check. The explicit
count also rejects extra columns even if GROUP_CONCAT were truncated.

The other five SQL files are byte-identical to R2 and R1. No trigger, FK,
retention, generation or teardown predicate is weakened. The five retained
headers and internal gate variable names remain provenance, not a claim that
these are the lost historical SQL. No original/historical manifest is changed.

`manifest-r3.json` records all six hashes, parent R2 hashes and parent manifest.
`python migrations/mt5/candidate-r3/verify_static.py` checks the package and exact
delta locally; it is not a MySQL parser or runtime certification.

No R3 SQL has been executed. Only the index-predicate expressions were evaluated
against derived constant metadata using SELECT, without tables or DDL. Evidence
and remaining runtime checks are documented in the R3B report.

Installation requires separate explicit approval and a fresh guarded execution
journal. Do not replay R3/R3B journals, change MT4C, remove guards, or copy these
files into the old public runner directory. No .NET/JWT work belongs to this package.
