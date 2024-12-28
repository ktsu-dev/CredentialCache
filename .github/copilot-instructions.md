# Copilot Instructions

These are the rules for this project:

## Guidelines

### Do's

- **Testing Framework**
  - Use the **MSTest** framework when writing tests.

- **Variable Declarations**
  - Use the `nameof` operator when referring to the name of a variable, type, or member.
  - Use **C# 9.0** target-typed `new` expressions.

- **Consistent Encoding and Formatting**
  - Use `utf-8` charset for all files.
  - Use **spaces** for indentation with a size of **4**.
  - Insert a final newline at the end of files.
  - Trim trailing whitespace in most files.

- **File-Specific Formatting**
  - **Solution Files (`*.sln`)**: Use **tabs** for indentation.
  - **XML and Configuration Files (`*.xml`, `*.config`, etc.)**: Use an indentation size of **2**.
  - **JSON and YAML Files (`*.json`, `*.yml`, `*.yaml`)**: Use an indentation size of **2**.
  - **Markdown Files (`*.md`, `*.mdx`)**: Do not trim trailing whitespace.
  - **Web Files (`*.html`, `*.js`, `*.css`, etc.)**: Use an indentation size of **2**.
  - **Batch Files (`*.cmd`, `*.bat`)**: Use **CRLF** for end-of-line.
  - **Bash Files (`*.sh`)**: Use **LF** for end-of-line.
  - **Makefiles (`Makefile`)**: Use **tabs** for indentation.
  - **C# Files (`*.cs`, `*.csx`)**: Use **tabs** for indentation.

- **.NET Code Style Enforcement**
  - Treat all .NET code style diagnostics as **errors**.
  - Prefer language keywords over framework type names (e.g., `int` over `Int32`).
  - **Always** require accessibility modifiers.
  - Use `readonly` for fields where applicable.
  - Prefer **object** and **collection initializers**.
  - Use conditional expressions and compound assignments where appropriate.
  - Prefer simplified boolean expressions and null-checking.

- **Naming Conventions**
  - **PascalCase** for:
    - Namespaces, classes, enums, structs, delegates, events, methods, and properties.
  - **camelCase** for:
    - Private fields and local variables.
  - **Prefix Interfaces** with an uppercase 'I' (e.g., `ICredentialFactory`).
  - **Prefix Generic Type Parameters** with an uppercase 'T' (e.g., `TCredential`).

- **C# Formatting Rules**
  - Place `using` directives **inside** namespaces.
  - Use **file-scoped** namespaces.
  - Organize `using` directives with `System` namespaces first.
  - Maintain proper spacing around binary operators and within method declarations.
  - Preserve single-line blocks where appropriate.
  - Ensure braces are properly placed with new lines as configured.

- **Code Cleanliness**
  - Eliminate **unused parameters** and suppressions.
  - Prefer **discards** for unused variables.
  - Avoid unnecessary method wrappers and redundant code constructs.

- **.NET 9.0 Code Analysis Rules**
  - **Nullable Reference Types**
    - **Do** enable nullable reference types to prevent null-related issues.
    - **Do** annotate reference types with `?` where null is permitted.
    - **Do not** ignore nullable warnings; address them promptly.
  
  - **Asynchronous Programming**
    - **Do** prefer asynchronous methods (`async`/`await`) for I/O-bound operations.
    - **Do not** block asynchronous code using `.Result` or `.Wait()`.
  
  - **Pattern Matching Enhancements**
    - **Do** utilize enhanced pattern matching features for more expressive and concise code.
    - **Do not** overcomplicate conditions; maintain readability.
  
  - **Performance Optimizations**
    - **Do** use `Span<T>` and `Memory<T>` for memory-efficient operations.
    - **Do** minimize allocations in performance-critical sections.
    - **Do not** use reflection or dynamic types in performance-sensitive code.
  
  - **Security Best Practices**
    - **Do** validate all inputs to prevent injection attacks.
    - **Do** use secure cryptographic practices and avoid obsolete algorithms.
    - **Do not** expose sensitive information in logs or exceptions.
  
  - **Modern C# Features**
    - **Do** leverage records for immutable data structures where appropriate.
    - **Do** use init-only setters to enforce immutability.
    - **Do not** use deprecated or obsolete language features.
  
  - **Code Readability and Maintainability**
    - **Do** write clear and concise code with meaningful variable and method names.
    - **Do** refactor long methods into smaller, reusable components.
    - **Do not** ignore compiler or analyzer suggestions that improve code quality.

