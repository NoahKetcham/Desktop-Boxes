---
name: task-execution
description: Completes tasks from docs/taskHierarchy.md in priority order (High → Medium → Low → Later), applying autotaskrecc rules and maintaining docs/To_Add.md and docs/taskHierarchy.md. Use when the user says "begin user skill 1".
---

# Hierarchical Task Execution

## Trigger

Apply this skill when the user says **"begin user skill 1"**.

## Workflow

### 1. Apply the autotaskrecc rule

First, apply the rule at `.cursor/rules/autotaskrecc.mdc`. This rule defines how to maintain `docs/To_Add.md` and `docs/taskHierarchy.md`.

### 2. Read current state

- Read `docs/taskHierarchy.md` for the Recommended Priority Order
- Read `docs/To_Add.md` for the TO-DO section and task details

### 3. Execute tasks in order

Work through tasks in this order:

1. **High Priority** (complete all before moving on)
2. **Medium Priority**
3. **Low Priority**
4. **Later** (only if user requests or all others are done)

For each task:

1. Implement the task
2. Mark completion in `docs/To_Add.md`: add sub-bullet `(Completed YYYY-MM-DD)`
3. Update `docs/taskHierarchy.md`:
   - Remove completed task from Current Outstanding Tasks
   - Adjust Recommended Priority Order if needed
   - Add entry to Recent Updates: `YYYY-MM-DD – Completed: [task name]`

### 4. Document maintenance rules

- **docs/To_Add.md**: NEVER delete; mark complete with sub-bullet only
- **docs/taskHierarchy.md**: Keep Current Outstanding Tasks, Recommended Priority Order, Rationale, and Recent Updates in sync; newest updates at top of Recent Updates

## Per-task checklist

```
- [ ] Implement the task
- [ ] Add "(Completed YYYY-MM-DD)" to corresponding item in docs/To_Add.md
- [ ] Update docs/taskHierarchy.md (outstanding tasks, recent updates)
```

## Stopping points

- Stop after each task to confirm completion before proceeding
- If a task is blocked or unclear, note it and move to the next; update taskHierarchy with the blocker
