# Validation evidence

The versionable JSON summaries record real runs and baseline/final comparisons. Raw TRX,
logs and the Git source archive remain locally available here and are ignored to avoid
versioning large repeated environment-failure traces. `test-comparison.json` contains
per-test outcome changes and added cases; an empty changes array does not mean the
integration suite passed. See the main report for the unavailable database prerequisites.
