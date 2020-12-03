namespace AltCover

open System
open System.Collections.Generic
open System.IO

open Mono.Cecil
open Mono.Cecil.Cil
open Mono.Cecil.Mdb
open Mono.Cecil.Pdb

module internal HashTrace =
  let mutable internal trace:(string -> unit) = ignore
  let hash = System.Security.Cryptography.SHA256.Create()

  let hashtext (s:String) =
    s
    |> System.Text.Encoding.UTF8.GetBytes
    |> hash.ComputeHash
    |> Convert.ToBase64String

  let format (s : string) =
    if s |> isNull
    then "(null)"
    else if String.IsNullOrEmpty s
         then "(empty)"
         else if String.IsNullOrWhiteSpace s
              then "(whitespace)"
              else hashtext s

  let formatFilePath (s:string) =
    let directory = try
                      Path.GetDirectoryName s
                    with
                    | x -> x.Message
                           |> sprintf "failed to get directory with %A"
                           |> trace
                           String.Empty
    let file =      try
                      Path.GetFileNameWithoutExtension s
                    with
                    | x -> x.Message
                           |> sprintf "failed to get file name with %A"
                           |> trace
                           String.Empty
    let extension = try
                      Path.GetExtension s
                    with
                    | x -> x.Message
                           |> sprintf "failed to get file extension with %A"
                           |> trace
                           String.Empty

    sprintf "directory = %A filename = %A extension = %A"
             (hashtext directory) (hashtext file) extension

  let formatFileName (s:string) =
    let file =      try
                      Path.GetFileNameWithoutExtension s
                    with
                    | x -> x.Message
                           |> sprintf "failed to get file name with %A"
                           |> trace
                           String.Empty
    let extension = try
                      Path.GetExtension s
                    with
                    | x -> x.Message
                           |> sprintf "failed to get file extension with %A"
                           |> trace
                           String.Empty

    sprintf "filename = %A extension = %A"
             (hashtext file) extension

[<RequireQualifiedAccess>]
module internal ProgramDatabase =
  // "Public" "field"
  let internal symbolFolders = List<String>()

  // Implementation details
  module private I =

    // We no longer have to violate Cecil encapsulation to get the PDB path
    // but we do to get the embedded PDB info
    let internal getEmbed =
      (typeof<Mono.Cecil.AssemblyDefinition>.Assembly.GetTypes()
       |> Seq.filter (fun m -> m.FullName = "Mono.Cecil.Mixin")
       |> Seq.head).GetMethod("GetEmbeddedPortablePdbEntry")

    let internal getEmbeddedPortablePdbEntry(assembly : AssemblyDefinition) =
      getEmbed.Invoke(null, [| assembly.MainModule.GetDebugHeader() :> obj |]) :?> ImageDebugHeaderEntry

    let internal getSymbolsByFolder fileName folderName =
      sprintf "getSymbolsByFolder %s with directory %s or %A" (HashTrace.formatFileName fileName) folderName (HashTrace.hashtext folderName)
      |> HashTrace.trace

      let name = Path.Combine(folderName, fileName)
      let fallback = Path.ChangeExtension(name, ".pdb")
      if File.Exists(fallback) then
        Some fallback
      else
        let fallback2 = name + ".mdb"
        // Note -- the assembly path, not the mdb path, because GetSymbolReader wants the assembly path for Mono
        if File.Exists(fallback2) then Some name else None

  // "Public" API
  let internal getPdbFromImage(assembly : AssemblyDefinition) =
    Some assembly.MainModule
    |> Option.filter (fun x -> x.HasDebugHeader)
    |> Option.map (fun x -> x.GetDebugHeader())
    |> Option.filter (fun x -> x.HasEntries)
    |> Option.bind (fun x -> x.Entries |> Seq.tryFind (fun t -> true))
    |> Option.map (fun x -> x.Data)
    |> Option.filter (fun x -> x.Length > 0x18)
    |> Option.map (fun x ->
          x
          |> Seq.skip 0x18 // size of the debug header
          |> Seq.takeWhile (fun x -> x <> byte 0)
          |> Seq.toArray
          |> System.Text.Encoding.UTF8.GetString)
    |> Option.filter (fun s -> s.Length > 0)
    |> Option.filter (fun s ->
          s
          |> HashTrace.formatFilePath
          |> (sprintf "getPdbFromImage %s or %s" s)
          |> HashTrace.trace

          assembly.Name.Name
          |> HashTrace.hashtext
          |> (sprintf "Emedded symbols would be %s.pdb or %s.pdb" assembly.Name.Name)
          |> HashTrace.trace

          File.Exists s || (s = (assembly.Name.Name + ".pdb") && (assembly
                                                                  |> I.getEmbeddedPortablePdbEntry).IsNotNull))

  let internal getPdbWithFallback(assembly : AssemblyDefinition) =
    let n = assembly.MainModule.FileName
    sprintf "getPdbPath %s or %s" n (HashTrace.formatFilePath n)
    |> HashTrace.trace
    match getPdbFromImage assembly with
    | None ->
        sprintf "getPdbWithFallback (fallback) %s or %s" n (HashTrace.formatFilePath n)
        |> HashTrace.trace

        let foldername = Path.GetDirectoryName assembly.MainModule.FileName
        let filename = Path.GetFileName assembly.MainModule.FileName
        foldername :: (Seq.toList symbolFolders)
        |> Seq.map (I.getSymbolsByFolder filename)
        |> Seq.choose id
        |> Seq.tryFind (fun _ -> true)
    | pdbpath ->
        let n = assembly.MainModule.FileName
        sprintf "getPdbWithFallback (from debug header) %s or %s" n (HashTrace.formatFilePath n)
        |> HashTrace.trace
        pdbpath

  // Ensure that we read symbols from the .pdb path we discovered.
  // Cecil currently only does the Path.ChangeExtension(path, ".pdb") fallback if left to its own devices
  // Will fail  with InvalidOperationException if there is a malformed file with the expected name
  let internal readSymbols(assembly : AssemblyDefinition) =
    getPdbWithFallback assembly
    |> Option.iter (fun pdbpath ->
          let provider : ISymbolReaderProvider =
            if pdbpath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
            then PdbReaderProvider() :> ISymbolReaderProvider
            else MdbReaderProvider() :> ISymbolReaderProvider

          let reader = provider.GetSymbolReader(assembly.MainModule, pdbpath)
          assembly.MainModule.ReadSymbols(reader))