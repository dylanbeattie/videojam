# TaskFlow SaaS — Requirements Document

**Version:** 1.0  
**Date:** 2026-02-19  
**Status:** Draft  
**Prepared by:** Requirements Analyst (AI-assisted)  
**For:** Architecture & Planning Team  

---

## 1. Executive Summary

TaskFlow is a cloud-based task and project management SaaS platform targeting small-to-medium software development teams (5–50 people). It replaces the team's current reliance on scattered spreadsheets and Jira (which they find too complex) with a lightweight, opinionated workflow tool. The MVP must ship within 4 months and support paying customers from day one.

---

## 2. Project Context

### 2.1 Background & Motivation

The founders identified that many SME dev teams find enterprise tools like Jira and Asana over-engineered for their scale. TaskFlow aims to occupy the "just enough process" space — more structured than a Trello board, far simpler than Jira. The business model is subscription SaaS (per-seat pricing).

### 2.2 Scope

**In Scope:**
- Project and board management (Kanban view)
- Task creation, assignment, prioritisation, and status tracking
- User authentication (email/password + Google SSO)
- Team and workspace management
- In-app notifications
- Basic reporting (burndown, velocity by sprint)
- REST API for third-party integrations
- Slack notifications (outbound webhook)

**Out of Scope (v1):**
- Native mobile apps (web-responsive only at launch)
- Time tracking
- Billing / subscription management UI (handled by Stripe directly at launch)
- Git integration
- AI-assisted features

### 2.3 Stakeholders

| Role | Name / Team | Interest / Concern |
|------|-------------|-------------------|
| Product Owner | Founders | Product direction and GTM timeline |
| End Users | Dev team members | Simplicity, speed, reliability |
| Team Admins | Engineering managers | User management, reporting |
| Infrastructure | TBD (solo DevOps contractor) | Cost-efficient, low-maintenance deployment |

---

## 3. User Personas

### 3.1 Developer (Primary)

- **Role:** Individual contributor on a software team
- **Goals:** Quickly see what they're working on, update task status, leave comments
- **Pain Points:** Too many clicks in current tools; notifications are noisy and not actionable
- **Technical Proficiency:** High
- **Frequency of Use:** Multiple times per day

### 3.2 Engineering Manager (Admin)

- **Role:** Team lead responsible for sprint planning and delivery reporting
- **Goals:** Clear visibility of team progress; easy sprint planning; exportable reports
- **Pain Points:** Can't trust data in current spreadsheets; too much manual work to produce status updates
- **Technical Proficiency:** High
- **Frequency of Use:** Daily

---

## 4. Functional Requirements

### 4.1 Core Features (MVP)

| ID | Feature | Description | Priority |
|----|---------|-------------|----------|
| FR-001 | Kanban Board | Visual board with customisable columns; drag-and-drop task movement | Must Have |
| FR-002 | Task Management | Create/edit/delete tasks; assign to user; set due date, priority, labels | Must Have |
| FR-003 | Sprint Management | Create sprints; add tasks to sprints; start/complete sprints | Must Have |
| FR-004 | User Auth | Email+password signup; Google SSO; password reset | Must Have |
| FR-005 | Workspace & Teams | Multi-team workspace; invite members by email; role management | Must Have |
| FR-006 | In-app Notifications | Notify on assignment, comment, due date approaching | Should Have |
| FR-007 | Slack Integration | Outbound webhook notifications to Slack channel | Should Have |
| FR-008 | Basic Reporting | Burndown chart, sprint velocity, open/closed task counts | Should Have |
| FR-009 | REST API | Public API for task CRUD; OAuth2 tokens | Could Have |
| FR-010 | Activity Log | Per-task history of changes | Could Have |

### 4.2 User Journeys

#### Journey 1: Developer Updates a Task

1. Developer logs in and lands on their personal dashboard
2. Developer sees tasks assigned to them, sorted by due date
3. Developer clicks a task to open the detail view
4. Developer moves the task to "In Review" via drag-and-drop or status dropdown
5. Developer adds a comment
6. Assigned reviewer receives an in-app notification

#### Journey 2: Manager Plans a Sprint

1. Manager navigates to the Sprint Planning view
2. Manager creates a new sprint with a name and date range
3. Manager drags backlog items into the sprint
4. Manager clicks "Start Sprint" — sprint becomes active
5. Team members see the sprint board updated in real time

### 4.3 Third-Party Integrations

| System | Integration Type | Direction | Purpose | Notes |
|--------|-----------------|-----------|---------|-------|
| Google OAuth2 | OAuth2 | Inbound | SSO login | Must support Google Workspace accounts |
| Slack | Outbound Webhook | Outbound | Notifications | User configures channel per workspace |
| Stripe | Redirect / Webhook | Outbound / Inbound | Subscription billing | Customer Portal only at v1 |
| SendGrid | SMTP / API | Outbound | Transactional email | Password reset, invitation emails |

