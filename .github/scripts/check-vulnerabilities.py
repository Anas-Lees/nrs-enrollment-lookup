#!/usr/bin/env python3
"""Fail CI on High/Critical NuGet advisories; report Moderate/Low.

Reads the JSON from `dotnet list package --vulnerable --include-transitive --format json`
(path given as argv[1]). Known/accepted Moderate advisories are suppressed at restore time
via Directory.Build.props; this gate only blocks on High/Critical.
"""
import json
import sys


def main(path: str) -> int:
    with open(path, encoding="utf-8") as fh:
        data = json.load(fh)

    found = set()
    for project in data.get("projects", []):
        for framework in project.get("frameworks", []):
            for group in (framework.get("topLevelPackages", []), framework.get("transitivePackages", [])):
                for pkg in group:
                    for vuln in pkg.get("vulnerabilities", []):
                        found.add((pkg.get("id"), vuln.get("severity"), vuln.get("advisoryurl")))

    if not found:
        print("No vulnerable packages reported.")
        return 0

    print("Advisories reported by NuGet audit:")
    for pid, severity, url in sorted(found):
        print(f"  [{severity}] {pid} -> {url}")

    blocking = [f for f in found if (f[1] or "").lower() in ("high", "critical")]
    if blocking:
        print(f"::error::{len(blocking)} High/Critical vulnerable package(s) detected")
        return 1

    print("No High/Critical advisories — passing (moderate/low reported above).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "vuln.json"))
