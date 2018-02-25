﻿namespace Tests.Runner

open System
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Reflection
open System.Text
open System.Threading
open System.Xml

open AltCover
open AltCover.Augment
open AltCover.Base
open Mono.Options
open NUnit.Framework

[<TestFixture>]
type AltCoverTests() = class

  // Base.fs

  [<Test>]
  member self.RealIdShouldIncrementCount() =
    let visits = new Dictionary<string, Dictionary<int, int>>()
    let key = " "
    Counter.AddVisit visits key  23
    Assert.That (visits.Count, Is.EqualTo 1)
    Assert.That (visits.[key].Count, Is.EqualTo 1)
    Assert.That (visits.[key].[23], Is.EqualTo 1)

  [<Test>]
  member self.DistinctIdShouldBeDistinct() =
    let visits = new Dictionary<string, Dictionary<int, int>>()
    let key = " "
    Counter.AddVisit visits key 23
    Counter.AddVisit visits "key" 42
    Assert.That (visits.Count, Is.EqualTo 2)

  [<Test>]
  member self.DistinctLineShouldBeDistinct() =
    let visits = new Dictionary<string, Dictionary<int, int>>()
    let key = " "
    Counter.AddVisit visits key 23
    Counter.AddVisit visits key 42
    Assert.That (visits.Count, Is.EqualTo 1)
    Assert.That (visits.[key].Count, Is.EqualTo 2)

  [<Test>]
  member self.RepeatVisitsShouldIncrementCount() =
    let visits = new Dictionary<string, Dictionary<int, int>>()
    let key = " "
    Counter.AddVisit visits key 23
    Counter.AddVisit visits key 23
    Assert.That (visits.[key].[23], Is.EqualTo 2)

  member self.resource = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                         |> Seq.find (fun n -> n.EndsWith("SimpleCoverage.xml", StringComparison.Ordinal))
   member self.resource2 = Assembly.GetExecutingAssembly().GetManifestResourceNames()
                          |> Seq.find (fun n -> n.EndsWith("Sample1WithOpenCover.xml", StringComparison.Ordinal))

  [<Test>]
  member self.KnownModuleWithPayloadMakesExpectedChangeInOpenCover() =
    Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource2)
    let size = int stream.Length
    let buffer = Array.create size 0uy
    Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
    use worker = new MemoryStream()
    worker.Write (buffer, 0, size)
    worker.Position <- 0L
    let payload = Dictionary<int,int>()
    [0..9 ]
    |> Seq.iter(fun i -> payload.[i] <- (i+1))
    let item = Dictionary<string, Dictionary<int, int>>()
    item.Add("7C-CD-66-29-A3-6C-6D-5F-A7-65-71-0E-22-7D-B2-61-B5-1F-65-9A", payload)
    Counter.UpdateReport true item ReportFormat.OpenCover worker |> ignore
    worker.Position <- 0L
    let after = XmlDocument()
    after.Load worker
    Assert.That( after.SelectNodes("//SequencePoint")
                 |> Seq.cast<XmlElement>
                 |> Seq.map (fun x -> x.GetAttribute("vc")),
                 Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1"])

  [<Test>]
  member self.FlushLeavesExpectedTraces() =
    let saved = Console.Out
    let here = Directory.GetCurrentDirectory()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    let reportFile = Path.Combine(unique, "FlushLeavesExpectedTraces.xml")
    try
      let visits = new Dictionary<string, Dictionary<int, int>>()
      use stdout = new StringWriter()
      Console.SetOut stdout
      Directory.CreateDirectory(unique) |> ignore
      Directory.SetCurrentDirectory(unique)

      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      do
        use worker = new FileStream(reportFile, FileMode.CreateNew)
        worker.Write(buffer, 0, size)
        ()

      let payload = Dictionary<int,int>()
      [0..9 ]
      |> Seq.iter(fun i -> payload.[i] <- (i+1))
      visits.["f6e3edb3-fb20-44b3-817d-f69d1a22fc2f"] <- payload

      Counter.DoFlush true visits AltCover.Base.ReportFormat.NCover reportFile |> ignore

      use worker' = new FileStream(reportFile, FileMode.Open)
      let after = XmlDocument()
      after.Load worker'
      Assert.That( after.SelectNodes("//seqpnt")
                   |> Seq.cast<XmlElement>
                   |> Seq.map (fun x -> x.GetAttribute("visitcount")),
                   Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1"])
    finally
      if File.Exists reportFile then File.Delete reportFile
      Console.SetOut saved
      Directory.SetCurrentDirectory(here)
      try
        Directory.Delete(unique)
      with
      | :? IOException -> ()

  // Runner.fs and CommandLine.fs

  [<Test>]
  member self.UsageIsAsExpected() =
    let options = Runner.DeclareOptions ()
    let saved = Console.Error

    try
      use stderr = new StringWriter()
      Console.SetError stderr
      let empty = OptionSet()
      CommandLine.Usage "UsageError" empty options
      let result = stderr.ToString().Replace("\r\n", "\n")
      let expected = """Error - usage is:
or
  Runner
  -r, --recorderDirectory=VALUE
                             The folder containing the instrumented code to
                               monitor (including the AltCover.Recorder.g.dll
                               generated by previous a use of the .net core
                               AltCover).
  -w, --workingDirectory=VALUE
                             Optional: The working directory for the
                               application launch
  -x, --executable=VALUE     The executable to run e.g. dotnet
  -?, --help, -h             Prints out the options.
"""

      Assert.That (result, Is.EqualTo (expected.Replace("\r\n", "\n")), "*" + result + "*")

    finally Console.SetError saved

  [<Test>]
  member self.ShouldLaunchWithExpectedOutput() =
    // Hack for running while instrumented
    let where = Assembly.GetExecutingAssembly().Location
    let path = Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "_Mono/Sample1")
#if NETCOREAPP2_0
    let path' = if Directory.Exists path then path
                else Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "../_Mono/Sample1")
