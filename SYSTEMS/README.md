# SYSTEMS — Local Persistence

- `Profiles/` owns local commander identities in `user://Profiles/profiles.json`. Profile names are for display and GUIDs identify save directories.
- `Saving/` owns versioned completion data in `user://Profiles/{guid}/campaign_progress.json`. The catalog determines unlocks. Progress writes occur on explicit completion.
- `Settings/` owns global display options in `user://Settings/options.json`.

`AtomicJsonFile` bounds and validates reads, flushes a temporary file before replacement, and retains the previous valid primary as a backup. If the primary is unreadable, a valid backup can be used. If both files are unreadable, the menu reports the error and preserves the files.
