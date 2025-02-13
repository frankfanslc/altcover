﻿#r "paket:
nuget BlackFox.VsWhere >= 1.0.0
nuget Fake.Core.Target >= 5.21.0
nuget Fake.Core.Environment >= 5.21.0
nuget Fake.Core.Process >= 5.21.0
nuget Fake.DotNet.Cli >= 5.21.0
nuget Fake.DotNet.NuGet >= 5.21.0
nuget Fake.IO.FileSystem >= 5.21.0 //"

#r "System.Xml"
#r "System.Xml.Linq"

open System
open System.IO
open System.Xml.Linq

open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.DotNet.NuGet.Restore
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators

let consoleBefore = (Console.ForegroundColor, Console.BackgroundColor)

Shell.copyFile "./AltCover.Engine/Abstract.fsi" "./AltCover.Engine/Abstract.fs"

// Really bootstrap
let dotnetPath = "dotnet" |> ProcessUtils.tryFindFileOnPath

let dotnetOptions (o: DotNet.Options) =
    match dotnetPath with
    | Some f -> { o with DotNetCliPath = f }
    | None -> o

DotNet.restore
    (fun o ->
        { o with
            Packages = [ "./packages" ]
            Common = dotnetOptions o.Common })
    "./Build/NuGet.csproj"

let toolPackages =
    let xml =
        "./Build/NuGet.csproj"
        |> Path.getFullName
        |> XDocument.Load

    xml.Descendants(XName.Get("PackageReference"))
    |> Seq.filter (fun x -> x.Attribute(XName.Get("Include")) |> isNull |> not)
    |> Seq.map (fun x -> (x.Attribute(XName.Get("Include")).Value, x.Attribute(XName.Get("version")).Value))
    |> Map.ofSeq

let packageVersion (p: string) =
    p.ToLowerInvariant() + "/" + (toolPackages.Item p)

let nuget =
    ("./packages/"
     + (packageVersion "NuGet.CommandLine")
     + "/tools/NuGet.exe")
    |> Path.getFullName

let dixon =
    ("./packages/"
     + (packageVersion "AltCode.Dixon")
     + "/tools")
    |> Path.getFullName

let fxcop =
    if Environment.isWindows then
        BlackFox.VsWhere.VsInstances.getAll ()
        |> Seq.filter (fun i -> System.Version(i.InstallationVersion).Major = 17)
        |> Seq.map (fun i ->
            i.InstallationPath
            @@ "Team Tools/Static Analysis Tools/FxCop")
        |> Seq.filter Directory.Exists
        |> Seq.tryHead
    else
        None

let restore (o: RestorePackageParams) = { o with ToolPath = nuget }

let build =
    """// generated by dotnet fake run .\Build\setup.fsx
// includes by source all of AltCoverFake.DotNet.Testing.AltCover
#r "paket:
nuget Fake.Core.Target >= 5.21.0
nuget Fake.Core.Environment >= 5.21.0
nuget Fake.Core.Process >= 5.21.0
nuget Fake.DotNet.AssemblyInfoFile >= 5.21.0
nuget Fake.DotNet.Cli >= 5.21.0
nuget Fake.DotNet.FxCop >= 5.21.0
nuget Fake.DotNet.MSBuild >= 5.21.0
nuget Fake.DotNet.NuGet >= 5.21.0
nuget Fake.DotNet.Testing.NUnit >= 5.21.0
nuget Fake.DotNet.Testing.OpenCover >= 5.21.0
nuget Fake.DotNet.Testing.XUnit2 >= 5.21.0
nuget Fake.IO.FileSystem >= 5.21.0
nuget Fake.DotNet.Testing.Coverlet >= 5.21.0
nuget Fake.Testing.ReportGenerator >= 5.21.0
nuget Fake.Tools.Git >= 5.21.0
nuget AltCode.Fake.DotNet.Gendarme >= 5.18.1.24
nuget BlackFox.CommandLine >= 1.0.0
nuget BlackFox.VsWhere >= 1.1.0
nuget Markdig >= 0.24.0
nuget Manatee.Json >= 13.0.4
nuget NUnit >= 3.12.0
nuget Fuchu >= 1.1.0
nuget System.Text.Encoding.CodePages >= 5.0.0
nuget Unquote >= 5.0.0
nuget YamlDotNet >= 8.1.2 //"
#r "System.IO.Compression.FileSystem.dll"
#r "System.Xml"
#r "System.Xml.Linq"
#load "../AltCover.Engine/StringExtension.fs"
#load "../AltCover.Engine/Canonical.fs"
#load "../AltCover.Engine/NativeJson.fs"
#load "../AltCover.Engine/Abstract.fs"
#load "../AltCover.Engine/Primitive.fs"
#load "../AltCover.Engine/TypeSafe.fs"
#load "../AltCover.Engine/AltCover.fs"
#load "../AltCover.Engine/Args.fs"
#load "../AltCover.Fake.DotNet.Testing.AltCover/AltCoverCommand.fs"
#load "../AltCover.DotNet/DotNet.fs"
#load "../AltCover.Fake/Fake.fs"
#load "actions.fsx"
#load "targets.fsx"
#nowarn "988"

do ()"""

File.WriteAllText("./Build/build.fsx", build)

let _Target s f =
    Target.description s
    Target.create s f

let resetColours _ =
    Console.ForegroundColor <- consoleBefore |> fst
    Console.BackgroundColor <- consoleBefore |> snd

Target.description "ResetConsoleColours"
Target.createFinal "ResetConsoleColours" resetColours
Target.activateFinal "ResetConsoleColours"

_Target "FxCop" (fun _ ->
    fxcop
    |> Option.iter (fun fx ->
        Directory.ensure "./packages/fxcop/"
        let target = Path.getFullName "./packages/fxcop/"
        let prefix = fx.Length

        let check t pf (f: string) =
            let destination = t @@ (f.Substring pf)
            // printfn "%A" destination
            destination |> File.Exists |> not
            && destination |> Path.GetFileName
               <> "SecurityTransparencyRules.dll"

        Shell.copyDir target fx (check target prefix)

        Shell.copyDir target dixon (fun f ->
            Path.GetFileNameWithoutExtension f
            <> "AltCode.Dixon")

        let config = XDocument.Load "./packages/fxcop/FxCopCmd.exe.config"
        // Maybe process here...
        config.Save "./packages/fxcop/DixonCmd.exe.config"))

_Target "Preparation" ignore

let defaultTarget () =
    resetColours ()
    "Preparation"

"FxCop" =?> ("Preparation", Environment.isWindows)

Target.runOrDefault <| defaultTarget ()