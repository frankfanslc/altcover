﻿namespace AltCover

open System
open System.Collections.Generic
open System.Diagnostics.CodeAnalysis
open System.IO
open System.Xml.Linq

#if RUNNER
open System.Text
open Mono.Cecil
#endif

#if GUI || RUNNER
open System.Globalization
open Manatee.Json
#endif

module
#if GUI || RUNNER
       internal
#endif
                NativeJson =

  type internal TimeStamp = string

#if !GUI
  let internal fromTracking (ticks: int64) : TimeStamp =
    ticks
    |> System.Net.IPAddress.HostToNetworkOrder
    |> BitConverter.GetBytes
    |> Convert.ToBase64String
#endif

  type internal Times = List<TimeStamp>

  type internal Tracks = List<int>

  [<ExcludeFromCodeCoverage; NoComparison>]
  [<SuppressMessage("Gendarme.Rules.Design.Generic",
                    "DoNotExposeGenericListsRule",
                    Justification = "Harmless in context")>]
  type
#if GUI || RUNNER
      internal
#endif
                SeqPnt =
    { VC: int
      SL: int
      SC: int
      EL: int
      EC: int
      Offset: int
      Id: int
      [<SuppressMessage("Gendarme.Rules.Design.Generic",
                        "DoNotExposeGenericListsRule",
                        Justification = "Harmless in context")>]
      Times: Times
      [<SuppressMessage("Gendarme.Rules.Design.Generic",
                        "DoNotExposeGenericListsRule",
                        Justification = "Harmless in context")>]
      Tracks: Tracks }

  type internal SeqPnts = List<SeqPnt>

  // Coverlet compatible -- src/coverlet.core/CoverageResult.cs
  // also round-trippable
  [<ExcludeFromCodeCoverage; NoComparison>]
  [<SuppressMessage("Gendarme.Rules.Design.Generic",
                    "DoNotExposeGenericListsRule",
                    Justification = "Harmless in context")>]
  type
#if GUI || RUNNER
      internal
#endif
                BranchInfo =
    { Line: int
      Offset: int
      EndOffset: int
      Path: int
      Ordinal: uint
      Hits: int
      // scope to expand
      Id: int
      [<SuppressMessage("Gendarme.Rules.Design.Generic",
                        "DoNotExposeGenericListsRule",
                        Justification = "Harmless in context")>]
      Times: Times
      [<SuppressMessage("Gendarme.Rules.Design.Generic",
                        "DoNotExposeGenericListsRule",
                        Justification = "Harmless in context")>]
      Tracks: Tracks }

  type internal Lines = SortedDictionary<int, int>

  type internal Branches = List<BranchInfo>

  [<ExcludeFromCodeCoverage; NoComparison>]
  [<SuppressMessage("Gendarme.Rules.Design.Generic",
                    "DoNotExposeGenericListsRule",
                    Justification = "Harmless in context")>]
  type
#if GUI || RUNNER
      internal
#endif
                Method =
    { Lines: Lines
      [<SuppressMessage("Gendarme.Rules.Design.Generic",
                        "DoNotExposeGenericListsRule",
                        Justification = "Harmless in context")>]
      Branches: Branches
      // scope to expand
      [<SuppressMessage("Gendarme.Rules.Design.Generic",
                        "DoNotExposeGenericListsRule",
                        Justification = "Harmless in context")>]
      SeqPnts: SeqPnts
      TId: Nullable<int>
      Entry: Times
      Exit: Times }
#if !GUI
    static member Create(track: (int * string) option) =
      { Lines = Lines()
        Branches = Branches()
        SeqPnts = SeqPnts()
        TId = track |> Option.map fst |> Option.toNullable
        Entry = if track.IsNone then null else Times()
        Exit = if track.IsNone then null else Times() }
#endif

  type internal Methods = Dictionary<string, Method>
  type internal Classes = Dictionary<string, Methods>
  type internal Documents = SortedDictionary<string, Classes>
  type internal Modules = SortedDictionary<string, Documents> // <= serialize this

