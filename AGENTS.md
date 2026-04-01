# AGENTS.md

## Purpose

This repository uses coding agents and automation.  
Any agent working on this repository must follow these rules.

## Required rules for all agents

1. Never mark a Pull Request as complete if user-facing behavior changed and `README.md` was not updated.
2. If you add, remove, rename, or change a command, you must update:
   - the command list in `README.md`
   - usage examples in `README.md`
   - any related behavior description in `README.md`
3. If you change installation, release, manifest, shell, import/export, config parsing, or profile behavior, update the relevant README sections.
4. If you add tests for a new feature, verify that the feature is also documented for users when applicable.
5. Do not downgrade workflow versions or reintroduce outdated workflow files from older branches.
6. Keep Pull Requests in Draft until:
   - CI passes
   - documentation is updated when required
7. For release intent, use labels:
   - `release:patch`
   - `release:minor`
   - `release:major`
   - `skip-release`

## Pull Request completion checklist for agents

Before marking a PR ready for review, confirm:

- [ ] Code changes are complete
- [ ] CI passes
- [ ] `README.md` was updated if user-facing behavior changed
- [ ] Workflow files were kept aligned with current `main`
- [ ] Correct release label strategy is expected

## Documentation policy

When in doubt, update `README.md`.

User-facing changes include, but are not limited to:
- new commands
- renamed commands
- changed command syntax
- changed examples
- changed shell behavior
- changed import/export behavior
- changed config parsing behavior
- changed profile management behavior
- changed installation or release flow
