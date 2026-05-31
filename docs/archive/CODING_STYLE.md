# Coding Style

This solution uses default ReSharper C# formatting and layout as the baseline. When this document is more explicit than ReSharper defaults, this document defines the required project-specific rule.

The goal is not to invent a custom style guide. The goal is to stay close to default ReSharper behavior while making a few rules explicit so code review and automated cleanup remain predictable.

General rules:

- Use ReSharper default formatting, spacing, wrapping, ordering, and layout unless this document says otherwise.
- Keep style changes mechanical and behavior-preserving.
- Do not mix styles within the same file. If a file is touched for style cleanup, make it internally consistent.
- Prefer straightforward, refactoring-safe constructs over compact syntax that saves only a few characters.

Refactoring safety:

- Always use braces for `if`, `else`, `for`, `foreach`, `while`, `do`, `using`, `lock`, and `fixed`.
- Keep branches and loops visually explicit even when they contain a single statement.
- Avoid style shortcuts that make later edits more error-prone.

Constructors and type declarations:

- Do not use primary constructors.
- Use regular constructors with explicit fields or properties.
- Keep type/member declarations in the conventional ReSharper order unless there is a strong local reason to preserve an existing grouping.

Control flow:

- Prefer `switch` statements, switch expressions, and pattern matching when they describe multi-branch logic more clearly than repeated `if` / `else if`.
- Do not force a `switch` when a small guard clause or a simple binary condition is clearer.
- Prefer pattern matching for type tests and null tests when it improves readability and removes redundant casts.
- Keep nesting shallow where possible; use early returns when they simplify the method.

Accessibility and API surface:

- Do not make types or members `public` unless broader visibility is required.
- Prefer the narrowest valid accessibility for classes, structs, records, interfaces, methods, properties, fields, events, and constructors.
- When a member is only used within the declaring type, prefer `private`.
- When a member is only needed within the assembly, prefer `internal` over `public`.
- Do not widen visibility only for convenience, speculative reuse, or test access without a concrete requirement.

Properties and methods:

- Follow default ReSharper conventions for expression-bodied members, auto-properties, and member layout.
- Keep methods small enough to read linearly; extract helpers when it improves clarity, not just to reduce line count.
- Preserve existing naming unless the touched code is already being renamed for correctness or consistency.

Lambdas and local functions:

- If a lambda does not capture local state, instance state, or closure variables, mark it `static`.
- Prefer `static` lambdas for LINQ predicates, selectors, sort keys, and callbacks when capture is not required.
- Do not force `static` when the lambda legitimately needs captured state. In that case keep the lambda non-static and make the capture explicit in review.

Null handling and pattern use:

- Prefer explicit null handling that is easy to follow in the debugger.
- Prefer `is null`, `is not null`, and typed patterns when they make intent clearer than older idioms.
- Avoid overly clever pattern matching. Use it when it simplifies the code, not when it compresses it.

Cleanup workflow:

- Use ReSharper cleanup as the primary mechanism for formatting and standard style normalization.
- After automated cleanup, review touched files for the project-specific rules that ReSharper may not enforce by itself.
- Manual follow-up is expected for braces, visibility narrowing, replacing suitable multi-branch `if` chains with `switch` / pattern matching, and removing primary constructors.

What not to do:

- Do not introduce a second style system alongside ReSharper defaults.
- Do not make behavior changes while doing style-only cleanup.
- Do not widen accessibility, add new abstractions, or rewrite logic unless the change is required to satisfy the explicit rules above.
