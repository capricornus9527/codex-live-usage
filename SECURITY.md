# Security

Codex session data is sensitive. Do not include real rollout files, account
information, access tokens, or screenshots of private conversations in issues,
pull requests, or test fixtures.

Once this repository is published, report vulnerabilities through GitHub's
private security advisory flow instead of opening a public issue with sensitive
details.

The companion is designed to:

- read only numeric token fields from local Codex rollout files;
- make no network requests;
- avoid storing conversation content;
- avoid modifying Codex files, login state, or configuration.
