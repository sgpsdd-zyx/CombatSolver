# Upstream

- Source: [KKKKhazix/khazix-skills, neat-freak](https://github.com/KKKKhazix/khazix-skills/tree/4f2db09802736ac8130ddf8dd6121435b5a41b55/neat-freak).
- Revision: `4f2db09802736ac8130ddf8dd6121435b5a41b55`.
- Skill metadata version: `3.0.0`.
- Installed on: `2026-09-17`, using the Codex skill-installer helper.
- License: upstream repository [MIT license](LICENSE), retained verbatim.

This project installs skills in `.agents/skills/`, as exposed by its Codex
environment. The generic path examples in `references/agent-paths.md` do not
override that location or the project's `AGENTS.md`.

Included: `SKILL.md`, four references, the inventory script, and the license.
Upstream skill-development evaluation fixtures are not part of this project's
skill installation. No CombatSolver runtime code depends on this skill.

Local adaptation: the inventory script also skips `.local`, `.godot`, `bin`,
and `obj`, so routine inventories avoid game reference sources and build
artifacts. Skill instructions and references otherwise retain upstream text.
