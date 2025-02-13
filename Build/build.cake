#addin "nuget:?package=Microsoft.TestPlatform.ObjectModel&Version=16.1.1"
#addin "nuget:?package=PowerShellStandard.Library&Version=5.1.0"
#addin "nuget:file://{0}?package=altcover.api&Version={1}"

var target = Argument("target", "Test");
var configuration = Argument("configuration", "Debug");
var cakeversion = Argument("cakeversion", "Unknown");

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .WithCriteria(c => HasArgument("rebuild"))
    .Does(() =>
{
    CleanDirectory($"../_Binaries/cake_dotnettest/{configuration}+AnyCPU");
});

Task("Build")
    .IsDependentOn("Clean")
    .Does(() =>
{
#if CAKE_2
    DotNetBuild("./_DotnetTest/cake_dotnettest.fsproj", new DotNetBuildSettings
    {
        Configuration = configuration,
    });
#else
    DotNetCoreBuild("./_DotnetTest/cake_dotnettest.fsproj", new DotNetCoreBuildSettings
    {
        Configuration = configuration,
    });
#endif
});

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
    public string Report { get; set;}
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
    public IEnumerable<string> CallContext => new string[] {"[Fact]", "0"};
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

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
{
    using AltCover.Cake;

    var testv = Version();
    var im = ImportModule();
    Console.WriteLine("Version = {1} Import = '{2}'", null, testv, im);

    var prep = new TestPrepareOptions();


#if CAKE_2
    prep.Report = "coverage.build2.cake." + cakeversion +".xml";
#else
    prep.Report = "coverage.build.cake." + cakeversion +".xml";
#endif

    var altcoverSettings = new CoverageSettings {
        PreparationPhase = prep,
        CollectionPhase = new TestCollectOptions(),
        Options = new TestOptions()
    };

#if CAKE_2
    var testSettings = new DotNetTestSettings {
        Configuration = configuration,
        NoBuild = true,
    };
    
    testSettings.ArgumentCustomization = altcoverSettings.Concatenate(testSettings.ArgumentCustomization);
    DotNetTest("./_DotnetTest/cake_dotnettest.fsproj", testSettings);

#else
    var testSettings = new DotNetCoreTestSettings {
        Configuration = configuration,
        NoBuild = true,
    };

    DotNetCoreTest("./_DotnetTest/cake_dotnettest.fsproj", 
                      testSettings, altcoverSettings);
#endif
});


//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);