#if RUNNER || GUI
  // Deserialization ---------------------------------------------------------

  let internal timesFromJsonValue (j: JsonValue) =
    j.Array |> Seq.map (fun a -> a.String) |> Times

  let internal tracksFromJsonValue (j: JsonValue) =
    j.Array
    |> Seq.map (fun a -> a.Number |> Math.Round |> int)
    |> Tracks

  let internal zero = JsonValue(0.0)

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let internal softFromKey (fallback: JsonValue) (o: JsonObject) (key: string) =
    let b, i = o.TryGetValue key
    if b then i else fallback

  let internal softNumberFromKey (o: JsonObject) (key: string) =
    (softFromKey zero o key).Number
    |> Math.Round
    |> int

  let internal softValueFromKey (o: JsonObject) (key: string) =
    softFromKey JsonValue.Null o key

  let internal valueFromKey (o: JsonObject) (key: string) fallback decoder =
    let t = softValueFromKey o key

    if t = JsonValue.Null then
      fallback
    else
      decoder t

  let internal timesByKey (o: JsonObject) =
    valueFromKey o "Times" null timesFromJsonValue

  let internal tracksByKey (o: JsonObject) =
    valueFromKey o "Tracks" null tracksFromJsonValue

  let internal seqpntFromJsonValue (j: JsonValue) =
    let o = j.Object

    {
      // extract
      VC = (softNumberFromKey o "VC")
      SL = (softNumberFromKey o "SL")
      SC = (softNumberFromKey o "SC")
      EL = (softNumberFromKey o "EL")
      EC = (softNumberFromKey o "EC")
      Offset = (softNumberFromKey o "Offset")
      Id = (softNumberFromKey o "Id")
      Times = timesByKey o
      Tracks = tracksByKey o }

  let internal seqpntsFromJsonValue (j: JsonValue) =
    j.Array |> Seq.map seqpntFromJsonValue |> SeqPnts

  let internal branchinfoFromJsonValue (j: JsonValue) =
    let o = j.Object

    { // extract
      Line = (softNumberFromKey o "Line")
      Offset = (softNumberFromKey o "Offset")
      EndOffset = (softNumberFromKey o "EndOffset")
      Path = (softNumberFromKey o "Path")
      Ordinal = (softNumberFromKey o "Ordinal") |> uint
      Hits = (softNumberFromKey o "Hits")
      // Optionals
      Id = valueFromKey o "Id" 0 (fun t -> t.Number |> Math.Round |> int)
      Times = timesByKey o
      Tracks = tracksByKey o }

  let internal linesFromJsonValue (j: JsonValue) =
    let result = Lines()

    j.Object
    |> Seq.iter
         (fun kvp ->
           let _, i = Int32.TryParse kvp.Key

           if i > 0 then
             result.[i] <- kvp.Value.Number |> Math.Round |> int)

    result

  let internal branchesFromJsonValue (j: JsonValue) =
    j.Array
    |> Seq.map branchinfoFromJsonValue
    |> Branches

  let internal methodFromJsonValue (j: JsonValue) =
    let o = j.Object

    let tid =
      valueFromKey
        o
        "TId"
        (System.Nullable())
        (fun t -> t.Number |> Math.Round |> int |> Nullable<int>)

    { Lines = valueFromKey o "Lines" (Lines()) linesFromJsonValue
      Branches = valueFromKey o "Branches" (Branches()) branchesFromJsonValue
      // Optionals
      SeqPnts = valueFromKey o "SeqPnts" null seqpntsFromJsonValue
      TId = tid
      Entry =
        valueFromKey o "Entry" (if tid.HasValue then Times() else null) timesFromJsonValue
      Exit =
        valueFromKey o "Exit" (if tid.HasValue then Times() else null) timesFromJsonValue }

  let internal methodsFromJsonValue (j: JsonValue) =
    let result = Methods()

    j.Object
    |> Seq.iter (fun kvp -> result.[kvp.Key] <- kvp.Value |> methodFromJsonValue)

    result

  let internal classesFromJsonValue (j: JsonValue) =
    let result = Classes()

    j.Object
    |> Seq.iter (fun kvp -> result.[kvp.Key] <- kvp.Value |> methodsFromJsonValue)

    result

  let internal documentsFromJsonValue (j: JsonValue) =
    let result = Documents()

    j.Object
    |> Seq.iter (fun kvp -> result.[kvp.Key] <- kvp.Value |> classesFromJsonValue)

    result

  let internal modulesFromJsonValue (j: JsonValue) =
    let result = Modules()

    j.Object
    |> Seq.iter (fun kvp -> result.[kvp.Key] <- kvp.Value |> documentsFromJsonValue)

    result

  let internal fromJsonText (report: string) =
    report
    |> Manatee.Json.JsonValue.Parse
    |> modulesFromJsonValue
#endif

