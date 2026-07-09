---
name: skill-smoke-test
description: Verify that the agent skills system can discover, load, and read a skill resource. Use only when asked to run a skill smoke test, skills verification test, or skill discovery test.
metadata:
  author: agentic-league
  version: "1.0"
---

# Skill Smoke Test

Use this skill only to verify the Agent Skills progressive-disclosure workflow.

1. Call `read_skill_resource` for `verification-token.md` from this skill.
2. Reply with exactly this format, replacing the placeholder with the token content:

```text
SKILL_SMOKE_TEST_PASSED: <token>
```

3. Do not call roster, league, web search, bootstrap, image-generation, or any other tools.

