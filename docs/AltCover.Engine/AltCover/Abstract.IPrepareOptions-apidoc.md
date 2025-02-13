# Abstract.IPrepareOptions interface

Command line options for `AltCover`

Usage

```csharp
 using Altcover;
 Abstract.IPrepareOptions prepare = ...
```

or

```csharp
 using static Altcover.Abstract;
 IPrepareOptions prepare = ...
```

```csharp
public interface IPrepareOptions
```

## Members

| name | description |
| --- | --- |
| [AssemblyExcludeFilter](Abstract.IPrepareOptions/AssemblyExcludeFilter-apidoc) { get; } | Corresponds to command line option `-e, --assemblyExcludeFilter=VALUE` |
| [AssemblyFilter](Abstract.IPrepareOptions/AssemblyFilter-apidoc) { get; } | Corresponds to command line option `-s, --assemblyFilter=VALUE` |
| [AttributeFilter](Abstract.IPrepareOptions/AttributeFilter-apidoc) { get; } | Corresponds to command line option `-a, --attributeFilter=VALUE` |
| [AttributeTopLevel](Abstract.IPrepareOptions/AttributeTopLevel-apidoc) { get; } | Corresponds to command line option `--attributetoplevel=VALUE` |
| [BranchCover](Abstract.IPrepareOptions/BranchCover-apidoc) { get; } | Corresponds to command line option `--branchcover` |
| [CallContext](Abstract.IPrepareOptions/CallContext-apidoc) { get; } | Corresponds to command line option `-c, --callContext=VALUE` |
| [CommandLine](Abstract.IPrepareOptions/CommandLine-apidoc) { get; } | Corresponds to the command line to run, given after a `-- ` |
| [Defer](Abstract.IPrepareOptions/Defer-apidoc) { get; } | Corresponds to command line option `--defer` |
| [Dependencies](Abstract.IPrepareOptions/Dependencies-apidoc) { get; } | Corresponds to command line option `-d, --dependency=VALUE` |
| [ExposeReturnCode](Abstract.IPrepareOptions/ExposeReturnCode-apidoc) { get; } | Corresponds to the converse of command line option `--dropReturnCode ` |
| [FileFilter](Abstract.IPrepareOptions/FileFilter-apidoc) { get; } | Corresponds to command line option `-f, --fileFilter=VALUE` |
| [InPlace](Abstract.IPrepareOptions/InPlace-apidoc) { get; } | Corresponds to command line option `--inplace` |
| [InputDirectories](Abstract.IPrepareOptions/InputDirectories-apidoc) { get; } | Corresponds to command line option `-i, --inputDirectory=VALUE` |
| [Keys](Abstract.IPrepareOptions/Keys-apidoc) { get; } | Corresponds to command line option `-k, --key=VALUE` |
| [LineCover](Abstract.IPrepareOptions/LineCover-apidoc) { get; } | Corresponds to command line option `--linecover` |
| [LocalSource](Abstract.IPrepareOptions/LocalSource-apidoc) { get; } | Corresponds to command line option `-l, --localSource` |
| [MethodFilter](Abstract.IPrepareOptions/MethodFilter-apidoc) { get; } | Corresponds to command line option `-m, --methodFilter=VALUE` |
| [MethodPoint](Abstract.IPrepareOptions/MethodPoint-apidoc) { get; } | Corresponds to command line option `--methodpoint` |
| [MethodTopLevel](Abstract.IPrepareOptions/MethodTopLevel-apidoc) { get; } | Corresponds to command line option `--methodtoplevel=VALUE` |
| [OutputDirectories](Abstract.IPrepareOptions/OutputDirectories-apidoc) { get; } | Corresponds to command line option `-o, --outputDirectory=VALUE` |
| [PathFilter](Abstract.IPrepareOptions/PathFilter-apidoc) { get; } | Corresponds to command line option `-p, --pathFilter=VALUE` |
| [Report](Abstract.IPrepareOptions/Report-apidoc) { get; } | Corresponds to command line option `-r, --report=VALUE` |
| [ReportFormat](Abstract.IPrepareOptions/ReportFormat-apidoc) { get; } | Corresponds to command line option `--reportFormat=VALUE` |
| [Save](Abstract.IPrepareOptions/Save-apidoc) { get; } | Corresponds to command line option `--save` |
| [ShowGenerated](Abstract.IPrepareOptions/ShowGenerated-apidoc) { get; } | Corresponds to command line option `--showGenerated` |
| [ShowStatic](Abstract.IPrepareOptions/ShowStatic-apidoc) { get; } | Corresponds to command line option `--showstatic[=VALUE]` |
| [SingleVisit](Abstract.IPrepareOptions/SingleVisit-apidoc) { get; } | Corresponds to command line option `--single` |
| [SourceLink](Abstract.IPrepareOptions/SourceLink-apidoc) { get; } | Corresponds to command line option `--sourcelink` |
| [StrongNameKey](Abstract.IPrepareOptions/StrongNameKey-apidoc) { get; } | Corresponds to command line option `--sn, --strongNameKey=VALUE` |
| [SymbolDirectories](Abstract.IPrepareOptions/SymbolDirectories-apidoc) { get; } | Corresponds to command line option `-y, --symbolDirectory=VALUE` |
| [TypeFilter](Abstract.IPrepareOptions/TypeFilter-apidoc) { get; } | Corresponds to command line option `-t, --typeFilter=VALUE` |
| [TypeTopLevel](Abstract.IPrepareOptions/TypeTopLevel-apidoc) { get; } | Corresponds to command line option `--typetoplevel=VALUE` |
| [Verbosity](Abstract.IPrepareOptions/Verbosity-apidoc) { get; } | Corresponds to command line option `-q` |
| [VisibleBranches](Abstract.IPrepareOptions/VisibleBranches-apidoc) { get; } | Corresponds to command line option `-v, --visibleBranches` |
| [ZipFile](Abstract.IPrepareOptions/ZipFile-apidoc) { get; } | Corresponds to command line option `--zipfile` |

## See Also

* class [Abstract](Abstract-apidoc)
* namespace [AltCover](../AltCover.Engine-apidoc)

<!-- DO NOT EDIT: generated by xmldocmd for AltCover.Engine.dll -->
