# Security policy

Report a vulnerability privately through [GitHub's private vulnerability reporting](https://github.com/Nickelony/Stanza/security/advisories/new),
not in a public issue.
Include the affected version and a minimal reproduction if you have one.

## Supported versions

Security fixes go to the latest released version of each package.

## What counts

- `Nickelony.Stanza.Analyzers` runs inside the compiler and the IDE of anyone who installs it.
- `Nickelony.Stanza.Markup` reads and writes files on disk and parses XML, including entities the document declares.
- `Nickelony.Stanza.Format` runs `git`, `dotnet format`, CSharpier and `stanza-markup` over a repository and writes its files.

Untrusted input, such as a file from someone else, that makes a package hang, crash, corrupt a file or run code it should not is a security report.
Any other crash is a bug; report it in a public issue.

## What a run trusts

- `stanza-format` runs the code a repository configures:
  the imports and targets of its MSBuild projects, the analyzers and source generators they reference,
  and the tools its tool manifest lists.
  The search for that manifest also reads the manifests above the repository, as `dotnet` does.
- Run it only on a repository you trust, as you would build one.
  Code a repository runs this way is no vulnerability of the package.
