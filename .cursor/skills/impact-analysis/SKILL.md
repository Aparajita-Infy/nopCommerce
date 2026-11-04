---
name: impact-analysis
description: Turn a feature request into a short impact analysis by selecting only the nopCommerce verification-map entries it changes. Use when asked for impact, scope, or which features a request touches. The written doc is the context pack a later agent should load.
---

# Impact analysis

Input is one feature request: the text after this skill name, or the paragraph the user pasted. If there is no request, ask for it and stop. Do not invent a request and do not start from the codebase.

The map is `.cursor/skills/verify-nopcommerce/features/README.md`. Feature files are the links under `## Features`. The output is `docs/impact/<slug>.md`, copied from [template.md](template.md). `<slug>` is three to six words from the request, lowercase, hyphens.

The doc is the whole task context for a later implementation agent. A section that does not change a file they would open or a journey they would re-run does not belong in it.

## 1. Select from the index

Read the `## Features` list in the README. Do not open a feature file in this step.

Mark each bullet:

- **Touch** — the request changes a behavior that bullet describes.
- **Neighbor** — it shares a page, form, or cookie with a touch, and the request might change that shared entry.
- **Out** — no shared entry. Leave the file unread.

Open every touch file. Open a neighbor only when the shared entry itself might change. If nothing is a touch, write the doc with an empty context pack, name the map gap under Unknowns, and stop. Do not search the repository to invent coverage.

## 2. Take only the handles

From each opened feature file, keep four things:

- sub-feature ids that the request affects
- entry points (routes, labels, selectors)
- the observable end state
- gotchas that would make the new behavior fail

Ignore the rest of the file. Do not copy launch, doctor, or cleanup instructions into the doc.

## 3. Pack the code

Search only for the routes and selectors from step 2. Read the view that renders the handle and the controller action it submits to. Stop at the end of that action and at the end of the view section that contains the handle.

Record `path` and a line range. One range must be enough to see the current behavior the request changes. Cap the pack at 8 ranges. If more ranges seem relevant, keep the 8 that decide user-visible behavior and name the others under Do not load.

Do not open plugins, localization XML, upgrade scripts, or the test project unless a handle is defined only there.

## 4. Write the doc

Fill every section of the template.

- **Request** stays in the user's words.
- **Map selection** lists touch, neighbor, and out. Out entries are one line each and do not quote the feature file.
- **Behavior change** is one row per affected sub-feature: what the user sees today, what they see after, and which map entry point proves it.
- **Context pack** is the only source the next agent loads.
- **Do not load** includes every out feature file and every code area step 3 skipped.
- **Verification** names the map journeys to re-run and the journeys whose observables stay the same.
- **Unknowns** are facts the map and the packed ranges do not settle. Do not guess them closed.

## 5. Reply

Return the doc path, the touch list, and the number of context-pack ranges. Do not paste the packed source and do not paste unread feature files.
