<!-- 0c34d6bc-21ea-45f5-88ae-a9ada6f2277b 811b5335-19fd-4ef7-b661-66cfb704f304 -->
# Shortcut Data Cleanup Plan

## Investigate

- review-storage: Trace `ScannedFileService` flows (`ScanAndSaveAsync`, `ImportPathsAsync`, `CreateShortcutsAsync`) to spot where the same desktop path can be persisted multiple times.
- inspect-ui: Examine how `ShortcutSelectionViewModel`, `DesktopBoxWindowViewModel`, and `BoxWindowManager` consume the scanned data and mutate it at runtime.
- analyse-settings-pipeline: Audit the settings UI pipeline to confirm it shares the same dedupe logic and doesn’t rehydrate duplicates when data changes.

## Implement Fixes

- normalize-import: Update `ScannedFileService` so drag-and-drop/import paths reuse existing entries (match by canonical path, update metadata, avoid new IDs) and ensure the manifest mirrors those updates.
- shared-filter-service: Provide a unified service/helper that returns deduped shortcut collections for both settings and box windows, reducing drift between UIs.
- staged-box-load: Adjust the box window so it opens empty, then delays briefly and invokes a dedicated loader to populate shortcuts, ensuring the first render starts from a clean state.
- ui-validation-hooks: Add lightweight validation (logging or debug assertions) when data is bound to the UI to catch duplicate IDs/paths early.

## Validate

- test-refresh: Drag in a desktop file, confirm only one record appears in settings (scanned files) and only one tile appears after the staged load completes.
- regression-check: Re-run full desktop scan and ensure archived shortcuts/folder hierarchies stay intact.
- verification-logging: Enable the new validation checks during testing and ensure the logs stay clean of duplication warnings.
- Run build tests in agent as well as any other testing that may seem fit.

### To-dos

- [ ] Trace ScannedFileService flows to find duplicate persistence sources.
- [ ] Check shortcut-consuming viewmodels for duplication side-effects.
- [ ] Canonicalize and dedupe shortcut persistence in ScannedFileService.
- [ ] Update viewmodels to rely on deduped data and clear stale items.
- [ ] Implement staged shortcut loading in box windows (open empty, delay, populate).
- [ ] Verify drag-drop and box reopen show only single shortcut instance.
- [ ] Re-run desktop scan to ensure folders/archives stay intact.
