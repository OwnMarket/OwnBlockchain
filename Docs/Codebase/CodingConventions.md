# Coding Conventions

To improve the readability of the codebase, and avoid multiple different coding styles and conventions to spread through it, we're defining the conventions below to be followed.


## Formatting

Formatting rules defined here apply not only to F# code, but are generally applicable to many other languages (C#, JavaScript, SQL).

### Line length

- Max line length is 120 characters.

### Spaces

- Put one empty space before and after operators (including type definition, e.g. `let myFunction (p : int) = ...`.

- Put one empty space before and after inline comment declaration `//`.

- Put one empty space after, but none before, commas `,` and semicolons `;`.
    ```fsharp
    // Good:
    let myList = [1; 2; 3; 4; 5]
    let myTuple = (1, 2, 3, 4, 5)

    // Bad:
    let myList = [1;2;3;4;5]
    let myList = [1 ;2 ;3 ;4 ;5]
    let myList = [1 ; 2 ; 3 ; 4 ; 5]
    let myTuple = (1,2,3,4,5)
    let myTuple = (1 ,2 ,3 ,4 ,5)
    let myTuple = (1 , 2 , 3 , 4 , 5)
    ```

- Don't put empty spaces at the end of the line.

- Don't put more than one empty space between any two characters (applies to comments as well). Two or more consecutive spaces are not allowed, except multiples of 4 (indentation) at the beginning of the line.
    ```fsharp
    let x = 1 // Good line and comment

    let x=1 // Bad line
    let x= 1 // Bad line
    let x =1 // Bad line
    let x =  1 // Bad line
    let x  = 1 // Bad line
    let x = 1// Bad comment
    let x = 1 //Bad comment
    let x = 1 //  Bad comment
    let x = 1  // Bad comment
    ```

- Don't vertically align the assignments.
    ```fsharp
    // Good:
    let something = 1
    let somethingElse = 2
    let somethingThird = 3

    // Bad:
    let something      = 1
    let somethingElse  = 2
    let somethingThird = 3
    ```


### Indentation

- Don't use `tab` for indentation; use `4` spaces instead.
- Use exactly `4` spaces per indentation level.
- Don't increase indentation (indenting of next line) for more than one indentation level at a time.

    ```fsharp
    // Good: Parentheses closed at the indentation level of the opening line!
    let myList = [
        one
        two
        three
    ]

    let myFunction x =
        [1 .. 10]
        |> List.map (fun x ->
            let y = doSomethingWith x
            ...
        )

    // Bad (more than one indent and indentation not divisible by 4):
    let myList = [
                     one
                     two
                     three
                 ]

    let myFunction x =
        [1 .. 10]
        |> List.map (fun x ->
                         let y = doSomethingWith x
                         ...
                    )
    ```

### Empty Lines

- Don't put empty lines at the top or bottom of the file.
- Don't put multiple consecutive empty lines in the code.
- Don't put empty lines at the beginning of a function, except when signature is multi-line (which usually happens when explicit type declaration is involved). E.g.:
    ```fsharp
    // Good
    let myFunction param1 param2 param3 =
        let x = 1
        ...

    // Good
    let myFunction
        (param1 : SomeType1)
        (param2 : SomeType2)
        (param3 : SomeType3)
        : SomeReturnType =

        let x = 1
        ...

    ```
- Put one empty line before each function (even before the first function in the module, immediately after module declaration).
- Put one new-line character at the end of the file (avoids change of the last line in source code history when new lines are added).
- Separate larger code sections with `100` slashes long ribbons:
    ```fsharp
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Section
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ```

    Why 100 slashes?
    - Function code usually starts at second indentation level (8 spaces), because module is declared at zero indentation, and function at first level (4 spaces). This gives us three additional levels on which such sections will fit (because 100 + (5 * 4) = 120). Once a section separation is needed on the sixth indentation level, it is a clear sign that some refactoring is needed.
    - Aesthetically pleasing uniform length, with slashes looking like a [yellow ribbon](https://www.google.com/search?q=yellow+warning+ribbon&tbm=isch) :)


## Naming

Definition of styles:

Style | Example
--- | ---
Pascal case | `ThisIsPascalCase`
Camel case | `thisIsCamelCase`
Snake case | `this_is_snake_case`
All caps | `THIS_IS_ALL_CAPS`

Use `PascalCase` for:
- File names (extension should be lowercase though; e.g. `MyFile.txt`)
- Namespaces
- Modules
- Types (discriminated unions, records, interfaces, classes)
- Type members (properties, methods)

Use `camelCase` for:
- Functions
- Function parameters
- Method parameters
- Local values
- In general all `let` bindings

Use `snake_case` for:
- Database objects (tables, columns)
- Shell scripts

Use `ALL_CAPS` for:
- Environment variables

### Specific names

- Use `Tx` instead of `Transaction` when referring to blockchain transactions. This avoids ambiguity and makes searching for DB transaction related code in the codebase easier.


## F# Specific Rules

- If the first parameter of a function is tuple, it can remind of the C# methods and cause the developer to call it like:
    ```fsharp
    someFunction(x, y)
    ```
    However, if a function has a second parameter, a call would look like this:
    ```fsharp
    someFunction(x, y) z
    ```
    which is a bit weird.
    Therefore, always put a space between a function name and its first parameter, as well as between any subsequent parameters:
    ```fsharp
    someFunction (x, y) z
    ```
    Basically, apply the same logic as when passing parameters to the command in the terminal, where parameters are separated by spaces, but the ones containing spaces are enclosed in quotes (parentheses in F# case).

- Use .NET naming convention for generic parameters:
    ```fsharp
    // Good
    let myFunction (x : 'TSomeInput) : 'TSomeOutput =
        ...

    // Bad
    let myFunction (x : 'a) : 'b =
        ...
    ```

- Sort `open` statements in following order:

    1. `System`
    2. `System.*`
    3. Any 3rd party namespaces
    4. `Own.Common`
    5. `Own.*`
