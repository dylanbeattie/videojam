---
name: gather-requirements
description: >
  Full requirements elicitation skill. Use when the user wants to discover, 
  document, or refine project requirements. Produces REQUIREMENTS.md for an 
  architect. Invoked automatically when user mentions "requirements", 
  "spec", "specification", "what should we build", or asks to plan a project.
---

# Requirements Elicitation Skill

This skill guides a structured requirements discovery interview and produces a professional `REQUIREMENTS.md` document.

## When to Invoke

Auto-invoke when the user:
- Asks to document, gather, or discuss project requirements
- Mentions writing a spec or specification
- Asks "what should we build" or "help me plan my project"
- Asks to produce a document for an architect or technical lead

## Interview Protocol

Conduct a structured interview across 6 phases. **One question per message.**

### Phase 1 — Project Overview
What is the project name, the problem it solves, who it's for, the business driver, and whether it's greenfield or a rebuild?

### Phase 2 — Users & Personas
Who are the primary and secondary users? What roles exist? What are their technical proficiency levels? Accessibility requirements?

### Phase 3 — Functional Requirements
What features must exist at launch (MVP)? What is explicitly out of scope? What are the critical user journeys? What third-party integrations are needed? What data does the system manage?

### Phase 4 — Non-Functional Requirements
Performance targets? Uptime/availability? Scalability projections? Security requirements? Compliance obligations (GDPR, HIPAA, SOC2, etc.)? Deployment constraints?

### Phase 5 — Constraints & Assumptions
Preferred or required tech stack? Timeline and budget constraints? Existing systems to integrate with or replace?

### Phase 6 — Open Questions & Risks
What is the riskiest unknown? What dependencies exist? What must the architect investigate before design begins?

## Interview Rules

1. One focused question per message
2. Acknowledge each answer before moving on
3. Probe vague answers for specifics and metrics
4. Summarise every 5–7 questions; ask for corrections
5. Signal completion and ask user to confirm before generating the document

## Output

When the user confirms the interview is complete, use the Write tool to create `REQUIREMENTS.md` in the current working directory.

See `template.md` for the exact document structure to follow.

After writing:
- Confirm the file location
- Summarise the 3–5 most important captured requirements
- Highlight sections with gaps or open questions
- Suggest the user share the document with their architect

## Supporting Files

- `template.md` — The exact REQUIREMENTS.md template to fill in
- `examples/saas-platform.md` — Example output for a SaaS project
