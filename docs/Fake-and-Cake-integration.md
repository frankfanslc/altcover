# F# Fake and Cake integration v7.x and up

APIs for use with build scripting tools are provided in the `AltCover.Cake.dll` and `AltCover.Fake.dll` assemblies, which are present in the `AltCover.Api` nuget package

* [Fake integration](#fake-integration)
* [Cake integration](#cake-integration)

# Fake integration 
Found in `AltCover.Fake.dll`  
Detailed API documentation is [presented here](AltCover.Fake/Fake-fsapidoc).

## To use the Fake `dotnet test` API `Fake.DotNet.DotNet.test`
Driving `dotnet test` in a Fake script 

In the project(s) to be covered, insert at least

```
    <PackageReference Include="altcover.api" Version="<whatever>">
      <IncludeAssets>build;</IncludeAssets>
    </PackageReference>
```

with the relevant script fragment (based on [the AltCover build script here](https://github.com/SteveGilham/altcover/blob/9b12b5b27f2877fcde186c1d8c08f6335108e306/Build/targets.fsx#L3425-L3454))

```
#r "paket:
nuget Fake.DotNet.Cli >= 5.20.4
nuget AltCover.Api >= 8.2.833 //"

let ForceTrue = AltCover.DotNet.CLIOptions.Force true 

let p =
  { AltCover.Primitive.PrepareOptions.Create() with
      CallContext = [| "[Fact]"; "0" |]
      AssemblyFilter = [| "xunit" |] }

let prepare = AltCover.AltCover.PrepareOptions.Primitive p
let c = AltCover.Primitive.CollectOptions.Create()
let collect = AltCover.AltCover.CollectOptions.Primitive c

open AltCover.Fake.DotNet // extension method WithAltCoverOptions
Fake.DotNet.DotNet.test
  (fun to' -> to'.WithAltCoverOptions prepare collect ForceTrue)
  "dotnettest.fsproj"

```

# Cake integration 

Applies to Cake 1.1 and up (with obsolescence warnings if used with Cake 2.0 or later)

Found in `AltCover.Cake.dll`  
Detailed API documentation is [presented here](AltCover.Cake/AltCover.Cake-apidoc).

## To use the Cake `dotnet test` API `DotNetCoreTest`

In the project(s) to be covered, insert at least

```
    <PackageReference Include="altcover.api" Version="<whatever>">
      <IncludeAssets>build;</IncludeAssets>
    </PackageReference>
```

In your `.cake` file include

```
#addin "nuget:?package=Microsoft.TestPlatform.ObjectModel&Version=16.1.1"
#addin "nuget:?package=PowerShellStandard.Library&Version=5.1.0"
#addin "nuget:?package=altcover.api&Version=<whatever>"

```
the first two needed to silence warnings.

Implement the needed interfaces ([as documented here](AltCover.Engine/AltCover/Abstract-apidoc)) e.g. by copying and pasting this for the minimal example
```
  class TestOptions : AltCover.DotNet.ICLIOptions
  {
    public bool ForceDelete => false;
    public bool FailFast => false;
    public string ShowSummary => String.Empty;
  }

  class TestPrepareOptions : AltCover.Abstract.IPrepareOptions
  {
    public IEnumerable<string> InputDirectories => throw new NotImplementedException("InputDirectories not used");
    public IEnumerable<string> OutputDirectories => throw new NotImplementedException("OutputDirectories not used");
    public IEnumerable<string> SymbolDirectories => Array.Empty<string>();
    public IEnumerable<string> Dependencies => Array.Empty<string>();
    public IEnumerable<string> Keys => Array.Empty<string>();
    public string StrongNameKey => String.Empty;
    public string Report => String.Empty;
    public IEnumerable<string> FileFilter => Array.Empty<string>();
    public IEnumerable<string> AssemblyFilter => Array.Empty<string>();
    public IEnumerable<string> AssemblyExcludeFilter => Array.Empty<string>();
    public IEnumerable<string> TypeFilter => Array.Empty<string>();
    public IEnumerable<string> MethodFilter => Array.Empty<string>();
    public IEnumerable<string> AttributeFilter => Array.Empty<string>();
    public IEnumerable<string> PathFilter => Array.Empty<string>();
    public IEnumerable<string> AttributeTopLevel => Array.Empty<string>();
    public IEnumerable<string> TypeTopLevel => Array.Empty<string>();
    public IEnumerable<string> MethodTopLevel => Array.Empty<string>();
    public IEnumerable<string> CallContext => Array.Empty<string>();
    public string ReportFormat => String.Empty;
    public bool InPlace => false;
    public bool Save => false;
    public bool ZipFile => false;
    public bool MethodPoint => false;
    public bool SingleVisit => false;
    public bool LineCover => false;
    public bool BranchCover => false;
    public IEnumerable<string> CommandLine => throw new NotImplementedException("CommandLine not used");
    public bool ExposeReturnCode => throw new NotImplementedException("ExposeReturnCode not used");
    public bool SourceLink => true;
    public bool Defer => throw new NotImplementedException("Defer not used");
    public bool LocalSource => true;
    public bool VisibleBranches => false;
    public string ShowStatic => String.Empty;
    public bool ShowGenerated => false;
    public System.Diagnostics.TraceLevel Verbosity => System.Diagnostics.TraceLevel.Verbose;
  }

  class TestCollectOptions : AltCover.Abstract.ICollectOptions
  {
    public string RecorderDirectory => throw new NotImplementedException("RecorderDirectory not used");
    public string WorkingDirectory => throw new NotImplementedException("WorkingDirectory not used");
    public string Executable => throw new NotImplementedException("Executable not used");
    public string LcovReport => String.Empty;
    public string Threshold => String.Empty;
    public string Cobertura => String.Empty;
    public string OutputFile => String.Empty;
    public IEnumerable<string> CommandLine => throw new NotImplementedException("CommandLine not used");
    public bool ExposeReturnCode => throw new NotImplementedException("ExposeReturnCode not used");
    public string SummaryFormat => String.Empty;
    public System.Diagnostics.TraceLevel Verbosity => System.Diagnostics.TraceLevel.Verbose;
  }
```
changing fixed values to `{ get; set; }` as required; then your test-with-coverage phase looks like
```
{
    // do any required customizations here
    var altcoverSettings = new AltCover.Cake.CoverageSettings {
        PreparationPhase = new TestPrepareOptions(),
        CollectionPhase = new TestCollectOptions(),
        Options = new TestOptions()
    };

    var testSettings = new DotNetCoreTestSettings {
        Configuration = configuration,
        NoBuild = true,
    };

    DotNetCoreTest(<project to test>,
                      testSettings, altcoverSettings);
});

```

## To use the Cake2.x `dotnet test` API `DotNetTest`

There isn't a one-stop-shop Cake 2.x alias; instead make the following changes after defining the `CoverageSettings` value (inlining the effect of the alias used for the `DotNetCoreTest` API) :

```
    // use the Cake 2.0 test settings type
    var testSettings = new DotNetTestSettings {
        Configuration = configuration,
        NoBuild = true,
    };

    // mix-in the AltCover coverage settings explicitly
    testSettings.ArgumentCustomization = altcoverSettings.Concatenate(testSettings.ArgumentCustomization);

    // test using the default alias
    DotNetTest("./_DotnetTest/cake_dotnettest.fsproj", testSettings);

```

As the `AltCover.Cake` assembly still targets Cake 1.1 and netstandard2.0 there will still be warnings like
```
The assembly 'AltCover.Cake, Version=8.2.0.0, Culture=neutral, PublicKeyToken=null'
is referencing an older version of Cake.Core (1.1.0).
For best compatibility it should target Cake.Core version 2.0.0.
```
but there will be no warnings about obsolescent types or methods being used.