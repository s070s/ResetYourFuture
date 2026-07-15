# CLAUDE.md

## Chat responses

- Answer in the Claude Code chat using basic, non-technical terminology.
- Keep each distinct statement to a maximum of 1-2 sentences.

## Keeping docs current

- After making a change, update `README.md` and `docs/` if the change affects anything they describe.
- `README.md`: refresh it when the tech stack, project structure, setup/run steps, configuration, or the security posture changes.
- `docs/`: update the relevant file when a change resolves, adds, or alters an item there (e.g. an audit finding or a plan); remove work that is fully done.
- Skip these updates when the change is not reflected in either place.

## Committing

- Commit at each complete, working boundary (the change builds/passes tests and stands on its own) rather than bundling multiple boundaries into one commit.