#else
    let path' = path
#endif
    let files = Directory.GetFiles(path')
    let program = files
                  |> Seq.filter (fun x -> x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                  |> Seq.head

    let saved = (Console.Out, Console.Error)
    try
      use stdout = new StringWriter()
      use stderr = new StringWriter()
      Console.SetOut stdout
      Console.SetError stderr

      let nonWindows = System.Environment.GetEnvironmentVariable("OS") <> "Windows_NT"
      let exe, args = if nonWindows then ("mono", program) else (program, String.Empty)
      let r = CommandLine.Launch exe args (Path.GetDirectoryName (Assembly.GetExecutingAssembly().Location))
      Assert.That (r, Is.EqualTo 0)

      Assert.That(stderr.ToString(), Is.Empty)
      let result = stdout.ToString()
      // hack for Mono
      let computed = if result.Length = 14 then
                       result |> Encoding.Unicode.GetBytes |> Array.takeWhile (fun c -> c <> 0uy)|> Encoding.UTF8.GetString
                     else result

      if "TRAVIS_JOB_NUMBER" |> Environment.GetEnvironmentVariable |> String.IsNullOrWhiteSpace || result.Length > 0 then
        Assert.That(computed.Trim(), Is.EqualTo("Where is my rocket pack?"))
    finally
      Console.SetOut (fst saved)
      Console.SetError (snd saved)

  [<Test>]
  member self.ShouldHaveExpectedOptions() =
    let options = Runner.DeclareOptions ()
    Assert.That (options.Count, Is.EqualTo 5)
    Assert.That(options |> Seq.filter (fun x -> x.Prototype <> "<>")
                        |> Seq.forall (fun x -> (String.IsNullOrWhiteSpace >> not) x.Description))
    Assert.That (options |> Seq.filter (fun x -> x.Prototype = "<>") |> Seq.length, Is.EqualTo 1)

  [<Test>]
  member self.ParsingJunkIsAnError() =
    let options = Runner.DeclareOptions ()
    let parse = CommandLine.ParseCommandLine [| "/@thisIsNotAnOption" |] options
    match parse with
    | Right _ -> Assert.Fail()
    | Left (x, y) -> Assert.That (x, Is.EqualTo "UsageError")
                     Assert.That (y, Is.SameAs options)

  [<Test>]
  member self.ParsingJunkAfterSeparatorIsExpected() =
    let options = Runner.DeclareOptions ()
    let input = [| "--";  "/@thisIsNotAnOption"; "this should be OK" |]
    let parse = CommandLine.ParseCommandLine input options
    match parse with
    | Left _ -> Assert.Fail()
    | Right (x, y) -> Assert.That (x, Is.EquivalentTo (input |> Seq.skip 1))
                      Assert.That (y, Is.SameAs options)

  [<Test>]
  member self.ParsingHelpGivesHelp() =
    let options = Runner.DeclareOptions ()
    let input = [| "--?" |]
    let parse = CommandLine.ParseCommandLine input options
    match parse with
    | Left _ -> Assert.Fail()
    | Right (x, y) -> Assert.That (y, Is.SameAs options)

    match CommandLine.ProcessHelpOption parse with
    | Right _ -> Assert.Fail()
    | Left (x, y) -> Assert.That (x, Is.EqualTo "HelpText")
                     Assert.That (y, Is.SameAs options)

    // a "not sticky" test
    lock Runner.executable (fun () ->
      Runner.executable := None
      match CommandLine.ParseCommandLine [| "/x"; "x" |] options
            |> CommandLine.ProcessHelpOption with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty))

  [<Test>]
  member self.ParsingErrorHelpGivesHelp() =
    let options = Runner.DeclareOptions ()
    let input = [| "--o"; Path.GetInvalidPathChars() |> String |]
    let parse = CommandLine.ParseCommandLine input options
    match parse with
    | Right _ -> Assert.Fail()
    | Left (x, y) -> Assert.That (x, Is.EqualTo "UsageError")
                     Assert.That (y, Is.SameAs options)

    match CommandLine.ProcessHelpOption parse with
    | Right _ -> Assert.Fail()
    | Left (x, y) -> Assert.That (x, Is.EqualTo "UsageError")
                     Assert.That (y, Is.SameAs options)

    // a "not sticky" test
    lock Runner.executable (fun () ->
      Runner.executable := None
      match CommandLine.ParseCommandLine [| "/x"; "x" |] options
            |> CommandLine.ProcessHelpOption with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty))

  [<Test>]
  member self.ParsingExeGivesExe() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      let options = Runner.DeclareOptions ()
      let unique = "some exe"
      let input = [| "-x"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty)

      match !Runner.executable with
      | None -> Assert.Fail()
      | Some x -> Assert.That(Path.GetFileName x, Is.EqualTo unique)
    finally
      Runner.executable := None)

  [<Test>]
  member self.ParsingMultipleExeGivesFailure() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      let options = Runner.DeclareOptions ()
      let unique = Guid.NewGuid().ToString()
      let input = [| "-x"; unique; "/x"; unique.Replace("-", "+") |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.executable := None)

  [<Test>]
  member self.ParsingNoExeGivesFailure() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      let options = Runner.DeclareOptions ()
      let blank = " "
      let input = [| "-x"; blank; |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.executable := None)

  [<Test>]
  member self.ParsingWorkerGivesWorker() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let unique = Path.GetFullPath(".")
      let input = [| "-w"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty)

      match Runner.workingDirectory with
      | None -> Assert.Fail()
      | Some x -> Assert.That(x, Is.EqualTo unique)
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ParsingMultipleWorkerGivesFailure() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = [| "-w"; Path.GetFullPath("."); "/w"; Path.GetFullPath("..") |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ParsingBadWorkerGivesFailure() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let unique = Guid.NewGuid().ToString().Replace("-", "*")
      let input = [| "-w"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ParsingNoWorkerGivesFailure() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = [| "-w" |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ParsingRecorderGivesRecorder() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let unique = Path.GetFullPath(".")
      let input = [| "-r"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Left _ -> Assert.Fail()
      | Right (x, y) -> Assert.That (y, Is.SameAs options)
                        Assert.That (x, Is.Empty)

      match Runner.recordingDirectory with
      | None -> Assert.Fail()
      | Some x -> Assert.That(x, Is.EqualTo unique)
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ParsingMultipleRecorderGivesFailure() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = [| "-r"; Path.GetFullPath("."); "/r"; Path.GetFullPath("..") |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ParsingBadRecorderGivesFailure() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let unique = Guid.NewGuid().ToString().Replace("-", "*")
      let input = [| "-r"; unique |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ParsingNoRecorderGivesFailure() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = [| "-r" |]
      let parse = CommandLine.ParseCommandLine input options
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ShouldRequireExe() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := None
      let options = Runner.DeclareOptions ()
      let parse = Runner.RequireExe (Right ([], options))
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.executable := None)

  [<Test>]
  member self.ShouldAcceptExe() =
    lock Runner.executable (fun () ->
    try
      Runner.executable := Some "xxx"
      let options = Runner.DeclareOptions ()
      let parse = Runner.RequireExe (Right (["b"], options))
      match parse with
      | Right (x::y, z) -> Assert.That (z, Is.SameAs options)
                           Assert.That (x, Is.EqualTo "xxx")
                           Assert.That (y, Is.EquivalentTo ["b"])
      | _ -> Assert.Fail()
    finally
      Runner.executable := None)

  [<Test>]
  member self.ShouldRequireWorker() =
    try
      Runner.workingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireWorker input
      match parse with
      | Right _ -> Assert.That(parse, Is.SameAs input)
                   Assert.That(Option.isSome Runner.workingDirectory)
      | _-> Assert.Fail()
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ShouldAcceptWorker() =
    try
      Runner.workingDirectory <- Some "ShouldAcceptWorker"
      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireWorker input
      match parse with
      | Right _ -> Assert.That(parse, Is.SameAs input)
                   Assert.That(Runner.workingDirectory,
                               Is.EqualTo (Some "ShouldAcceptWorker"))
      | _-> Assert.Fail()
    finally
      Runner.workingDirectory <- None

  [<Test>]
  member self.ShouldRequireRecorder() =
    try
      Runner.recordingDirectory <- None
      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireRecorder input
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ShouldRequireRecorderDll() =
    try
      let where = Assembly.GetExecutingAssembly().Location
      let path = Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "_Mono/Sample1")
      let path' = if Directory.Exists path then path
                  else Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "../_Mono/Sample1")
      Runner.recordingDirectory <- Some path'
      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireRecorder input
      match parse with
      | Right _ -> Assert.Fail()
      | Left (x, y) -> Assert.That (y, Is.SameAs options)
                       Assert.That (x, Is.EqualTo "UsageError")
    finally
      Runner.recordingDirectory <- None
  [<Test>]
  member self.ShouldAcceptRecorder() =
    try
      let here = (Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName)
      let where = Path.Combine(here, Guid.NewGuid().ToString())
      Directory.CreateDirectory(where) |> ignore
      Runner.recordingDirectory <- Some where
      let create = Path.Combine(where, "AltCover.Recorder.g.dll")
      if create |> File.Exists |> not then do
        let from = Path.Combine(here, "AltCover.Recorder.dll")
        use frombytes = new FileStream(from, FileMode.Open, FileAccess.Read)
        use libstream = new FileStream(create, FileMode.Create)
        frombytes.CopyTo libstream

      let options = Runner.DeclareOptions ()
      let input = (Right ([], options))
      let parse = Runner.RequireRecorder input
      match parse with
      | Right _ -> Assert.That(parse, Is.SameAs input)
      | _-> Assert.Fail()
    finally
      Runner.recordingDirectory <- None

  [<Test>]
  member self.ShouldProcessTrailingArguments() =
    // Hack for running while instrumented
    let where = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let path = Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "_Mono/Sample1")
#if NETCOREAPP2_0
    let path' = if Directory.Exists path then path
                else Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "../_Mono/Sample1")
#else
    let path' = path
#endif
    let files = Directory.GetFiles(path')
    let program = files
                  |> Seq.filter (fun x -> x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                  |> Seq.head

    let saved = (Console.Out, Console.Error)
    try
      use stdout = new StringWriter()
      use stderr = new StringWriter()
      Console.SetOut stdout
      Console.SetError stderr

      let u1 = Guid.NewGuid().ToString()
      let u2 = Guid.NewGuid().ToString()

      let baseArgs= [program; u1; u2]
      let nonWindows = System.Environment.GetEnvironmentVariable("OS") <> "Windows_NT"
      let args = if nonWindows then "mono" :: baseArgs else baseArgs

      let r = CommandLine.ProcessTrailingArguments args <| DirectoryInfo(where)
      Assert.That(r, Is.EqualTo 0)

      Assert.That(stderr.ToString(), Is.Empty)
      stdout.Flush()
      let result = stdout.ToString()

      // hack for Mono
      let computed = if result.Length = 50 then
                       result |> Encoding.Unicode.GetBytes |> Array.takeWhile (fun c -> c <> 0uy)|> Encoding.UTF8.GetString
                     else result
      if "TRAVIS_JOB_NUMBER" |> Environment.GetEnvironmentVariable |> String.IsNullOrWhiteSpace || result.Length > 0 then
        Assert.That(computed.Trim(), Is.EqualTo("Where is my rocket pack? " +
                                                  u1 + "*" + u2))
    finally
      Console.SetOut (fst saved)
      Console.SetError (snd saved)

  [<Test>]
  member self.ShouldNoOp() =
    let where = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let r = CommandLine.ProcessTrailingArguments [] <| DirectoryInfo(where)
    Assert.That(r, Is.EqualTo 0)

  [<Test>]
  member self.ErrorResponseIsAsExpected() =
    let saved = Console.Error
    try
      use stderr = new StringWriter()
      Console.SetError stderr
      let unique = Guid.NewGuid().ToString()
      let main = typeof<Tracer>.Assembly.GetType("AltCover.Main").GetMethod("Main", BindingFlags.NonPublic ||| BindingFlags.Static)
      let returnCode = main.Invoke(null, [| [| "RuNN"; "-r"; unique |] |])
      Assert.That(returnCode, Is.EqualTo 255)
      let result = stderr.ToString().Replace("\r\n", "\n")
      let expected = "\"RuNN\" \"-r\" \"" + unique + "\"\n" +
                       """Error - usage is:
  -i, --inputDirectory=VALUE Optional: The folder containing assemblies to
                               instrument (default: current directory)
  -o, --outputDirectory=VALUE
                             Optional: The folder to receive the instrumented
                               assemblies and their companions (default: sub-
                               folder '__Instrumented' of the current directory)
"""
#if NETCOREAPP2_0
#else
                     + """  -k, --key=VALUE            Optional, multiple: any other strong-name key to
                               use
      --sn, --strongNameKey=VALUE
                             Optional: The default strong naming key to apply
                               to instrumented assemblies (default: None)
"""
#endif
                     + """  -x, --xmlReport=VALUE      Optional: The output report template file (default:
                                coverage.xml in the current directory)
  -f, --fileFilter=VALUE     Optional: source file name to exclude from
                               instrumentation (may repeat)
  -s, --assemblyFilter=VALUE Optional: assembly name to exclude from
                               instrumentation (may repeat)
  -e, --assemblyExcludeFilter=VALUE
                             Optional: assembly which links other instrumented
                               assemblies but for which internal details may be
                               excluded (may repeat)
  -t, --typeFilter=VALUE     Optional: type name to exclude from
                               instrumentation (may repeat)
  -m, --methodFilter=VALUE   Optional: method name to exclude from
                               instrumentation (may repeat)
  -a, --attributeFilter=VALUE
                             Optional: attribute name to exclude from
                               instrumentation (may repeat)
      --opencover            Optional: Generate the report in OpenCover format
  -?, --help, -h             Prints out the options.
or
  Runner
  -r, --recorderDirectory=VALUE
                             The folder containing the instrumented code to
                               monitor (including the AltCover.Recorder.g.dll
                               generated by previous a use of the .net core
                               AltCover).
  -w, --workingDirectory=VALUE
                             Optional: The working directory for the
                               application launch
  -x, --executable=VALUE     The executable to run e.g. dotnet
  -?, --help, -h             Prints out the options.
"""

      Assert.That (result, Is.EqualTo (expected.Replace("\r\n", "\n")))

    finally Console.SetError saved

  [<Test>]
  member self.ShouldGetStringConstants() =
    let where = Assembly.GetExecutingAssembly().Location
                |> Path.GetDirectoryName
    let save = Runner.RecorderName
    lock self (fun () ->
    try
      Runner.recordingDirectory <- Some where
      Runner.RecorderName <- "AltCover.Recorder.dll"
      let instance = Runner.RecorderInstance()
      Assert.That(instance.FullName, Is.EqualTo "AltCover.Recorder.Instance", "should be the instance")
      let token = (Runner.GetMethod instance "get_Token") |> Runner.GetFirstOperandAsString
      Assert.That(token, Is.EqualTo "AltCover", "should be plain token")
      let report = (Runner.GetMethod instance "get_ReportFile") |> Runner.GetFirstOperandAsString
      Assert.That(report, Is.EqualTo "Coverage.Default.xml", "should be default coverage file")

    finally
      Runner.recordingDirectory <- None
      Runner.RecorderName <- save)

  [<Test>]
  member self.ShouldProcessPayload() =
    // Hack for running while instrumented
    let where = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let path = Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "_Mono/Sample1")
#if NETCOREAPP2_0
    let path' = if Directory.Exists path then path
                else Path.Combine(where.Substring(0, where.IndexOf("_Binaries")), "../_Mono/Sample1")
#else
    let path' = path
#endif
    let files = Directory.GetFiles(path')
    let program = files
                  |> Seq.filter (fun x -> x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                  |> Seq.head

    let saved = (Console.Out, Console.Error)
    Runner.workingDirectory <- Some where
    try
      use stdout = new StringWriter()
      use stderr = new StringWriter()
      Console.SetOut stdout
      Console.SetError stderr

      let u1 = Guid.NewGuid().ToString()
      let u2 = Guid.NewGuid().ToString()
      use latch = new ManualResetEvent true

      let baseArgs= [program; u1; u2]
      let nonWindows = System.Environment.GetEnvironmentVariable("OS") <> "Windows_NT"
      let args = if nonWindows then "mono" :: baseArgs else baseArgs
      let r = Runner.GetPayload args
      Assert.That(r, Is.EqualTo 0)

      Assert.That(stderr.ToString(), Is.Empty)
      stdout.Flush()
      let result = stdout.ToString()

      // hack for Mono
      let computed = if result.Length = 50 then
                       result |> Encoding.Unicode.GetBytes |> Array.takeWhile (fun c -> c <> 0uy)|> Encoding.UTF8.GetString
                     else result
      if "TRAVIS_JOB_NUMBER" |> Environment.GetEnvironmentVariable |> String.IsNullOrWhiteSpace || result.Length > 0 then
        Assert.That(computed.Trim(), Is.EqualTo("Where is my rocket pack? " +
                                                  u1 + "*" + u2))
    finally
      Console.SetOut (fst saved)
      Console.SetError (snd saved)
      Runner.workingDirectory <- None

  [<Test>]
  member self.ShouldDoCoverage() =
    let start = Directory.GetCurrentDirectory()
    let here = (Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName)
    let where = Path.Combine(here, Guid.NewGuid().ToString())
    Directory.CreateDirectory(where) |> ignore
    Directory.SetCurrentDirectory where
    let create = Path.Combine(where, "AltCover.Recorder.g.dll")
    if create |> File.Exists |> not then do
        let from = Path.Combine(here, "AltCover.Recorder.dll")
        let updated = Instrument.PrepareAssembly from
        Instrument.WriteAssembly updated create

    let save = Runner.RecorderName
    let save1 = Runner.GetPayload
    let save2 = Runner.GetMonitor
    let save3 = Runner.DoReport

    let report =  "coverage.xml" |> Path.GetFullPath
    try
      Runner.RecorderName <- "AltCover.Recorder.g.dll"
      let payload (rest:string list) =
        Assert.That(rest, Is.EquivalentTo [|"test"; "1"|])
        255

      let monitor (hits:ICollection<(string*int)>) (token:string) _ _ =
        Assert.That(token, Is.EqualTo report, "should be default coverage file")
        Assert.That(hits, Is.Empty)
        127

      let write (hits:ICollection<(string*int)>) format (report:string) =
        Assert.That(report, Is.EqualTo report, "should be default coverage file")
        Assert.That(hits, Is.Empty)
        TimeSpan.Zero

      Runner.GetPayload <- payload
      Runner.GetMonitor <- monitor
      Runner.DoReport <- write

      let empty = OptionSet()
      let dummy = report + ".xx.bin"
      do
        use temp = File.Create dummy
        dummy |> File.Exists |> Assert.That

      let r = Runner.DoCoverage [|"Runner"; "-x"; "test"; "-r"; where; "--"; "1"|] empty
      dummy |> File.Exists |> not |> Assert.That
      Assert.That (r, Is.EqualTo 127)

    finally
      Runner.GetPayload <- save1
      Runner.GetMonitor <- save2
      Runner.DoReport <- save3
      Runner.RecorderName <- save
      Directory.SetCurrentDirectory start

  [<Test>]
  member self.WriteLeavesExpectedTraces() =
    let saved = Console.Out
    let here = Directory.GetCurrentDirectory()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    let reportFile = Path.Combine(unique, "FlushLeavesExpectedTraces.xml")
    try
      let visits = new Dictionary<string, Dictionary<int, int>>()
      use stdout = new StringWriter()
      Console.SetOut stdout
      Directory.CreateDirectory(unique) |> ignore
      Directory.SetCurrentDirectory(unique)

      Counter.measureTime <- DateTime.ParseExact("2017-12-29T16:33:40.9564026+00:00", "o", null)
      use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(self.resource)
      let size = int stream.Length
      let buffer = Array.create size 0uy
      Assert.That (stream.Read(buffer, 0, size), Is.EqualTo size)
      do
        use worker = new FileStream(reportFile, FileMode.CreateNew)
        worker.Write(buffer, 0, size)
        ()

      let hits = List<(string*int)>()
      [0..9 ]
      |> Seq.iter(fun i ->
        for j = 1 to i+1 do
          hits.Add("f6e3edb3-fb20-44b3-817d-f69d1a22fc2f", i)
          ignore j
      )

      let payload = Dictionary<int,int>()
      [0..9 ]
      |> Seq.iter(fun i -> payload.[i] <- (i+1))
      visits.["f6e3edb3-fb20-44b3-817d-f69d1a22fc2f"] <- payload

      Runner.DoReport hits AltCover.Base.ReportFormat.NCover reportFile |> ignore

      use worker' = new FileStream(reportFile, FileMode.Open)
      let after = XmlDocument()
      after.Load worker'
      Assert.That( after.SelectNodes("//seqpnt")
                   |> Seq.cast<XmlElement>
                   |> Seq.map (fun x -> x.GetAttribute("visitcount")),
                   Is.EquivalentTo [ "11"; "10"; "9"; "8"; "7"; "6"; "4"; "3"; "2"; "1"])
    finally
      if File.Exists reportFile then File.Delete reportFile
      Console.SetOut saved
      Directory.SetCurrentDirectory(here)
      try
        Directory.Delete(unique)
      with
      | :? IOException -> ()

  [<Test>]
  member self.NullPayloadShouldReportNothing() =
    let hits = List<string*int>()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    do
      use s = File.Create (unique + ".0.bin")
      s.Close()
    let r = Runner.GetMonitor hits unique List.length []
    Assert.That(r, Is.EqualTo 0)
    Assert.That (File.Exists (unique + ".bin"))
    Assert.That(hits, Is.Empty)

  [<Test>]
  member self.ActivePayloadShouldReportAsExpected() =
    let hits = List<string*int>()
    let where = Assembly.GetExecutingAssembly().Location |> Path.GetDirectoryName
    let unique = Path.Combine(where, Guid.NewGuid().ToString())
    let formatter = System.Runtime.Serialization.Formatters.Binary.BinaryFormatter()
    let r = Runner.GetMonitor hits unique (fun l ->
       use sink = new DeflateStream(File.OpenWrite (unique + ".0.bin"), CompressionMode.Compress)
       l |> List.mapi (fun i x -> formatter.Serialize(sink, (x,i)); x) |> List.length
                                           ) ["a"; "b"; String.Empty; "c"]
    Assert.That(r, Is.EqualTo 4)
    Assert.That (File.Exists (unique + ".bin"))
    Assert.That(hits, Is.EquivalentTo [("a",0); ("b",1)])

end