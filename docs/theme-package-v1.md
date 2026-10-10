# Theme package v1

A `.tstheme` file is a ZIP archive containing:

```text
manifest.json
theme.json
assets/                 # optional; referenced media only
```

`manifest.json` identifies the package, schema version, display name, package version, minimum app version, supported host versions, and declared asset names with media types. `theme.json` stores appearance profiles for `chat`, `work`, and `codex`.

Each view profile has one background (`solid`, `gradient`, `image`, `gif`, `mp4`, or `webm`) and semantic surface settings. Surface settings may express foreground/background colors, opacity, blur, font family, corner radius, and supported control states. Background placement includes fit, focal position, dim overlay, and blur.

Package requirements:

- Schema version must be `1`.
- All asset references are relative paths below `assets/`; absolute paths, traversal, symlinks, scripts, CSS, and HTML are rejected.
- Media extensions are allow-listed and must match the declared media kind.
- Unknown schema versions are rejected with an actionable message.
- Import extracts into a new managed directory and never overwrites the source archive or an existing theme.
- The format carries media bytes for portability. It does not fetch URLs.

An adapter may report a theme field as unsupported for a host version/view. Unsupported fields are skipped and surfaced in compatibility details rather than silently claiming full application.
