# Diagrams

Exported architecture and design diagrams. Keep an editable source (`.drawio`) next to each
exported image so they can be regenerated.

| File | Shows | Status |
| ---- | ----- | ------ |
| `architecture.drawio` | System architecture & deployment topology (editable source) | present |
| `architecture.png` | Exported architecture diagram | _planned — not yet exported_ |
| `erd.png` | Entity-relationship diagram (PERSON · ID_CARD · PASSPORT) | _planned_ |
| `sequence-search.png` | Sequence of an operator search request | _planned_ |
| `ci-cd.png` | CI/CD pipeline and environment promotion | _planned_ |

> Only the editable `architecture.drawio` source is committed today; the README and the live
> mermaid diagrams cover architecture and the search sequence in the meantime. The PNG exports
> land with the tasks that need them (e.g. the ERD with the data model).
>
> Note: `architecture.drawio` predates later changes — it still shows unversioned routes and
> Swagger, and omits the Redis cache and audit trail; refresh it when exporting.
