# [Project Name] — Requirements Document

**Version:** 1.0  
**Date:** [TODAY'S DATE]  
**Status:** Draft  
**Prepared by:** Requirements Analyst (AI-assisted)  
**For:** Architecture & Planning Team  

---

## 1. Executive Summary

[2–4 sentence plain-English description of the project, the problem it solves, and who it is for.]

---

## 2. Project Context

### 2.1 Background & Motivation

[Why is this project being done? What business driver exists? What happens if it isn't built?]

### 2.2 Scope

**In Scope:**
- [Feature / capability included at launch]
- [...]

**Out of Scope (v1):**
- [Feature / capability explicitly excluded]
- [...]

### 2.3 Stakeholders

| Role | Name / Team | Interest / Concern |
|------|-------------|-------------------|
| Product Owner | … | Overall direction and priority |
| End Users | … | Usability and value delivered |
| Engineering Lead | … | Feasibility and technical constraints |
| … | … | … |

---

## 3. User Personas

### 3.1 [Persona Name]

- **Role:** …
- **Goals:** …
- **Pain Points:** …
- **Technical Proficiency:** Low / Medium / High
- **Frequency of Use:** Daily / Weekly / Occasional

### 3.2 [Persona Name]

- **Role:** …
- **Goals:** …
- **Pain Points:** …
- **Technical Proficiency:** …
- **Frequency of Use:** …

---

## 4. Functional Requirements

### 4.1 Core Features (MVP)

| ID | Feature | Description | Priority |
|----|---------|-------------|----------|
| FR-001 | … | … | Must Have |
| FR-002 | … | … | Should Have |
| FR-003 | … | … | Could Have |
| … | … | … | … |

> Priority uses MoSCoW: **Must Have** (launch blocker), **Should Have** (important, not critical), **Could Have** (nice to have), **Won't Have** (explicitly deferred).

### 4.2 User Journeys

#### Journey 1: [Name — e.g., "User Registration"]

1. User navigates to …
2. User enters …
3. System validates …
4. System responds with …
5. User proceeds to …

#### Journey 2: [Name]

1. …

### 4.3 Third-Party Integrations

| System | Integration Type | Direction | Purpose | Notes |
|--------|-----------------|-----------|---------|-------|
| … | REST API / Webhook / SDK | Inbound / Outbound / Bidirectional | … | … |

### 4.4 Data Requirements

[Describe the key entities the system creates, reads, updates, or deletes.]

**Key Entities:**
- **[Entity Name]:** [Description, key attributes]
- **[Entity Name]:** …

**Data Volumes (estimated):**
- [Entity]: ~[N] records at launch, growing by ~[N] per [period]

---

## 5. Non-Functional Requirements

| ID | Category | Requirement | Target / Metric | Notes |
|----|----------|-------------|-----------------|-------|
| NFR-001 | Performance | Page / API response time | < [X]ms at P95 under [N] concurrent users | … |
| NFR-002 | Availability | Uptime | [X]% per month | Planned maintenance windows? |
| NFR-003 | Scalability | Concurrent users | Support [N] concurrent at launch, [M] in 12 months | … |
| NFR-004 | Security | Authentication | [Method: SSO / MFA / OAuth2] | … |
| NFR-005 | Security | Authorisation | [RBAC / ABAC / etc.] | … |
| NFR-006 | Security | Data at rest | Encrypted at rest | … |
| NFR-007 | Security | Data in transit | TLS 1.2+ | … |
| NFR-008 | Data Retention | Logs retained for | [N] days / months / years | … |
| … | … | … | … | … |

### 5.1 Compliance & Regulatory

| Framework | Applicability | Key Requirements |
|-----------|---------------|-----------------|
| GDPR | [Yes / No / Partial] | … |
| HIPAA | [Yes / No] | … |
| PCI-DSS | [Yes / No] | … |
| SOC 2 Type II | [Target / Existing] | … |
| … | … | … |

---

## 6. Technical Constraints

| Constraint | Detail | Reason / Source |
|------------|--------|----------------|
| Language | … | … |
| Framework | … | … |
| Cloud Provider | … | … |
| Hosting Model | Cloud / On-premise / Hybrid | … |
| Containerisation | Docker / Kubernetes / None | … |
| Database | … | … |
| CI/CD | … | … |
| … | … | … |

---

## 7. Assumptions

The following assumptions have been made during requirements gathering. If any prove false, requirements may need to be revisited.

1. [Assumption — e.g., "Users have reliable internet access"]
2. [Assumption]
3. …

---

## 8. Open Questions & Risks

Items that must be resolved before or during architectural design.

| # | Question / Risk | Category | Owner | Priority | Notes |
|---|-----------------|----------|-------|----------|-------|
| 1 | … | Technical / Business / Legal | … | High / Medium / Low | … |
| 2 | … | … | … | … | … |

---

## 9. Glossary

| Term | Definition |
|------|------------|
| … | … |

---

## 10. Revision History

| Version | Date | Author | Summary of Changes |
|---------|------|--------|--------------------|
| 1.0 | [DATE] | Requirements Analyst (AI-assisted) | Initial draft |
