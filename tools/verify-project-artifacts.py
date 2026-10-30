"""Canonical release closure; historical bootstrap evidence is not re-certified."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
subprocess.run(['node', str(ROOT/'tools/release-artifacts.cjs')], cwd=ROOT, check=True)