#if RUNNER
  // Serialization ---------------------------------------------------------

  [<SuppressMessage("Gendarme.Rules.Performance",
                    "AvoidReturningArraysOnPropertiesRule",
                    Justification = "Indexing required")>]
  let private allowed =
    [|
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        0uy
        1uy
        1uy
        0uy
        1uy
        1uy
        1uy
        0uy
        0uy
        1uy
        1uy
        1uy
        0uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        0uy
        1uy
        0uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        0uy
        1uy
        1uy
        1uy
        0uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        1uy
        0uy
  |]

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let private escapeString (builder: StringBuilder) (s: String) =
    s
    |> Seq.iter (fun c ->
      match c with
      | '"' -> builder.Append("\\\"")
      | '\\' -> builder.Append("\\\\")
      | '\b' -> builder.Append("\\b")
      | '\f' -> builder.Append("\\f")
      | '\n' -> builder.Append("\\n")
      | '\r' -> builder.Append("\\r")
      | '\t' -> builder.Append("\\t")
      | h when (int h) >= 128 || Array.get allowed (int h) = 0uy ->
        builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture))
      | _ -> builder.Append(c)
      |> ignore )

  let private slugs =
    { 0 .. 14 }
    |> Seq.map (fun i -> (i, String(' ', i)))
    |> Map.ofSeq

  let private dictionaryToBuilder<'a>
    (depth: int)
    (next: StringBuilder -> 'a -> StringBuilder)
    (w: StringBuilder)
    (report: IDictionary<string, 'a>)
    =
    let mutable first = true

    report
    |> Seq.iter
         (fun kvp ->
           if not first then
             ("," |> w.AppendLine |> ignore)

           first <- false

           w.Append(slugs.[depth]).Append('"')
           |> ignore

           escapeString w kvp.Key
           w.AppendLine("\": {") |> ignore

           (next w kvp.Value)
             .Append(slugs.[depth + 1])
             .Append('}')
           |> ignore)

    w.AppendLine() |> ignore
    w

  [<SuppressMessage("Gendarme.Rules.Smells",
                    "AvoidMessageChainsRule",
                    Justification = "Fluent interface")>]
  let private lineToBuilder (w: StringBuilder) (kvp: KeyValuePair<int, int>) =
    w
      .Append(slugs.[11])
      .Append('"')
      .Append(kvp.Key.ToString(CultureInfo.InvariantCulture))
      .Append("\": ")
      .Append(kvp.Value.ToString(CultureInfo.InvariantCulture))

  [<SuppressMessage("Gendarme.Rules.Smells",
                    "AvoidMessageChainsRule",
                    Justification = "Fluent interface")>]
  let private itemToBuilder (w: StringBuilder) (i: int) (n: string) more =
    w
      .Append(slugs.[12])
      .Append('"')
      .Append(n)
      .Append("\": ")
      .Append(i.ToString(CultureInfo.InvariantCulture))
    |> ignore

    if more then
      w.AppendLine(",") |> ignore

  let private timeToBuilder (b: StringBuilder) depth (time: TimeStamp) =
    b
      .Append(slugs.[depth])
      .Append('"')
      .Append(time)
      .Append('"')
    |> ignore

  let private timesToBuilder (w: StringBuilder) (times: Times) =
    if times.IsNotNull && times.Count > 0 then
      w
        .AppendLine(",")
        .Append(slugs.[12])
        .Append("\"Times\": [")
      |> ignore

      let mutable firstTime = true

      times
      |> Seq.iter
           (fun t ->
             timeToBuilder
               (if firstTime then
                  firstTime <- false
                  w.AppendLine()
                else
                  w.AppendLine(","))
               14
               t)

      w
        .AppendLine()
        .Append(slugs.[13])
        .Append("]")
      |> ignore

  let private tracksToBuilder (w: StringBuilder) (tracks: Tracks) =
    if tracks.IsNotNull && tracks.Count > 0 then
      w
        .AppendLine(",")
        .Append(slugs.[12])
        .Append("\"Tracks\": [")
      |> ignore

      let mutable firstTime = true

      tracks
      |> Seq.iter
           (fun t ->
             (if firstTime then
                firstTime <- false
                w.AppendLine()
              else
                w.AppendLine(","))
               .Append(slugs.[14])
               .Append(t.ToString(CultureInfo.InvariantCulture))
             |> ignore)

      w
        .AppendLine()
        .Append(slugs.[13])
        .Append("]")
      |> ignore

  let private branchToBuilder (w: StringBuilder) (b: BranchInfo) =
    w.Append(slugs.[11]).AppendLine("{")
    |> ignore

    itemToBuilder w b.Line "Line" true
    itemToBuilder w b.Offset "Offset" true
    itemToBuilder w b.EndOffset "EndOffset" true
    itemToBuilder w b.Path "Path" true
    itemToBuilder w (int b.Ordinal) "Ordinal" true
    itemToBuilder w b.Hits "Hits" (b.Id > 0)

    if b.Id > 0 then
      itemToBuilder w b.Id "Id" false

    timesToBuilder w b.Times
    tracksToBuilder w b.Tracks

    w
      .AppendLine()
      .Append(slugs.[11])
      .Append("}")
    |> ignore

  let private seqpntToBuilder (w: StringBuilder) (s: SeqPnt) =
    w.Append(slugs.[11]).AppendLine("{")
    |> ignore

    itemToBuilder w s.VC "VC" true
    itemToBuilder w s.SL "SL" true
    itemToBuilder w s.SC "SC" true
    itemToBuilder w s.EL "EL" true
    itemToBuilder w s.EC "EC" true
    itemToBuilder w s.Offset "Offset" true
    itemToBuilder w s.Id "Id" false
    timesToBuilder w s.Times
    tracksToBuilder w s.Tracks

    w
      .AppendLine()
      .Append(slugs.[11])
      .Append("}")
    |> ignore

  [<SuppressMessage("Gendarme.Rules.Smells",
                    "AvoidMessageChainsRule",
                    Justification = "Fluent interface")>]
  let private methodToBuilder (w: StringBuilder) (method: Method) =
    w
      .Append(slugs.[9])
      .AppendLine("\"Lines\": {")
    |> ignore

    if method.Lines.IsNotNull && method.Lines.Count > 0 then
      let mutable first = true

      method.Lines // TODO extract
      |> Seq.iter
           (fun kvp ->
             if not first then
               w.AppendLine(",") |> ignore

             first <- false
             lineToBuilder w kvp |> ignore)

      w.AppendLine().Append(slugs.[10])
      |> ignore

    w.AppendLine("},") |> ignore

    // After Lines, now Branches

    w
      .Append(slugs.[9])
      .Append("\"Branches\": [")
    |> ignore

    if method.Branches.IsNotNull
       && method.Branches.Count > 0 then
      let mutable first = true
      w.AppendLine() |> ignore

      method.Branches // TODO extract
      |> Seq.iter
           (fun b ->
             if not first then
               w.AppendLine(",") |> ignore

             first <- false
             branchToBuilder w b)

      w
        .AppendLine()
        .Append(slugs.[10])
        .Append("]")
      |> ignore
    else
      w.Append(']') |> ignore

    // After Branches, now SeqPnts

    if method.SeqPnts.IsNotNull
       && method.SeqPnts.Count > 0 then
      w
        .AppendLine(",")
        .Append(slugs.[9])
        .AppendLine("\"SeqPnts\": [")
      |> ignore

      let mutable first = true

      method.SeqPnts
      |> Seq.iter
           (fun s ->
             if not first then
               w.AppendLine(",") |> ignore

             first <- false
             seqpntToBuilder w s)

      w
        .AppendLine()
        .Append(slugs.[10])
        .Append("]")
      |> ignore

    // After SeqPnts, now Tracking

    if method.TId.HasValue then
      w
        .AppendLine(",")
        .Append(slugs.[9])
        .Append("\"TId\": ")
        .Append(method.TId.Value.ToString(CultureInfo.InvariantCulture))
      |> ignore

      w
        .AppendLine(",")
        .Append(slugs.[9])
        .Append("\"Entry\": [")
      |> ignore

      let mutable firstTime = true

      if method.Entry.IsNotNull && method.Entry.Count > 0 then
        method.Entry
        |> Seq.iter
             (fun t ->
               timeToBuilder
                 (if firstTime then
                    firstTime <- false
                    w.AppendLine()
                  else
                    w.AppendLine(","))
                 11
                 t)

        w.AppendLine().Append(slugs.[10])
        |> ignore

      w.Append("]") |> ignore

      w
        .AppendLine(",")
        .Append(slugs.[9])
        .Append("\"Exit\": [")
      |> ignore

      let mutable firstTime = true

      if method.Exit.IsNotNull && method.Exit.Count > 0 then
        method.Exit
        |> Seq.iter
             (fun t ->
               timeToBuilder
                 (if firstTime then
                    firstTime <- false
                    w.AppendLine()
                  else
                    w.AppendLine(","))
                 11
                 t)

        w.AppendLine().Append(slugs.[10])
        |> ignore

      w.AppendLine("]") |> ignore
    else
      w.AppendLine() |> ignore

    w

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let private methodsToBuilder (w: StringBuilder) (methods: Methods) =
    (dictionaryToBuilder 7 methodToBuilder w methods)

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let private classesToBuilder (w: StringBuilder) (classes: Classes) =
    (dictionaryToBuilder 5 methodsToBuilder w classes)

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let private documentsToBuilder (w: StringBuilder) (documents: Documents) =
    (dictionaryToBuilder 3 classesToBuilder w documents)

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let private modulesToBuilder (w: StringBuilder) (report: Modules) =
    (dictionaryToBuilder 1 documentsToBuilder w report)

  let internal toText (report: Modules) =
    let w =StringBuilder()
    w.AppendLine("{") |> ignore

    (modulesToBuilder w report).AppendLine("}")
    |> ignore

    let result = w.ToString()
    result

  let internal serializeToUtf8Bytes (document: Modules) =
    document
    |> toText
    |> System.Text.Encoding.UTF8.GetBytes

#endif

#if GUI || RUNNER
  // Conversion to XML ---------------------------------------------------------

  let internal buildSummary (m: XContainer) =
    let zero name = XAttribute(XName.Get name, 0)

    let sd =
      XElement(
        XName.Get "Summary",
        zero "numBranchPoints",
        zero "visitedBranchPoints",
        zero "numSequencePoints",
        zero "visitedSequencePoints"
      )

    m.Add sd
    sd

  let internal buildMethodElement name fileId =
    let m =
      XElement(
        XName.Get "Method",
        XAttribute(XName.Get "visited", false),
        XAttribute(XName.Get "cyclomaticComplexity", 1),
        XAttribute(XName.Get "sequenceCoverage", 0),
        XAttribute(XName.Get "branchCoverage", 0),
        XAttribute(XName.Get "isConstructor", false),
        XAttribute(XName.Get "isStatic", false),
        XAttribute(XName.Get "isGetter", false),
        XAttribute(XName.Get "isSetter", false)
      )

    let sd = buildSummary m

    [ "MetadataToken", "0"; "Name", name ]
    |> Seq.iter
         (fun (name, value) ->
           let x = XElement(XName.Get name)
           x.Value <- value
           m.Add x)

    let f =
      XElement(XName.Get "FileRef", XAttribute(XName.Get "uid", fileId))

    m.Add f
    (m, sd)

  let internal makeSummary (nb: int) (vb: int) (ns: int) (vs: int) (sd: XElement) =
    sd.Attribute(XName.Get "numBranchPoints").Value <- nb.ToString(
      CultureInfo.InvariantCulture
    )

    sd.Attribute(XName.Get "visitedBranchPoints").Value <- vb.ToString(
      CultureInfo.InvariantCulture
    )

    sd.Attribute(XName.Get "numSequencePoints").Value <- ns.ToString(
      CultureInfo.InvariantCulture
    )

    sd.Attribute(XName.Get "visitedSequencePoints").Value <- vs.ToString(
      CultureInfo.InvariantCulture
    )

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let internal methodToXml
    (fileId: int)
    (item: XElement)
    (method: KeyValuePair<string, Method>)
    =
    let m, sd = buildMethodElement method.Key fileId
    item.Add m

    let sp = XElement(XName.Get "SequencePoints")
    m.Add sp
    let bp = XElement(XName.Get "BranchPoints")
    m.Add bp
    let value = method.Value

    let bec = Dictionary<int, int>()
    let bev = Dictionary<int, int>()
    let mutable nb = 0
    let mutable vb = 0

    if value.Branches.IsNotNull then
      value.Branches
      |> Seq.iter
           (fun b ->
             let _, old = bec.TryGetValue b.Line
             bec.[b.Line] <- old + 1
             nb <- nb + 1

             if b.Hits > 0 then
               vb <- vb + 1
               let _, old = bev.TryGetValue b.Line
               bev.[b.Line] <- old + 1

             let bx =
               XElement(
                 XName.Get "BranchPoint",
                 XAttribute(XName.Get "vc", b.Hits),
                 XAttribute(XName.Get "sl", b.Line),
                 XAttribute(XName.Get "uspid", b.Id),
                 XAttribute(XName.Get "ordinal", b.Ordinal),
                 XAttribute(XName.Get "offset", b.Offset),
                 XAttribute(XName.Get "path", b.Path)
               )

             bp.Add bx)

      let targets =
        value.Branches
        |> Seq.groupBy (fun b -> b.Line)
        |> Seq.sumBy
             (fun (_, x) ->
               x
               |> Seq.distinctBy (fun bx -> bx.EndOffset)
               |> Seq.length)

      m.Attribute(XName.Get "cyclomaticComplexity").Value <- (1 + targets)
        .ToString(CultureInfo.InvariantCulture)

    let mutable mvc = 0
    let mutable ns = 0
    let mutable vs = 0

    if value.SeqPnts.IsNotNull then
      value.SeqPnts
      |> Seq.iter
           (fun s ->
             let _, bec2 = bec.TryGetValue s.SL
             bec.[s.SL] <- 0
             let _, bev2 = bev.TryGetValue s.SL
             bev.[s.SL] <- 0
             ns <- ns + 1
             if s.VC > 0 then vs <- vs + 1

             let sx =
               XElement(
                 XName.Get "SequencePoint",
                 XAttribute(XName.Get "vc", s.VC),
                 XAttribute(XName.Get "offset", s.Offset),
                 XAttribute(XName.Get "sl", s.SL),
                 XAttribute(XName.Get "sc", s.SC),
                 XAttribute(XName.Get "el", s.EL),
                 XAttribute(XName.Get "ec", s.EC),
                 XAttribute(XName.Get "uspid", s.Id),
                 XAttribute(XName.Get "bec", bec2),
                 XAttribute(XName.Get "bev", bev2)
               )

             sp.Add sx
             mvc <- Math.Max(mvc, s.VC))
    else
      value.Lines
      |> Seq.iteri
           (fun i l ->
             let k = l.Key
             let _, bec2 = bec.TryGetValue k
             bec.[k] <- 0
             let _, bev2 = bev.TryGetValue k
             bev.[k] <- 0
             ns <- ns + 1
             if l.Value > 0 then vs <- vs + 1

             let sx =
               XElement(
                 XName.Get "SequencePoint",
                 XAttribute(XName.Get "vc", l.Value),
                 XAttribute(XName.Get "offset", i),
                 XAttribute(XName.Get "sl", k),
                 XAttribute(XName.Get "sc", 1),
                 XAttribute(XName.Get "el", k),
                 XAttribute(XName.Get "ec", 2),
                 XAttribute(XName.Get "uspid", i),
                 XAttribute(XName.Get "bec", bec2),
                 XAttribute(XName.Get "bev", bev2)
               )

             sp.Add sx
             mvc <- Math.Max(mvc, l.Value))

    let mp =
      XElement(XName.Get "MethodPoint", XAttribute(XName.Get "vc", mvc))

    m.Add mp
    makeSummary nb vb ns vs sd

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let internal methodsToXml (fileId: int) (item: XElement) (methods: Methods) =
    methods |> Seq.iter (methodToXml fileId item)

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let internal tryGetValueOrDefault
    (table: Dictionary<string, 'a>)
    (key: string)
    (f: unit -> 'a)
    =
    let ok, index = table.TryGetValue key

    if ok then
      index
    else
      let value = f ()
      table.Add(key, value)
      value

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let internal classesToXml
    (fileId: int)
    (table: Dictionary<string, XElement>)
    (classes: Classes)
    =
    classes
    |> Seq.iteri
         (fun i kvp ->
           let name = kvp.Key

           let item =
             tryGetValueOrDefault
               table
               name
               (fun () ->
                 XElement(
                   XName.Get "Class",
                   XElement(XName.Get "FullName", name),
                   XElement(XName.Get "Methods")
                 ))

           let next =
             item.Elements(XName.Get "Methods") |> Seq.head

           methodsToXml fileId next kvp.Value)

  let private valueOf (x: XElement) (name: string) =
    x.Attribute(XName.Get name).Value
    |> Int32.TryParse
    |> snd

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let internal summarize sd (m: XElement) name =
    let (nb, vb, ns, vs) =
      m.Descendants(XName.Get name)
      |> Seq.collect (fun m2 -> m2.Elements(XName.Get "Summary"))
      |> Seq.fold
           (fun (bn, bv, sn, sv) ms ->
             (bn + valueOf ms "numBranchPoints",
              bv + valueOf ms "visitedBranchPoints",
              sn + valueOf ms "numSequencePoints",
              sv + valueOf ms "visitedSequencePoints"))
           (0, 0, 0, 0)

    makeSummary nb vb ns vs sd

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let internal documentsToXml
    (indexTable: Dictionary<string, int>)
    (key: string)
    (documents: Documents)
    =
    let m =
      XElement(XName.Get "Module", XAttribute(XName.Get "hash", key))

    let sd = buildSummary m

    [ "ModulePath", key
      "ModuleName", (key |> Path.GetFileNameWithoutExtension) ]
    |> Seq.iter
         (fun (name, value) ->
           let x = XElement(XName.Get name)
           x.Value <- value
           m.Add x)

    let files = XElement(XName.Get "Files")
    m.Add files
    let classes = XElement(XName.Get "Classes")
    m.Add classes
    let classTable = Dictionary<string, XElement>()

    documents
    |> Seq.iter
         (fun kvp ->
           let name = kvp.Key

           let i =
             tryGetValueOrDefault indexTable name (fun () -> 1 + indexTable.Count)

           let item =
             XElement(
               XName.Get "File",
               XAttribute(XName.Get "uid", i),
               XAttribute(XName.Get "fullPath", name)
             )

           files.Add item
           classesToXml i classTable kvp.Value)

    classTable
    |> Seq.iter (fun kvp -> classes.Add kvp.Value)

    summarize sd m "Method"
    m

  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  [<SuppressMessage("Gendarme.Rules.Exceptions",
                    "InstantiateArgumentExceptionCorrectlyRule",
                    Justification = "Library method inlined")>]
  [<SuppressMessage("Microsoft.Usage",
                    "CA2208:InstantiateArgumentExceptionsCorrectly",
                    Justification = "Library method inlined")>]
  let internal jsonToXml (modules: Modules) =
    let x = XDocument()
    x.Add(XElement(XName.Get "CoverageSession"))
    let root = x.Root
    let sd = buildSummary root
    let mroot = XElement(XName.Get "Modules")
    root.Add mroot
    let fileRefs = Dictionary<string, int>()

    modules
    |> Seq.iter
         (fun kvp ->
           documentsToXml fileRefs kvp.Key kvp.Value
           |> mroot.Add)

    summarize sd root "Module"

    let mcc =
      x.Descendants(XName.Get "Method")
      |> Seq.fold
           (fun top x ->
             let value = valueOf x "cyclomaticComplexity"
             if value > top then value else top)
           1

    sd.Add(
      XAttribute(
        XName.Get "maxCyclomaticComplexity",
        mcc.ToString(CultureInfo.InvariantCulture)
      )
    )

    x

#if RUNNER
  [<SuppressMessage("Gendarme.Rules.Maintainability",
                    "AvoidUnnecessarySpecializationRule",
                    Justification = "AvoidSpeculativeGenerality too")>]
  let internal orderXml (x: XDocument) =
    x.Descendants(XName.Get "SequencePoints")
    |> Seq.iter
         (fun sps ->
           let original = sps.Elements() |> Seq.toList
           sps.RemoveAll()

           original
           |> Seq.sortBy
                (fun sp ->
                  let sl =
                    sp.Attribute(XName.Get "sl").Value
                    |> Int32.TryParse
                    |> snd

                  let sc =
                    sp.Attribute(XName.Get "sc").Value
                    |> Int32.TryParse
                    |> snd

                  (sl <<< 16) + sc)
           |> sps.Add)

    x.Descendants(XName.Get "BranchPoints")
    |> Seq.iter
         (fun bps ->
           let original = bps.Elements() |> Seq.toList
           bps.RemoveAll()

           original
           |> Seq.sortBy
                (fun bp ->
                  let sl =
                    bp.Attribute(XName.Get "sl").Value
                    |> Int32.TryParse
                    |> snd

                  let offset =
                    bp.Attribute(XName.Get "ordinal").Value
                    |> Int32.TryParse
                    |> snd

                  (sl <<< 16) + offset)
           |> bps.Add)

    x
#endif

  let internal fileToJson filename =
    filename |> File.ReadAllText |> fromJsonText

#endif

#if RUNNER
  // Instrumentation ---------------------------------------------------------

  [<ExcludeFromCodeCoverage; NoComparison; AutoSerializable(false)>]
  type internal JsonContext =
    { Documents: Documents
      Type: TypeDefinition
      VisibleType: TypeDefinition
      Method: MethodDefinition
      VisibleMethod: MethodDefinition
      Track: (int * string) option }
    static member Build() =
      { Documents = null
        Type = null
        VisibleType = null
        Method = null
        VisibleMethod = null
        Track = None }

  let internal reportGenerator () =
    let document = Modules()

    let startVisit = id

    let visitModule s (m: ModuleEntry) =
      let documents = Documents()
      document.Add(m.Module.FileName |> Path.GetFileName, documents)
      { s with Documents = documents }

    let visitType (s: JsonContext) (m: TypeEntry) =
      { s with
          Type = m.Type
          VisibleType = m.VisibleType }

    let visitMethod (s: JsonContext) (m: MethodEntry) =
      { s with
          Method = m.Method
          VisibleMethod = m.VisibleMethod
          Track = m.Track }

    let getMethodRecord (s: JsonContext) (doc: string) =
      let visibleMethodName = s.VisibleMethod.FullName
      let visibleTypeName = s.VisibleMethod.DeclaringType.FullName

      let classes =
        match s.Documents.TryGetValue doc with
        | true, c -> c
        | _ ->
            let c = Classes()
            s.Documents.Add(doc, c)
            c

      let methods =
        match classes.TryGetValue visibleTypeName with
        | true, m -> m
        | _ ->
            let m = Methods()
            classes.Add(visibleTypeName, m)
            m

      match methods.TryGetValue visibleMethodName with
      | true, m -> m
      | _ ->
          let m = Method.Create(s.Track)
          methods.Add(visibleMethodName, m)
          m

    let visitMethodPoint (s: JsonContext) (e: StatementEntry) =
      if e.Interesting then
        e.SeqPnt
        |> Option.iter
             (fun codeSegment ->
               let doc =
                 codeSegment.Document |> Visitor.sourceLinkMapping

               let mplus = getMethodRecord s doc
               mplus.Lines.[codeSegment.StartLine] <- int e.DefaultVisitCount

               mplus.SeqPnts.Add
                 { VC = int e.DefaultVisitCount
                   SL = codeSegment.StartLine
                   SC = codeSegment.StartColumn
                   EL = codeSegment.EndLine
                   EC = codeSegment.EndColumn
                   Offset = codeSegment.Offset
                   Id = e.Uid
                   Times = null
                   Tracks = null })

      s

    let visitBranchPoint (s: JsonContext) (b: GoTo) =
      if b.Included then
        let doc =
          b.SequencePoint.Document.Url
          |> Visitor.sourceLinkMapping

        let mplus = getMethodRecord s doc

        mplus.Branches.Add
          { Line = b.SequencePoint.StartLine
            Offset = b.Offset
            EndOffset = b.Target.Head.Offset
            Path =
              mplus.Branches
              |> Seq.filter (fun k -> k.Offset = b.Offset)
              |> Seq.length
            Ordinal = uint mplus.Branches.Count
            Hits = int b.VisitCount
            // scope to expand
            Id = b.Uid
            Times = null
            Tracks = null }

      s

    let visitAfterMethod (s: JsonContext) _ =
      { s with
          Method = null
          VisibleMethod = null
          Track = None }

    let visitAfterType (s: JsonContext) =
      { s with
          Type = null
          VisibleType = null }

    let visitAfterModule s = { s with Documents = null }
    //    let afterAll = id

    let reportVisitor (s: JsonContext) (node: Node) =
      match node with
      | Start _ -> startVisit s
      | Node.Module m -> visitModule s m
      | Node.Type t -> visitType s t
      | Node.Method m -> visitMethod s m
      | MethodPoint m -> visitMethodPoint s m
      | BranchPoint b -> visitBranchPoint s b
      | AfterMethod m -> visitAfterMethod s m
      | AfterType _ -> visitAfterType s
      | AfterModule _ -> visitAfterModule s
      //      | Finish -> afterAll s
      | _ -> s

    let result =
      Visitor.encloseState reportVisitor (JsonContext.Build())

    (result,
     fun (s: System.IO.Stream) ->
       let encoded = serializeToUtf8Bytes document
       s.Write(encoded, 0, encoded.Length))

[<AutoSerializable(false)>]
type internal DocumentType =
  | XML of XDocument
  | JSON of NativeJson.Modules
  | Unknown
  static member internal LoadReport format report =
    if File.Exists report then
      if format = ReportFormat.NativeJson
         || format = ReportFormat.NativeJsonWithTracking then
        report |> NativeJson.fileToJson |> JSON
      else
        report |> XDocument.Load |> XML
    else
      Unknown
#endif

#if GUI || RUNNER
// FxCop ---------------------------------------------------------
#if GUI
[<assembly: SuppressMessage("Microsoft.Performance",
                            "CA1810:InitializeReferenceTypeStaticFieldsInline",
                            Scope = "member",
                            Target = "<StartupCode$AltCover-UICommon>.$NativeJson.#.cctor()",
                            Justification = "Compiler Generated")>]
#endif

[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#.ctor(System.Collections.Generic.SortedDictionary`2<System.Int32,System.Int32>,System.Collections.Generic.List`1<AltCover.NativeJson+BranchInfo>,System.Collections.Generic.List`1<AltCover.NativeJson+SeqPnt>,Microsoft.FSharp.Core.FSharpOption`1<System.Int32>)",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#Branches",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#SeqPnts",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+BranchInfo.#.ctor(System.Int32,System.Int32,System.Int32,System.Int32,System.UInt32,System.Int32,System.Int32,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.Int32>)",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+BranchInfo.#Times",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+BranchInfo.#Tracks",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#.ctor(System.Collections.Generic.SortedDictionary`2<System.Int32,System.Int32>,System.Collections.Generic.List`1<AltCover.NativeJson+BranchInfo>,System.Collections.Generic.List`1<AltCover.NativeJson+SeqPnt>,System.Nullable`1<System.Int32>,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.String>)",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+SeqPnt.#.ctor(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.Int32>)",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+SeqPnt.#Times",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+SeqPnt.#Tracks",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#Entry",
                            Justification = "Harmless in context")>]
[<assembly: SuppressMessage("Microsoft.Design",
                            "CA1002:DoNotExposeGenericLists",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#Exit",
                            Justification = "Harmless in context")>]

[<assembly: SuppressMessage("Microsoft.Naming",
                            "CA1704:IdentifiersShouldBeSpelledCorrectly",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#SeqPnts",
                            MessageId = "Pnts",
                            Justification = "Smaller JSON")>]
[<assembly: SuppressMessage("Microsoft.Naming",
                            "CA1704:IdentifiersShouldBeSpelledCorrectly",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#.ctor(System.Collections.Generic.SortedDictionary`2<System.Int32,System.Int32>,System.Collections.Generic.List`1<AltCover.NativeJson+BranchInfo>,System.Collections.Generic.List`1<AltCover.NativeJson+SeqPnt>,System.Nullable`1<System.Int32>,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.String>)",
                            MessageId = "t",
                            Justification = "Smaller JSON")>]
[<assembly: SuppressMessage("Microsoft.Naming",
                            "CA1704:IdentifiersShouldBeSpelledCorrectly",
                            Scope = "member",
                            Target = "AltCover.NativeJson+Method.#.ctor(System.Collections.Generic.SortedDictionary`2<System.Int32,System.Int32>,System.Collections.Generic.List`1<AltCover.NativeJson+BranchInfo>,System.Collections.Generic.List`1<AltCover.NativeJson+SeqPnt>,System.Nullable`1<System.Int32>,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.String>)",
                            MessageId = "Pnts",
                            Justification = "Smaller JSON")>]
[<assembly: SuppressMessage("Microsoft.Naming",
                            "CA1704:IdentifiersShouldBeSpelledCorrectly",
                            Scope = "type",
                            Target = "AltCover.NativeJson+SeqPnt",
                            MessageId = "Pnt",
                            Justification = "Smaller JSON")>]
[<assembly: SuppressMessage("Microsoft.Naming",
                            "CA1704:IdentifiersShouldBeSpelledCorrectly",
                            Scope = "member",
                            Target = "AltCover.NativeJson+SeqPnt.#.ctor(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.Int32>)",
                            MessageId = "e",
                            Justification = "Smaller JSON")>]
[<assembly: SuppressMessage("Microsoft.Naming",
                            "CA1704:IdentifiersShouldBeSpelledCorrectly",
                            Scope = "member",
                            Target = "AltCover.NativeJson+SeqPnt.#.ctor(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.Int32>)",
                            MessageId = "s",
                            Justification = "Smaller JSON")>]
[<assembly: SuppressMessage("Microsoft.Naming",
                            "CA1704:IdentifiersShouldBeSpelledCorrectly",
                            Scope = "member",
                            Target = "AltCover.NativeJson+SeqPnt.#.ctor(System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.Int32>)",
                            MessageId = "v",
                            Justification = "Smaller JSON")>]
()
#endif