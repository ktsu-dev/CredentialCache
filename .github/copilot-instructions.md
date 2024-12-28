# Copilot Instructions

These are the rules for this project:

## Guidelines

### Testing Framework

- **Use the MSTest framework when writing tests.**

### Variable Declarations

- **Use the `nameof` operator** when referring to the name of a variable, type, or member.
- **Use C# 9.0 target-typed `new` expressions.**
- **Do not use the `var` keyword** for the following types:
  - `string`, `int`, `long`, `float`, `double`, `decimal`, `bool`, `char`, and `object`.

### Consistent Encoding and Formatting

- **Use UTF-8 charset** for all files.
- **Use tabs for indentation in C# files**, with a size of **4**.
- **Insert a final newline** at the end of files.
- **Trim trailing whitespace** in most files.
- **Do not mix spaces and tabs** for indentation.
- **Do not use inconsistent indentation sizes** across different file types.

### File-Specific Formatting

- **Solution Files (`*.sln`)**: Use **tabs** for indentation.
- **XML and Configuration Files (`*.xml`, `*.config`, etc.)**: Use an indentation size of **2 spaces**.
- **JSON and YAML Files (`*.json`, `*.yml`, `*.yaml`)**: Use an indentation size of **2 spaces**.
- **Markdown Files (`*.md`, `*.mdx`)**: **Do not trim trailing whitespace**.
- **Web Files (`*.html`, `*.js`, `*.css`, etc.)**: Use an indentation size of **2 spaces**.
- **Batch Files (`*.cmd`, `*.bat`)**: Use **CRLF** for end-of-line.
- **Bash Files (`*.sh`)**: Use **LF** for end-of-line.
- **Makefiles (`Makefile`)**: Use **tabs** for indentation.

**Formatting Violations:**

