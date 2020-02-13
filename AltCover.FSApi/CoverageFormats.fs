namespace AltCover

open System
open System.Diagnostics.CodeAnalysis
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Linq
open System.Xml
open System.Xml.Linq
open System.Xml.XPath

open Mono.Cecil

[<RequireQualifiedAccess>]
module CoverageFormats =

  [<SuppressMessage("Microsoft.Design", "CA1059",
                    Justification = "converts concrete types")>]
  let ConvertToLcov xmlDocument stream =
    let format = XmlUtilities.DiscoverFormat xmlDocument
    let xdoc = XmlUtilities.ToXDocument xmlDocument
    AltCover.LCov.ConvertReport xdoc format stream

  [<SuppressMessage("Microsoft.Design", "CA1059",
                    Justification = "converts concrete types")>]
  let ConvertToCobertura xmlDocument =
    let format = XmlUtilities.DiscoverFormat xmlDocument
    let xdoc = XmlUtilities.ToXDocument xmlDocument
    AltCover.Cobertura.ConvertReport xdoc format

  [<SuppressMessage("Microsoft.Design", "CA1059",
                    Justification = "returns a specific concrete type")>]
  let ConvertFromNCover (navigable : IXPathNavigable) (assemblies : string array) =
    let reporter, rewrite = AltCover.OpenCover.ReportGenerator()
    let visitors = [ reporter ]
    let navigator = navigable.CreateNavigator()
    let identities = Dictionary<string, XPathNavigator>()
    navigator.Select("//module").OfType<XPathNavigator>()
    |> Seq.iter (fun n ->
         let key = n.GetAttribute("assemblyIdentity", String.Empty)
         identities.Add(key, n))

    let paths = Dictionary<string, string>()
    assemblies
    |> Seq.iter (fun p ->
         let a =
           XmlUtilities.AssemblyNameWithFallback p (Path.GetFileNameWithoutExtension p)
         paths.Add(p, a))
    let usefulAssemblies =
      assemblies
      |> Seq.filter (fun p -> identities.ContainsKey paths.[p])
      |> Seq.map (fun p -> (p, []))

    // ensure default state -- this switches branch recording off
    AltCover.Main.init()

    AltCover.Visitor.Visit visitors usefulAssemblies

    let parse s =
      Int32.TryParse
        (s, System.Globalization.NumberStyles.Integer,
         System.Globalization.CultureInfo.InvariantCulture) |> snd
    // Match modules
    rewrite.Descendants(XName.Get "Module")
    |> Seq.iter (fun target ->
         let path =
           target.Descendants(XName.Get "ModulePath")
           |> Seq.map (fun n -> n.Value)
           |> Seq.head

         let identity = paths.[path]
         let source = identities.[identity]
         let files = Dictionary<string, string>()
         target.Descendants(XName.Get "File").OfType<XElement>()
         |> Seq.iter
              (fun f ->
                files.Add
                  (f.Attribute(XName.Get "fullPath").Value,
                   f.Attribute(XName.Get "uid").Value))

         // Copy sequence points across
         source.Select(".//seqpnt").OfType<XPathNavigator>()
         |> Seq.iter (fun s ->
              let sl = s.GetAttribute("line", String.Empty)
              let sc = s.GetAttribute("column", String.Empty)
              let el = s.GetAttribute("endline", String.Empty)
              let ec = s.GetAttribute("endcolumn", String.Empty)
              let uid = files.[s.GetAttribute("document", String.Empty)]
              let vc = parse <| s.GetAttribute("visitcount", String.Empty)
              let xpath =
                ".//SequencePoint[@sl='" + sl + "' and @sc='" + sc + "' and @el='" + el
                + "' and @ec='" + ec + "' and @fileid='" + uid + "']"
              let sp = Extensions.XPathSelectElement(target, xpath)
              let v = parse <| sp.Attribute(XName.Get "vc").Value
              let visits = (max 0 v) + (max 0 vc)
              sp.Attribute(XName.Get "vc").Value <- visits.ToString
                                                      (System.Globalization.CultureInfo.InvariantCulture)))

    rewrite.Descendants(XName.Get "Class")
    |> Seq.filter (fun c -> c.Descendants(XName.Get "Method") |> Seq.isEmpty)
    |> Seq.toList // reify before making changes
    |> Seq.iter (fun c -> c.Remove())

    let dec = rewrite.Declaration
    dec.Encoding <- "utf-8"
    dec.Standalone <- null

    let converted = XmlUtilities.ToXmlDocument rewrite
    AltCover.Runner.PostProcess null AltCover.Base.ReportFormat.OpenCover converted
    converted

  [<SuppressMessage("Microsoft.Design", "CA1059",
                    Justification = "returns a specific concrete type")>]
  let ConvertToNCover(navigable : IXPathNavigable) =
    let transform = XmlUtilities.LoadTransform "OpenCoverToNCover"
    let rewrite = XmlDocument()
    do use output = rewrite.CreateNavigator().AppendChild()
       transform.Transform(navigable, output)
    XmlUtilities.PrependDeclaration rewrite

    rewrite.SelectNodes("//method").OfType<XmlElement>()
    |> Seq.iter (fun m ->
         let c = m.GetAttribute("class")
         m.SetAttribute("class", c.Replace('/', '+'))
         let name = m.GetAttribute("name")
         let lead = name.Substring(name.LastIndexOf("::", StringComparison.Ordinal) + 2)
         m.SetAttribute("name", lead.Substring(0, lead.IndexOf('('))))

    rewrite.SelectNodes("//module").OfType<XmlElement>()
    |> Seq.iter (fun m ->
         let path = m.GetAttribute("name")
         let info = System.IO.FileInfo path
         m.SetAttribute("name", info.Name)
         let assembly = m.GetAttribute("assembly")
         m.SetAttribute
           ("assemblyIdentity", XmlUtilities.AssemblyNameWithFallback path assembly))

    let culture = System.Threading.Thread.CurrentThread.CurrentCulture
    try
      System.Threading.Thread.CurrentThread.CurrentCulture <- CultureInfo.InvariantCulture
      rewrite.SelectNodes("//coverage").OfType<XmlElement>()
      |> Seq.iter (fun c ->
           let now =
             DateTime.UtcNow.ToLongDateString() + ":" + DateTime.UtcNow.ToLongTimeString()
           c.SetAttribute("startTime", now)
           c.SetAttribute("measureTime", now))
    finally
      System.Threading.Thread.CurrentThread.CurrentCulture <- culture
    rewrite

  let internal X s = XName.Get s

  let internal Summary() =
    XElement
      (X "Summary", XAttribute(X "numSequencePoints", 0),
        XAttribute(X "visitedSequencePoints", 0), XAttribute(X "numBranchPoints", 0),
        XAttribute(X "visitedBranchPoints", 0), XAttribute(X "sequenceCoverage", 0),
        XAttribute(X "branchCoverage", 0), XAttribute(X "maxCyclomaticComplexity", 0),
        XAttribute(X "minCyclomaticComplexity", 0), XAttribute(X "visitedClasses", 0),
        XAttribute(X "numClasses", 0), XAttribute(X "visitedMethods", 0),
        XAttribute(X "numMethods", 0), XAttribute(X "minCrapScore", 0),
        XAttribute(X "maxCrapScore", 0))

  let UpdateMethod (m:XElement) (file:AssemblyDefinition) =
    // visited attribute <Method visited="false" cyclomaticComplexity="1" nPathComplexity="0" sequenceCoverage="0" branchCoverage="0" isConstructor="false" isStatic="true" isGetter="false" isSetter="false" crapScore="0">
    let a = m.Attributes() |> Seq.toList
    m.RemoveAttributes()
    let filterVisted = fun (x:XElement) -> x.Attribute(X "vc").Value <> "0"
    let visited = Seq.concat [ m.Descendants(X "SequencePoint")
                               m.Descendants(X "MethodPoint")
                               m.Descendants(X "BranchPoint") ]
                  |> Seq.exists filterVisted
    let v = XAttribute(X "visited", OpenCover.boolString visited)
    m.Add((v::a) |> List.toArray)
    let declaringTypeName = (m.AncestorsAndSelf(X "Class") |> Seq.head).Descendants(X "FullName")
                            |> Seq.head
    let declaringType = declaringTypeName.Value |> file.MainModule.GetType

    // value in method <MetadataToken>100663297</MetadataToken>
    let methodFullName = (m.Descendants(X "Name") |> Seq.head).Value
    let methodDef = declaringType.Methods
                    |> Seq.tryFind(fun n -> n.FullName = methodFullName)
    methodDef
    |> Option.iter(fun x -> let token = m.Descendants(X "MetadataToken") |> Seq.head
                            token.Value <- x.MetadataToken.ToUInt32().
                                             ToString(CultureInfo.InvariantCulture))
    // xsi:type in <MethodPoint xsi:type="SequencePoint" vc="0" uspid="0" ordinal="0" offset="2" sl="59" sc="16" el="59" ec="17" bec="0" bev="0" fileid="1" />
    //  instead of xmlns:p8="xsi" <MethodPoint vc="0" uspid="0" p8:type="SequencePoint" ordinal="0" offset="0" sc="0" sl="59" ec="1" el="59" bec="0" bev="0" fileid="1" xmlns:p8="xsi" />
    m.Descendants(X "MethodPoint")
    |> Seq.tryHead
    |> Option.iter (fun x -> let a = x.Attributes()
                                     |> Seq.filter (fun s -> s.Name.ToString().Contains("{") |> not)
                                     |> Seq.cast<obj>
                                     |> Seq.toArray
                             x.RemoveAttributes()
                             x.Add(XAttribute(XName.Get("type",
                                                        "http://www.w3.org/2001/XMLSchema-instance"),
                                                        "SequencePoint"))
                             x.Add a)

  let UpdateModule (m:XElement) (files:string array) =
    // supply empty module level  <Summary numSequencePoints="0" visitedSequencePoints="0" numBranchPoints="0" visitedBranchPoints="0" sequenceCoverage="0" branchCoverage="0" maxCyclomaticComplexity="0" minCyclomaticComplexity="0" visitedClasses="0" numClasses="0" visitedMethods="0" numMethods="0" minCrapScore="0" maxCrapScore="0" />
    (m.Elements() |> Seq.head).AddBeforeSelf(Summary())
    let modulePath = m.Element(X "ModulePath")
    let assemblyFileName = modulePath.Value
    let assemblyPath = files |> Seq.tryFind (fun f -> assemblyFileName = Path.GetFileName f)

    assemblyPath
    |> Option.filter File.Exists
    // fill in path  <ModulePath>...\Sample4.dll</ModulePath>
    // fix hash in <Module hash="42-08-CA-1A-A6-25-CE-DA-DD-18-DC-D5-9A-BF-1B-BF-00-1D-E5-9B">
    // visited attribute <Method visited="false" cyclomaticComplexity="1" nPathComplexity="0" sequenceCoverage="0" branchCoverage="0" isConstructor="false" isStatic="true" isGetter="false" isSetter="false" crapScore="0">
    // value in method <MetadataToken>100663297</MetadataToken>
    // xsi:type in <MethodPoint xsi:type="SequencePoint" vc="0" uspid="0" ordinal="0" offset="2" sl="59" sc="16" el="59" ec="17" bec="0" bev="0" fileid="1" />
    //  instead of xmlns:p8="xsi" <MethodPoint vc="0" uspid="0" p8:type="SequencePoint" ordinal="0" offset="0" sc="0" sl="59" ec="1" el="59" bec="0" bev="0" fileid="1" xmlns:p8="xsi" />
    |> Option.iter(fun path -> modulePath.Value <- (Path.GetFullPath path)
                               m.Attribute(X "hash").Value <- KeyStore.HashFile path
                               m.Descendants(X "Method")
                               |> Seq.iter (fun m2 -> UpdateMethod m2 (AssemblyDefinition.ReadAssembly path)))

  let FormatFromCoverlet (report:XDocument) (files:string array) =
    let rewrite = XDocument(report)
    // attributes in <CoverageSession xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

    rewrite.Descendants(X "CoverageSession")
    |> Seq.iter(fun session -> session.RemoveAttributes()
                               session.Add(XAttribute(XNamespace.Xmlns + "xsd",
                                            "http://www.w3.org/2001/XMLSchema"),
                                           XAttribute(XNamespace.Xmlns + "xsi",
                                            "http://www.w3.org/2001/XMLSchema-instance")))

    // supply empty module level  <Summary numSequencePoints="0" visitedSequencePoints="0" numBranchPoints="0" visitedBranchPoints="0" sequenceCoverage="0" branchCoverage="0" maxCyclomaticComplexity="0" minCyclomaticComplexity="0" visitedClasses="0" numClasses="0" visitedMethods="0" numMethods="0" minCrapScore="0" maxCrapScore="0" />
    // fill in path  <ModulePath>...\Sample4.dll</ModulePath>
    // visited attribute <Method visited="false" cyclomaticComplexity="1" nPathComplexity="0" sequenceCoverage="0" branchCoverage="0" isConstructor="false" isStatic="true" isGetter="false" isSetter="false" crapScore="0">
    // value in method <MetadataToken>100663297</MetadataToken>
    // xsi:type in <MethodPoint xsi:type="SequencePoint" vc="0" uspid="0" ordinal="0" offset="2" sl="59" sc="16" el="59" ec="17" bec="0" bev="0" fileid="1" />
    //  instead of xmlns:p8="xsi" <MethodPoint vc="0" uspid="0" p8:type="SequencePoint" ordinal="0" offset="0" sc="0" sl="59" ec="1" el="59" bec="0" bev="0" fileid="1" xmlns:p8="xsi" />
    rewrite.Descendants(X "Module")
    |> Seq.iter (fun m -> UpdateModule m files)

    // TODO list
    // Fix offset, sc, ec in <SequencePoint vc="1" uspid="1" ordinal="0" offset="0" sl="47" sc="21" el="47" ec="26" bec="0" bev="0" fileid="1" />
    // Fix offset, sc, ec in <MethodPoint />

    // TODO - wants the tidy-up API https://github.com/SteveGilham/altcover/projects/8#card-28301506
    // complete module level  <Summary numSequencePoints="23" visitedSequencePoints="10" numBranchPoints="21" visitedBranchPoints="7" sequenceCoverage="43.48" branchCoverage="33.33" maxCyclomaticComplexity="11" minCyclomaticComplexity="1" visitedClasses="4" numClasses="7" visitedMethods="7" numMethods="11" minCrapScore="1" maxCrapScore="62.05" />
    // compute bec and bev values
    // compute crap score values

    rewrite