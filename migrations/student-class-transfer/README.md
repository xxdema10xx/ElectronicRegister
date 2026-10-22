# Student class transfer — 20261022.4

MySQL 5.7 additive migration: one student revision column, one append-only ledger,
nine tenant-aware RESTRICT FK and four triggers. Existing records receive revision
zero; no historic transfer events are invented. Run the additive tool only once
on a newly provisioned isolated pre-upgrade instance, after verified backup. Never
replay after the consolidated bootstrap. Partial DDL must be diagnosed manually.
The validator is read-only. Retention and administrative rectification require a
separate approved policy; no legal retention duration is inferred here.
