You connect one student's Java homework to a fixed set of automated tests. The tests never call the student's classes directly: they only use a neutral interface, and they get every object from `new StudentAdapter()`. Your job is to write that one class, `StudentAdapter`, for this student's code.

The adapter is a translator, nothing more. The same tests run against every student, and their results are given to the grader as evidence, so the adapter must let the student's own code succeed or fail on its own merits:

- Implement the interface by calling the student's classes and methods that do what each interface method describes, whatever the student named them (for example `deposit` might be the student's `addMoney`, `asText` their `toString`).
- Never compute, fix, filter, format or check anything yourself. Pass arguments through and return what the student's code returns. If the student's text looks different from what the tests expect, return it unchanged: a wrong format is the student's mistake to be found, not yours to hide.
- Only convert types where the student's method takes or returns a different but equivalent type (for example `int` and `Integer`, or a wrapper object the student uses for the same value), and keep it to a direct conversion.
- When the interface's methods return other interfaces, return a small wrapper (for example an anonymous class) around the student's object that does the same translation.
- When the student's code has nothing that does what an interface method describes, implement it as `throw new UnsupportedOperationException("not implemented: <what is missing>")`. Do not write the missing part yourself, and list it under "notImplemented".
- When a student's class needs constructor arguments, pass the simplest values the instructions imply (for example an empty name), and say so in "notes".
- Every `new StudentAdapter()` must be a fresh, independent object, with a public no-argument constructor.

Write `StudentAdapter` in the default package (no package declaration). When the student's classes are in a package, import them; when they are in the default package, use them directly. Use only the student's code and the Java standard library, and never change or copy the student's code. It must compile together with the student's files, the interface file and the tests on Java 21 or newer. When you are given compiler errors from an earlier attempt, fix the adapter so it compiles.

Answer with the adapter's complete source code, the interface methods you could not map, and short notes on any mapping a reader might question.
