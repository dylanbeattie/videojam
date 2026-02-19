---
description: Generate REQUIREMENTS.md from all requirements captured in this session
allowed-tools: Write, Read
---

# Generate REQUIREMENTS.md

Based on everything discussed in this session, generate a complete `REQUIREMENTS.md` file now.

Write the file to the current working directory. Use the following structure:

```markdown
# [Project Name] — Requirements Document

**Version:** 1.0  
**Date:** [today's date]  
**Status:** Draft  
**Prepared by:** Requirements Analyst (AI-assisted)  
**For:** Architecture & Planning Team  

---

## 1. Executive Summary
[2–4 sentence description of project, problem, and users]

---

## 2. Project Context

### 2.1 Background & Motivation
### 2.2 Scope
**In Scope:** ...
**Out of Scope:** ...
### 2.3 Stakeholders
| Role | Name / Team | Interest |

---

## 3. User Personas
### 3.1 [Persona Name]
- Role, Goals, Pain Points, Technical Proficiency

---

## 4. Functional Requirements

### 4.1 Core Features (MVP)
| ID | Feature | Description | Priority |

### 4.2 User Journeys
### 4.3 Integrations
| System | Type | Direction | Notes |
### 4.4 Data Requirements

---

## 5. Non-Functional Requirements
| ID | Category | Requirement | Target / Metric |

### 5.1 Compliance & Regulatory

---

## 6. Technical Constraints
| Constraint | Detail | Reason |

---

## 7. Assumptions
1. ...

---

## 8. Open Questions & Risks
| # | Question / Risk | Owner | Priority |

---

## 9. Glossary
| Term | Definition |

---

## 10. Revision History
| Version | Date | Author | Changes |
```

For any sections where information was not gathered, write `[To be determined — further discovery needed]` rather than leaving blank or omitting the section.

After writing the file, tell the user:
1. Confirm the file was written and its location
2. List the top 3 sections with the most open questions or gaps
3. Suggest they share the document with their architect and schedule a review of open questions before design begins
