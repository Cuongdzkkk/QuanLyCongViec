# SprintA Domain Context

SprintA is a work-management and collaboration SaaS. Use these terms consistently.

## Core hierarchy

- **Workspace / Site**: top-level user/team environment. A new user should be guided to create this first.
- **Project**: work container inside a workspace/site.
- **Member**: user participating in a workspace/project.
- **Scrum Project**: backlog and sprint-based workflow.
- **Kanban Project**: continuous-flow board with explicit workflow and optional WIP limits.
- **Task / WorkTask**: unit of work with assignee, status, priority, dates, dependencies, comments and activity.
- **Sprint**: Scrum-only timebox. Do not invent a Sprint concept for a pure Kanban project.
- **Backlog**: prioritized work not yet committed to an active sprint or flow stage.
- **Board**: visualization of workflow states.
- **Collaboration**: channel, direct message, reply, mention, attachment, call and notification features.
- **Reward / Gamification**: points, badges, kudos, achievements and related activity derived from real system events.
- **AI**: assistant capabilities that operate on real project/workspace context and respect authorization and privacy.

## Product priorities

1. Consistent modern design system from desktop to mobile.
2. Correct Agile/Scrum/Kanban semantics and workflow.
3. Every visible function works end-to-end.
4. First-user onboarding: account -> workspace/site -> project -> first work item.
5. Real persisted data; no production mock data.
6. Security, permissions, tests and observability are non-negotiable.

## Definition of done

A feature is not done because a button exists. Verify as applicable:

- UI and responsive states
- loading, empty, error and disabled states
- validation
- API integration
- real database persistence
- refresh/re-login persistence
- authorization
- realtime propagation when relevant
- automated regression coverage
- no dead controls
- discoverability of important features