- **Do not use spaces** for indentation in files where **tabs** are specified (**solution**, **Makefile**, and **C#** files).
- **Do not trim trailing whitespace** in **Markdown** files.
- **Do not use CRLF line endings** in **Bash** or **Makefiles**.

### .NET Code Style Enforcement

- **Treat all .NET code style diagnostics as errors.**
- **Prefer language keywords** over framework type names (e.g., `int` over `Int32`).
- **Always require accessibility modifiers.**
- **Use `readonly`** for fields where applicable.
- **Prefer object and collection initializers.**
- **Use conditional expressions and compound assignments** where appropriate.
- **Prefer simplified boolean expressions** and null-checking.

**Code Style Violations:**

- **Do not omit accessibility modifiers.**
- **Do not use framework type names** instead of language keywords.
- **Avoid complex boolean expressions** without clarity.
- **Do not neglect object and collection initializers.**

### Naming Conventions

- **PascalCase** for:
  - **Namespaces**, **classes**, **enums**, **structs**, **delegates**, **events**, **methods**, and **properties**.
- **camelCase** for:
  - **Private fields** and **local variables**.
- **Prefix interfaces with an uppercase 'I'** (e.g., `ICredentialFactory`).
- **Prefix generic type parameters with an uppercase 'T'** (e.g., `TCredential`).
- **Do not use underscores** in identifiers.
- **Do not use inconsistent or non-descriptive names.**

**Naming Violations:**

- **Do not use non-PascalCase names** for classes, methods, or properties.
- **Do not prefix interfaces** without an uppercase 'I'.
- **Do not use non-camelCase names** for private fields and local variables.
- **Avoid generic type parameters** without the 'T' prefix.

### C# Formatting Rules

- **Place `using` directives inside namespaces.**
- **Use file-scoped namespaces.**
- **Organize `using` directives** with `System` namespaces first.
- **Maintain proper spacing** around binary operators and within method declarations.
- **Preserve single-line blocks** where appropriate.
- **Ensure braces** are properly placed with new lines as configured.

**Formatting Violations:**

- **Do not place `using` directives outside** of namespaces.
- **Avoid mixing file-scoped and block-scoped namespaces** inconsistently.
- **Do not improperly space** method declarations and calls.
- **Refrain from placing braces** on the same line as control statements if new lines are specified.

### Code Cleanliness

- **Eliminate unused parameters** and suppressions.
- **Prefer discards** (`_`) for unused variables.
- **Avoid unnecessary method wrappers** and redundant code constructs.

**Cleanliness Violations:**

- **Do not leave unused parameters** or suppressions in the code.
- **Avoid retaining unused variables** and expressions.
- **Do not include redundant method calls** or wrappers.

### .NET 9.0 Code Analysis Rules

#### Nullable Reference Types

- **Enable nullable reference types** to prevent null-related issues.
- **Annotate reference types with `?`** where null is permitted.
- **Do not ignore nullable warnings**; address them promptly.

#### Asynchronous Programming

- **Prefer asynchronous methods (`async`/`await`)** for I/O-bound operations.
- **Do not block asynchronous code** using `.Result` or `.Wait()`.

#### Pattern Matching Enhancements

- **Utilize enhanced pattern matching features** for more expressive and concise code.
- **Do not overcomplicate conditions**; maintain readability.

#### Performance Optimizations

- **Use `Span<T>` and `Memory<T>`** for memory-efficient operations.
- **Minimize allocations** in performance-critical sections.
- **Do not use reflection or dynamic types** in performance-sensitive code.

#### Security Best Practices

- **Validate all inputs** to prevent injection attacks.
- **Use secure cryptographic practices** and avoid obsolete algorithms.
- **Do not expose sensitive information** in logs, exceptions, or other outputs.
- **Do not use weak or obsolete cryptographic algorithms.**

#### Modern C# Features

- **Leverage records** for immutable data structures where appropriate.
- **Use init-only setters** to enforce immutability.
- **Do not use deprecated or obsolete language features** or patterns.

#### Code Readability and Maintainability

- **Write clear and concise code** with meaningful variable and method names.
- **Refactor long methods** into smaller, reusable components.
- **Do not write overly long or complex methods**; strive for simplicity and clarity.
- **Do not ignore compiler or analyzer suggestions** that improve code quality and maintainability.

### Quality Rules from .NET Code Analysis

#### Maintainability

- **Ensure methods do not exceed** recommended lengths or complexity.
- **Use meaningful names** that convey intent.
- **Do not introduce unnecessary complexity** into the code.

#### Reliability

- **Handle exceptions appropriately.**
- **Avoid using deprecated APIs.**
- **Do not ignore exception handling best practices.**

#### Security

- **Follow secure coding practices** to prevent vulnerabilities.
- **Do not introduce code vulnerabilities** by ignoring secure coding practices.

#### Performance

- **Write efficient code** that optimizes resource usage.
- **Do not write inefficient code** that wastes resources.

### Style Rules from .NET Code Analysis

#### Formatting

- **Adhere to consistent code formatting** as specified above.
- **Do not deviate** from the specified formatting guidelines.

#### Naming

- **Follow the naming conventions** outlined above.
- **Do not use inconsistent or non-descriptive names.**

#### Design

- **Adhere to SOLID principles** and other design best practices.
- **Do not violate SOLID principles** or established design patterns.

#### Usage

- **Use language features effectively** to write clean and efficient code.
- **Do not misuse language features**, leading to unclear or inefficient code.

### Additional Best Practices

#### Code Documentation

- **Use XML comments** for public APIs to provide clear documentation.
- **Do not leave public methods or classes undocumented.**

#### Exception Handling

- **Catch specific exceptions** rather than general ones.
- **Avoid swallowing exceptions** without handling them.
- **Use exception filters** where appropriate.
- **Do not use empty catch blocks** or catch general exceptions.

#### Dependency Injection

- **Utilize dependency injection** to manage dependencies and promote testability.
- **Do not tightly couple classes** by instantiating dependencies directly.

#### Logging

- **Implement logging** using a consistent logging framework.
- **Log meaningful information** at appropriate log levels (e.g., Information, Warning, Error).
- **Do not log sensitive information.**

#### Avoid Magic Numbers and Strings

- **Use constants or enums** instead of hard-coded values.
- **Do not embed magic numbers or strings** directly in the code.

#### Single Responsibility Principle

- **Ensure each class or method** has a single responsibility.
- **Do not combine multiple responsibilities** within a single class or method.

#### DRY (Don't Repeat Yourself)

- **Abstract and reuse common functionality.**
- **Do not duplicate code** across the codebase.

#### KISS (Keep It Simple, Stupid)

- **Write simple and straightforward code.**
- **Do not introduce unnecessary complexity.**

#### YAGNI (You Aren't Gonna Need It)

- **Implement features based on current requirements.**
- **Do not add functionality** anticipating future needs unless necessary.

#### Thread Safety

- **Ensure thread-safe operations** in multi-threaded environments.
- **Do not introduce race conditions** or deadlocks.

#### Lambda Expressions and LINQ

- **Use lambda expressions and LINQ** for concise and readable code.
- **Do not create overly complex LINQ queries** that reduce readability.

#### Method Length and Complexity

- **Keep methods concise and focused.**
- **Do not create methods** that are excessively long or complex.

#### Code Reviews

- **Participate in regular code reviews** to maintain code quality.
- **Do not bypass peer reviews** for critical changes.

### Miscellaneous

- **Do not bypass or disable essential code analysis rules** unless absolutely necessary.
- **Avoid inconsistent naming styles** that do not adhere to the defined `.editorconfig` rules.