- **Quality Rules from .NET Code Analysis**
  - **Maintainability**
    - **Do** ensure methods do not exceed recommended lengths or complexity.
    - **Do** use meaningful names that convey intent.
    - **Do not** exceed recommended method lengths or introduce unnecessary complexity.
  
  - **Reliability**
    - **Do** handle exceptions appropriately.
    - **Do** avoid using deprecated APIs.
    - **Do not** ignore exception handling best practices.
    - **Do not** use deprecated APIs.
  
  - **Security**
    - **Do** follow secure coding practices to prevent vulnerabilities.
    - **Do not** introduce code vulnerabilities by ignoring secure coding practices.
  
  - **Performance**
    - **Do** write efficient code that optimizes resource usage.
    - **Do** write efficient code that optimizes resource usage.
    - **Do not** write inefficient code that wastes resources.

- **Style Rules from .NET Code Analysis**
  - **Formatting**
    - **Do** adhere to consistent code formatting as specified above.
    - **Do not** deviate from the specified formatting guidelines.
  
  - **Naming**
    - **Do** follow the naming conventions outlined above.
    - **Do not** use inconsistent or non-descriptive names.
  
  - **Design**
    - **Do** adhere to SOLID principles and other design best practices.
    - **Do not** violate SOLID principles or established design patterns.
  
  - **Usage**
    - **Do** use language features effectively to write clean and efficient code.
    - **Do not** misuse language features, leading to unclear or inefficient code.

### Don'ts

- **Avoid Inconsistent Indentation and Formatting**
  - Do **not** mix **spaces** and **tabs** for indentation.
  - Do **not** use inconsistent indentation sizes across different file types.

- **File-Specific Formatting Violations**
  - Avoid using spaces for indentation in **solution**, **Makefile**, and **C#** files where tabs are specified.
  - Do **not** trim trailing whitespace in **Markdown** files.
  - Do **not** use **CRLF** line endings in **Bash** or **Makefiles**.

- **.NET Code Style Violations**
  - Do **not** omit accessibility modifiers.
  - Do **not** use framework type names instead of language keywords.
  - Avoid complex boolean expressions without clarity.
  - Do **not** neglect object and collection initializers.

- **Variable Declarations**
  - Do **not** use the `var` keyword for the following types:
    - `string`, `int`, `long`, `float`, `double`, `decimal`, `bool`, `char`, and `object`.

- **Naming Convention Violations**
  - Do **not** use non-PascalCase names for classes, methods, or properties.
  - Do **not** prefix interfaces without an uppercase 'I'.
  - Do **not** use non-camelCase names for private fields and local variables.
  - Avoid generic type parameters without the 'T' prefix.
  - Do **not** use underscores in identifiers.

- **C# Formatting Rules Violations**
  - Do **not** place `using` directives outside of namespaces.
  - Avoid mixing file-scoped and block-scoped namespaces inconsistently.
  - Do **not** improperly space method declarations and calls.
  - Refrain from placing braces on the same line as control statements if new lines are specified.

- **Code Cleanliness Violations**
  - Do **not** leave unused parameters or suppressions in the code.
  - Avoid retaining unused variables and expressions.
  - Do **not** include redundant method calls or wrappers.

- **Miscellaneous**
  - Do **not** bypass or disable essential code analysis rules unless absolutely necessary.
  - Avoid inconsistent naming styles that do not adhere to the defined `.editorconfig` rules.

- **.NET 9.0 Code Analysis Violations**
  - **Nullable Reference Types**
    - Do **not** ignore nullable warnings or disable nullable annotations.
  
  - **Asynchronous Programming**
    - Do **not** block asynchronous operations using `.Result` or `.Wait()`.
  
  - **Pattern Matching Enhancements**
    - Do **not** create overly complex pattern matching conditions that reduce code clarity.
  
  - **Performance Optimizations**
    - Do **not** perform unnecessary memory allocations in performance-critical code.
    - Do **not** use reflection or dynamic types in scenarios where performance is essential.
  
  - **Security Best Practices**
    - Do **not** expose sensitive information in logs, exceptions, or other outputs.
    - Do **not** use weak or obsolete cryptographic algorithms.
  
  - **Modern C# Features**
    - Do **not** use deprecated language features or patterns that have more modern alternatives.
  
  - **Code Readability and Maintainability**
    - Do **not** write overly long or complex methods; strive for simplicity and clarity.
    - Do **not** ignore analyzer suggestions that improve code quality and maintainability.

- **Quality Rules from .NET Code Analysis**
  - **Maintainability**
    - Do **not** exceed recommended method lengths or introduce unnecessary complexity.
  
  - **Reliability**
    - Do **not** ignore exception handling best practices.
    - Do **not** use deprecated APIs.
  
  - **Security**
    - Do **not** introduce code vulnerabilities by ignoring secure coding practices.
  
  - **Performance**
    - Do **not** write inefficient code that wastes resources.

- **Style Rules from .NET Code Analysis**
  - **Formatting**
    - Do **not** deviate from the specified formatting guidelines.
  
  - **Naming**
    - Do **not** use inconsistent or non-descriptive names.
  
  - **Design**
    - Do **not** violate SOLID principles or established design patterns.
  
  - **Usage**
    - Do **not** misuse language features, leading to unclear or inefficient code.
