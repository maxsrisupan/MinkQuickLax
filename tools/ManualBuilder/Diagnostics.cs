namespace ManualBuilder;

public enum Severity
{
    /// <summary>Stops the build: broken links, duplicate or missing ids.</summary>
    Error,

    /// <summary>Printed only: untranslated English chapters are expected in phase 1 (SPEC 4.9).</summary>
    Note,
}

/// <param name="File">Path relative to the manual folder, or the file that references the manual.</param>
/// <param name="Code">Stable code so MSBuild shows errors in its usual format.</param>
public sealed record Diagnostic(Severity Severity, string File, int Line, string Code, string Message)
{
    /// <summary>The canonical <c>file(line): error CODE: message</c> form that MSBuild turns into a build error.</summary>
    public override string ToString() =>
        Severity == Severity.Error
            ? $"{File}({Line}): error {Code}: {Message}"
            : $"{File}({Line}): note {Code}: {Message}";
}
