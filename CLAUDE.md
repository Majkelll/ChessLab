# Code style

Do not add comments to code. This means no `//` line comments, no `/* */` block comments,
and no `///` XML doc comments — in any file, in any language, including new files you create
(DTOs, tests, JS, Razor, everything). Code should be written clearly enough (good names, small
functions, obvious structure) that it explains itself without comments. If you feel the urge to
explain a "why", put that in the commit message or PR description instead, never in the file.
