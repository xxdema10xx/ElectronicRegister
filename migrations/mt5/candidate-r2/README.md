# MT-5B candidate R2 — preflight correction, review only

R3 stopped on the first read-only SELECT in frozen R1: `principal_contract=1`.
No EXPAND, BACKFILL or other mutating SQL was run. Both servers retain MT4C.

The actual certified predecessor has the unique index
`AK_institution_membership_institution_id_id` on `(institution_id,id)`.
R1 incorrectly requires the name `uq_membership_institution_id`.

Only `000_preflight.sql` changes in this package: its ownership-principal check
requires a UNIQUE index whose complete ordered column list is exactly
`institution_id,id`, without assuming its name. Grouping remains per index;
NON_UNIQUE=0 and the exact GROUP_CONCAT predicate remain mandatory. No FK,
ownership requirement, environment guard or predecessor object is relaxed.

The other five SQL files are byte-identical to R1, including their provenance
headers and internal gate variable names. They have not passed runtime execution.
`manifest-r2.json` records the parent manifest and all parent/current/historical
hashes. R1 and the historical manifest remain unchanged.

Run only the local-file verifier for review:

```powershell
python migrations/mt5/candidate-r2/verify_static.py
```

No R2 SQL has been sent to either server. Installation needs a new explicit
authorization, full source review and a fresh guarded one-shot driver/session.
Do not replay the stopped R3 driver, rename the real index, alter the baseline,
or copy this package into the public runner directory. PREPARE/DO gates and
trigger bodies still require runtime verification on the isolated instance.

All other R1 contracts and limitations remain applicable; see the R3 report.
