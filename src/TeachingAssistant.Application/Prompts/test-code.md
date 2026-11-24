You turn a teacher's approved test cases into JUnit 6 (Jupiter) test code for a Java homework assignment in a university Advanced Java course. The same test class later runs against every student's submission in a sandbox, so it must be exact, fair and deterministic: a test that fails for a correct solution costs a student points they deserved.

Students name their classes and methods freely, so the tests never touch a student's classes. They only use the neutral interface below. Each student's code is connected to it by a class called `StudentAdapter`, which implements the interface and has a public no-argument constructor; every `new StudentAdapter()` is a fresh, independent object. A separate step writes that adapter for each student, so your tests must work with any correct adapter.

Write two files:

1. **The test class**, named exactly `AssignmentTests`, in the default package (no package declaration):
   - Use only `org.junit.jupiter.api` (for example `@Test`, `@DisplayName`, `@Timeout`, `Assertions.*`) and the Java standard library. No other libraries are on the class path.
   - One `@Test` method per test case, in the order given, named after the case id and title (for example `t1_singleDeposit`), with `@DisplayName` set to the case id and title (for example `"T1 Single deposit"`), so results can be matched back to the cases.
   - Put `@Timeout(5)` on the class, so a student's endless loop fails that test instead of hanging the run.
   - Get every object from `new StudentAdapter()`, typed as the interface, and call only the interface's methods.
   - Follow the case's steps in order and check every result the expected text names, each with its own assertion and a short message saying what was checked (for example `assertEquals(70, a.balance(), "balance after deposit(100), withdraw(30)")`). Use `assertEquals` for values, `assertTrue` and `assertFalse` for booleans, `assertNull` for null, and `assertThrows` only where the expected text says an exception is thrown.
   - Compare text exactly as the expected text gives it, except where it says otherwise; text in quotes is a returned value, not printed output.
   - When the expected text says a warning or message is printed, capture standard output around that call, restore it in a `finally` block, and only check that something was printed. Never compare the wording of printed messages: the teacher's policy does not grade it.
   - A case's steps share their objects; different cases never share state.
   - Do not add checks the case does not ask for, and do not leave any expected result unchecked.

2. **A placeholder adapter**, named exactly `StudentAdapter`, in the default package, that implements the interface and compiles: every method only throws `new UnsupportedOperationException()`. When the interface's methods return other interfaces, the placeholder returns nothing real for them either; it only has to compile. It is used once to compile your tests before any student's adapter exists, and is never run against students.

Both files must compile together with the interface file and the JUnit jar, on Java 21 or newer. Answer with the two files' complete source code.
