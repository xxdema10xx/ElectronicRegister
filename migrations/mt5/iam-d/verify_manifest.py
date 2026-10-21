"""Offline hash verification only. Does not connect to a database or run SQL."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parent
seen = set()
for filename in ("manifest.json", "manifest-hardening.json"):
    manifest = json.loads((root / filename).read_text(encoding="utf-8"))
    for entry in manifest["Files"]:
        name = entry["File"]
        assert name == Path(name).name and name.endswith(".sql") and name not in seen
        seen.add(name)
        digest = hashlib.sha256((root / name).read_bytes()).hexdigest().upper()
        assert digest == entry["SHA256"], f"Hash mismatch: {name}"
assert seen == {"001_membership_event.sql", "002_validate.sql",
                "003_event_snapshot_shape.sql", "004_validate_snapshot.sql"}
print("MT-5B-D: four SQL SHA256 verified; no SQL executed")
