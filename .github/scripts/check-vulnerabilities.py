#!/usr/bin/env python3
"""Fail CI on High/Critical NuGet advisories; report Moderate/Low.

Reads the JSON from `dotnet list package --vulnerable --include-transitive --format json`
(path given as argv[1]). Known/accepted Moderate advisories are suppressed at restore time
via Directory.Build.props; this gate blocks on High/Critical, except for the small set of
explicitly accepted advisories below (which still print, but do not fail the build).
"""
import json
import sys

# Advisories that are acknowledged and accepted: they still print, but don't fail CI.
# Keep this list tiny and justified; remove an entry the moment a real fix is available.
ACCEPTED_ADVISORIES = {
    # Microsoft.OpenApi 2.0.0 (HIGH) — transitive via Microsoft.AspNetCore.OpenApi 10.0.9.
    # No compatible fix: Microsoft.OpenApi 3.x breaks the framework's OpenAPI source
    # generator (IOpenApiMediaType.Example became read-only), and 2.0.0 is the only 2.x the
    # framework works with. Affects OpenAPI document generation, not the request/auth path.
    # Revisit when a patched .NET 10 Microsoft.AspNetCore.OpenApi ships.
    "https://github.com/advisories/GHSA-v5pm-xwqc-g5wc",
}


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
        tag = "  (accepted)" if url in ACCEPTED_ADVISORIES else ""
        print(f"  [{severity}] {pid} -> {url}{tag}")

    blocking = [
        f for f in found
        if (f[1] or "").lower() in ("high", "critical") and f[2] not in ACCEPTED_ADVISORIES
    ]
    if blocking:
        print(f"::error::{len(blocking)} unaccepted High/Critical vulnerable package(s) detected")
        return 1

    print("No blocking High/Critical advisories — passing (accepted/moderate/low reported above).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "vuln.json"))