### 4.4 Data Requirements

**Key Entities:**
- **Workspace:** Top-level container; has many Teams and Users
- **User:** Belongs to one or more Workspaces; has a Role per Workspace
- **Project:** Belongs to a Workspace; has Boards and Sprints
- **Task:** Belongs to a Project; has Status, Assignee, Priority, Labels, Due Date, Comments
- **Sprint:** Belongs to a Project; has Tasks; has a State (planned / active / complete)
- **Comment:** Belongs to a Task; has Author and timestamp

**Data Volumes (estimated):**
- Users: ~500 at launch, ~5,000 in 12 months
- Tasks: ~50,000 at launch, ~500,000 in 12 months

---

## 5. Non-Functional Requirements

| ID | Category | Requirement | Target / Metric | Notes |
|----|----------|-------------|-----------------|-------|
| NFR-001 | Performance | API response time | < 200ms at P95 under 500 concurrent users | |
| NFR-002 | Performance | Page load (first meaningful paint) | < 2s on 4G mobile | |
| NFR-003 | Availability | Uptime | 99.9% monthly (< 44 min downtime/month) | Planned maintenance via status page |
| NFR-004 | Scalability | Concurrent users | 500 at launch; 5,000 in 12 months | Horizontal scaling required |
| NFR-005 | Security | Authentication | Email/password + Google SSO; sessions expire after 30 days idle | |
| NFR-006 | Security | Authorisation | Role-based: Admin, Member, Viewer per workspace | |
| NFR-007 | Security | Data in transit | TLS 1.3 | |
| NFR-008 | Security | Data at rest | AES-256 encrypted at database and storage level | |
| NFR-009 | Data Retention | User data on deletion | Soft-delete; hard purge after 30 days | GDPR compliance |
| NFR-010 | Audit | Activity logging | All task mutations logged with actor and timestamp | |

### 5.1 Compliance & Regulatory

| Framework | Applicability | Key Requirements |
|-----------|---------------|-----------------|
| GDPR | Yes (EU customers expected from launch) | Right to erasure; data export; privacy policy; DPA with sub-processors |
| SOC 2 Type II | Target within 18 months | Implement controls now to support future audit |
| HIPAA | No | Not handling health data |
| PCI-DSS | No | Stripe handles all card data |

---

## 6. Technical Constraints

| Constraint | Detail | Reason / Source |
|------------|--------|----------------|
| Frontend | React + TypeScript | Founders' existing expertise |
| Backend | Node.js + TypeScript | Founders' existing expertise |
| Database | PostgreSQL | Relational data model; founder familiarity |
| Cloud | AWS | Existing accounts and credits |
| Containerisation | Docker + ECS Fargate | Low-ops overhead; no k8s experience on team |
| CI/CD | GitHub Actions | Already in use |
| Deployment regions | eu-west-1 (Ireland) primary; us-east-1 secondary | GDPR data residency preference |

---

## 7. Assumptions

1. Users will access TaskFlow via modern browsers (Chrome, Firefox, Safari, Edge — last 2 major versions)
2. The team will not need to support IE11 or legacy browsers
3. All team members have stable internet access (no offline mode required in v1)
4. Google SSO covers the majority of enterprise sign-in needs; SAML/SCIM is not required for v1
5. The founders will handle all DevOps manually or with one contractor; no dedicated ops team

---

## 8. Open Questions & Risks

| # | Question / Risk | Category | Owner | Priority | Notes |
|---|-----------------|----------|-------|----------|-------|
| 1 | Real-time updates: polling vs WebSockets? WebSockets add complexity; polling may be fine at this scale | Technical | Architect | High | Impacts board UX significantly |
| 2 | Multi-region data residency: how strictly must EU data stay in eu-west-1? | Legal / Technical | Founder + Legal | High | Affects caching and CDN strategy |
| 3 | What happens to data when a subscription lapses? Read-only grace period? Immediate lockout? | Business | Product Owner | Medium | Must be decided before billing integration |
| 4 | File attachments on tasks: needed for v1? Not mentioned but commonly expected | Product | Product Owner | Medium | Could significantly affect storage architecture |
| 5 | Rate limiting on public REST API — what are the tier limits? | Technical | Architect | Low | Needed before API goes public |

---

## 9. Glossary

| Term | Definition |
|------|------------|
| Workspace | The top-level account container; a company or organisation's TaskFlow account |
| Sprint | A time-boxed iteration (typically 1–2 weeks) containing a set of tasks |
| Kanban Board | A visual project view with columns representing task states |
| MoSCoW | Prioritisation framework: Must Have, Should Have, Could Have, Won't Have |
| GDPR | General Data Protection Regulation (EU data privacy law) |

---

## 10. Revision History

| Version | Date | Author | Summary of Changes |
|---------|------|--------|--------------------|
| 1.0 | 2026-02-19 | Requirements Analyst (AI-assisted) | Initial draft following discovery session |
