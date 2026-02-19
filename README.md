# Claude Code Requirements Analyst — Setup Guide

A complete Claude Code configuration that turns Claude into a structured **Requirements Analyst** that interviews users about their projects and produces a professional `REQUIREMENTS.md` document for an architect.

---

## What's Included

```
requirements-analyst/
├── README.md                          ← You are here
├── system-prompt/
│   └── REQUIREMENTS-ANALYST-SYSTEM-PROMPT.md   ← The main system prompt
└── .claude/
    ├── CLAUDE.md                      ← Project-level Claude context
    ├── commands/
    │   ├── gather-requirements.md     ← /gather-requirements slash command
    │   ├── show-progress.md           ← /show-progress slash command
    │   └── generate-requirements.md   ← /generate-requirements slash command
    └── skills/
        └── gather-requirements/
            ├── SKILL.md               ← Auto-invoked skill definition
            ├── template.md            ← REQUIREMENTS.md template
            └── examples/
                └── saas-platform.md   ← Example completed output
```

---

## Choosing the Right Approach

Claude Code offers three ways to customise behaviour. Here's which to use for this scenario:

| Approach | When to Use | This Scenario |
|----------|-------------|---------------|
| **System Prompt** | You want Claude to *always* behave as the analyst for the entire session | ✅ Best for dedicated requirements sessions; use `--append-system-prompt` |
| **CLAUDE.md** | You want lightweight context that applies to a project directory | ✅ Use alongside system prompt for project context |
| **Slash Command** | You want to trigger requirements mode on demand from a normal Claude session | ✅ Use `/gather-requirements` from any project |
| **Skill** | You want Claude to auto-detect when requirements work is needed | ✅ Place in `.claude/skills/` for automatic invocation |

---

## Installation

### Option A — Global (use from any project)

```bash
# 1. Create global directories
mkdir -p ~/.claude/system-prompts
mkdir -p ~/.claude/commands
mkdir -p ~/.claude/skills/gather-requirements/examples

# 2. Copy files
cp system-prompt/REQUIREMENTS-ANALYST-SYSTEM-PROMPT.md ~/.claude/system-prompts/
cp .claude/commands/*.md ~/.claude/commands/
cp .claude/skills/gather-requirements/SKILL.md ~/.claude/skills/gather-requirements/
cp .claude/skills/gather-requirements/template.md ~/.claude/skills/gather-requirements/
cp .claude/skills/gather-requirements/examples/*.md ~/.claude/skills/gather-requirements/examples/
```

### Option B — Project-level (use within a specific project)

```bash
# From your project root:
mkdir -p .claude/commands
mkdir -p .claude/skills/gather-requirements/examples

cp /path/to/this/repo/.claude/commands/*.md .claude/commands/
cp /path/to/this/repo/.claude/skills/gather-requirements/* .claude/skills/gather-requirements/
cp /path/to/this/repo/.claude/CLAUDE.md .claude/CLAUDE.md
```

---

## Usage

### Method 1 — Full System Prompt Mode (Recommended for dedicated sessions)

This makes Claude *exclusively* act as a Requirements Analyst for the entire session.

```bash
# Interactive session
claude --append-system-prompt "$(cat ~/.claude/system-prompts/REQUIREMENTS-ANALYST-SYSTEM-PROMPT.md)"

# One-off headless session
claude -p "Start a requirements session for my new mobile app" \
  --append-system-prompt "$(cat ~/.claude/system-prompts/REQUIREMENTS-ANALYST-SYSTEM-PROMPT.md)"
```

> **Tip:** Create an alias in your shell profile:
> ```bash
> alias claude-req='claude --append-system-prompt "$(cat ~/.claude/system-prompts/REQUIREMENTS-ANALYST-SYSTEM-PROMPT.md)"'
> ```
> Then just run: `claude-req`

---

### Method 2 — Slash Command (On-demand from any Claude session)

From a normal Claude Code session, type:

```
/gather-requirements
```

Or with a project name to skip Phase 1:

```
/gather-requirements My new e-commerce platform for selling handmade goods
```

Other commands:
```
/show-progress           # See what's been captured so far
/generate-requirements   # Force document generation immediately
```

---

### Method 3 — Skill (Auto-invoked)

If the skill is installed in `.claude/skills/gather-requirements/`, Claude will automatically invoke it when you say things like:

- *"Help me write up the requirements for this project"*
- *"I need to produce a spec for my architect"*
- *"Let's gather requirements for what we're building"*

You can also invoke it explicitly:

```
/gather-requirements
```

---

## What Claude Will Do

1. **Greet you** and explain the process
2. **Ask structured questions** across 6 phases (one at a time):
   - Project overview
   - Users & personas
   - Functional requirements
   - Non-functional requirements
   - Constraints & assumptions
   - Open questions & risks
3. **Summarise** what it has captured every 5–7 questions and ask for corrections
4. **Confirm** with you before generating the document
5. **Write `REQUIREMENTS.md`** to the current working directory
6. **Highlight gaps** and open questions for the architect

---

## Example Session

```
You:     /gather-requirements
Claude:  Hi! I'm your Requirements Analyst. My job is to ask you structured 
         questions so I can produce a REQUIREMENTS.md document your architect 
         can use to design the system.

         Let's start — what is the name of your project, and in one or two 
         sentences, what problem is it trying to solve?

You:     It's called HealthTrack. It helps patients log their daily symptoms 
         between doctor visits so their GP has better data.

Claude:  Great — HealthTrack sounds like it's addressing a real gap in 
         continuity of care. 

         Who are the primary users? Are we talking about patients logging 
         their own data, or would healthcare professionals also use the system 
         directly?

[... interview continues ...]

You:     I think that covers everything.

Claude:  Perfect. Let me confirm what I've captured before writing the document.
         [summary]
         Does this look right? Anything to add or correct?

You:     That's all good, go ahead.

Claude:  ✅ Writing REQUIREMENTS.md...
         [file written]
         
         I've written REQUIREMENTS.md to ./REQUIREMENTS.md
         
         Key sections with open questions:
         - Section 8: HIPAA compliance scope needs legal review
         - Section 4.3: EHR integration (Epic/FHIR) was mentioned but not 
           fully scoped — architect should investigate feasibility
         - Section 6: Hosting region for medical data not yet decided
         
         I'd recommend sharing this with your architect and scheduling a 
         30-minute review of the open questions before design begins.
```

---

## Customising the Template

Edit `.claude/skills/gather-requirements/template.md` to:
- Add your organisation's standard sections
- Change the table formats
- Add a cover page or header
- Include your company's compliance frameworks

---

## Troubleshooting

**Claude isn't asking one question at a time**  
→ Use the full system prompt (`--append-system-prompt`) rather than the skill alone, as it enforces the interview rules more strictly.

**Claude generated the document too early**  
→ Add to your CLAUDE.md: `Never generate REQUIREMENTS.md until the user explicitly says "go ahead" or "generate it".`

**The skill isn't auto-invoked**  
→ Check the skill is in `.claude/skills/gather-requirements/SKILL.md`. Try listing your skills with `/context` in Claude Code.
