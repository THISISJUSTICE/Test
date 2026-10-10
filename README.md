# UMLSync prototype for GitHub Pages

This is an **experimental first-pass**, not a complete C# UML generator.

## Quick start

1. Copy these files into your `Test` repository (keeping its `.git` directory).
2. Install Python 3.9+ if necessary.
3. Double-click `run-sync.bat` on Windows.
4. Open `Docs/Sample.drawio` in draw.io to inspect new nodes and connections.
5. Commit `Docs/Sample.drawio` to `main`; GitHub Pages displays the updated diagram.

To use your own local C# code, edit `Tools/UMLSync/config.json` and set each `sources[].path` to an absolute Windows path (forward slashes work) or a path relative to the repository root. Each source folder is scanned **non-recursively by default**. Multiple folders per diagram are supported. Each JSON entry has its own `output` draw.io path; additional diagrams need corresponding files and Pages links.

## Limitations and safety

- Parses basic class/interface/struct/enum/record declarations with regex, **not Roslyn**. Does not reliably handle all C# syntax (e.g. records with parameters, nested/generic inheritance, conditional compilation).
- **Existing manually drawn nodes are left untouched**, including member lists, layout, and edges. This first prototype does not update fields/properties/methods or remove obsolete types.
- Newly found nodes are placed in a staging area around x=2000; manually arrange them afterward.
- Adds only basic inheritance/implementation arrows where **both endpoints are in configured source paths**. Existing manually omitted relationships are not re-added if they are absent; this prototype cannot know which were intentionally omitted. For strict no-new-edges behavior, remove the inheritance-edge creation block in `sync.py`.
- No Git push is performed automatically. Review the result before committing.
- Use `py -3 Tools/UMLSync/sync.py --dry-run` to preview counts without writing.
- For production, replace regex with Roslyn, add stable type IDs and relationship visibility rules, and implement member-level sync while preserving layout.


https://thisisjustice.github.io/Test/