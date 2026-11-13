"""CI fails closed if the NuGet audit fails, is malformed or contains advisories."""
import json
import sys

with open(sys.argv[1], encoding='utf-8-sig') as file:
    audit = json.load(file)
assert audit.get('version') == 1 and audit.get('projects'), 'Missing NuGet audit projects'
assert not audit.get('problems'), 'NuGet audit unavailable/incomplete'
count = 0
for project in audit['projects']:
    for framework in project.get('frameworks', []):
        for package in framework.get('topLevelPackages', []) + framework.get('transitivePackages', []):
            if package.get('vulnerabilities'):
                count += 1
assert count == 0, 'Dependency vulnerabilities require review/remediation: '+str(count)
print('NuGet reported no vulnerable packages in this audit; runtime/OS/database patching is separate')
