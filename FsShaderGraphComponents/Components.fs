﻿namespace FsShaderGraphComponents

open Microsoft.FSharp.Reflection

open System
open System.CodeDom
open System.Collections.Generic
open System.Linq
open System.Drawing
open System.Windows.Forms
open System.Text.RegularExpressions

open Grasshopper
open Grasshopper.Kernel
open Grasshopper.Kernel.Attributes
open Grasshopper.Kernel.Types
open Grasshopper.Kernel.Special

open ccl.ShaderNodes

open Rhino.Geometry

open RhinoCyclesCore.Materials

open ShaderGraphResources
open RhinoCyclesCore.Environments

// ---------------------------------------
/// Simple color representation with ints (R, G, B)
type IntColor = int * int * int
/// Socket connection info (tocomponent, tosocket, fromsocket, fromcomponent)
type SocketsInfo = obj * IGH_Param * IGH_Param * obj

module Utils =
  let nfi = ccl.Utilities.Instance.NumberFormatInfo

  let toString (x:'a) = 
    match FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name

  let fromString<'a> (s:string) =
    match FSharpType.GetUnionCases typeof<'a> |> Array.filter (fun case -> case.Name = s) with
    |[|case|] -> Some(FSharpValue.MakeUnion(case,[||]) :?> 'a)
    |_ -> None

  /// Give message if true, else empty string ""
  let SetMessage t m = match t with true -> m | _ -> ""

  let Samples = 50

  let Logarithm (a:float) (b:float) = (/) (log a) (log b)

  let GreaterThan a b = match (>) a b with true -> 1.0 | _ -> 0.0
  let LessThen a b = match (<) a b with true -> 1.0 | _ -> 0.0

  let Sine a b = b |> ignore; sin a
  let Cosine a b = b|> ignore; cos a
  let Tangent a b = b|> ignore; tan a
  let Arcsine a b = b |> ignore; asin a
  let Arccosine a b = b|> ignore; acos a
  let Arctangent a b = b|> ignore; atan a
  let Round a b = b|> ignore; round a
  let Modulo a b = b |> ignore; a
  let Absolute a b = b |> ignore; if a < 0.0 then -a else a

  /// Give first (R) component of triplet (IntColor)
  let R (r:int, _:int, _:int) = r
  /// Give second (G) component of triplet (IntColor)
  let G (_:int, g:int, _:int) = g
  /// Give third (B) component of triplet (IntColor)
  let B (_:int, _:int, b:int) = b

  let rnd = new Random()

  /// Convert a byte channel to float
  let RGBChanToFloat (b:byte) = (float32 b)/255.0f

  let (|IntColor|) (c:Color) = 
    ((int c.R), (int c.G), (int c.B))
  let IntColorFromColor (c:Color) =
    ((int c.R), (int c.G), (int c.B))

  let ColorXml (c:Color) =
    String.Format(nfi, "{0} {1} {2}", RGBChanToFloat(c.R), RGBChanToFloat(c.G), RGBChanToFloat(c.B))

  /// Read color from given component data access at index idx. component
  /// message will be set to msg if reading the data failed.
  /// Returns an IntColor.
  let readColor(u:GH_Component, da:IGH_DataAccess, idx:int, msg) : Color =
    let mutable c = new GH_Colour()
    let r = da.GetData(idx, &c)
    u.Message <- SetMessage (not r) msg
    c.Value

  let readVector(u:GH_Component, da:IGH_DataAccess, idx:int, msg) : Vector3d =
    let mutable c = new GH_Vector()
    let r = da.GetData(idx, &c)
    u.Message <- SetMessage (not r) msg
    c.Value

  let float4FromColor (ic:Color) =
    ccl.float4(RGBChanToFloat(ic.R), RGBChanToFloat(ic.G), RGBChanToFloat(ic.B), 1.0f)

  let float4FromVector (vec:Vector3d) =
    ccl.float4((float32)vec.X, (float32)vec.Y, (float32)vec.Z, 1.0f)

  /// Read float from given component data access at index idx. component
  /// message will be set to msg if reading the data failed.
  /// Returns a float.
  let readFloat(u:GH_Component, da:IGH_DataAccess, idx:int, msg) : float =
    let mutable f = new GH_Number()
    let r = da.GetData(idx, &f)
    u.Message <- SetMessage (not r) msg
    f.Value

  let readFloat32(u:GH_Component, da:IGH_DataAccess, idx:int, msg) : float32 =
    (float32)(readFloat(u, da, idx, msg))

  let readInt (u:GH_Component, da:IGH_DataAccess, idx:int, msg) : int =
    let mutable f = new GH_Number()
    let r = da.GetData(idx, &f)
    u.Message <- SetMessage (not r) msg
    (int)f.Value

  let readString (u:GH_Component, da:IGH_DataAccess, idx:int, msg) : string =
    let mutable f = new GH_String()
    let r = da.GetData(idx, &f)
    u.Message <- SetMessage (not r) msg
    f.Value

  let randomColor = Color.FromArgb(rnd.Next(0, 255), rnd.Next(0, 255), rnd.Next(0, 255))

  /// Create a GH_Colour from given IntColor
  let createColor c = new GH_Colour(Color.FromArgb((R c), (G c), (B c)))

  /// Average out given IntColor with Utils.Samples
  let AvgColor c = ((R c) / Samples, (G c) / Samples, (B c) / Samples)

  /// Weight two IntColors given fac. A fac of 0.0 will yield c2,
  /// a fac of 1.0 will yield c1
  let WeightColors c1 c2 fac : IntColor =
    let choosecolor a b =
      match rnd.NextDouble() with i when i < fac -> a | _ -> b
    let cadder a b = ((R a) + (R b), (G a) + (G b), (B a) + (B b))

    List.init Samples (fun _ -> choosecolor c1 c2) |> List.reduce cadder |> AvgColor

  /// Cast an object as 'T, or null if that fails
  let castAs<'T when 'T : null> (o:obj) =
    match o with :? 'T as res -> res | _ -> null

  let GetDataXml (inp:IGH_Param, iteration: int) =
    match inp.SourceCount=1 with
    | true -> ("", "")
    | false ->
      let idx =
        match iteration<inp.VolatileDataCount with
        | true -> iteration
        | _ -> inp.VolatileDataCount - 1
      let in1 = inp.VolatileData.StructureProxy.[0].[idx]
      match in1 with
      | :? GH_Colour ->
          let c = castAs<GH_Colour>(in1)
          (inp.Name, ColorXml(c.Value))
      | :? GH_Vector ->
          let c = castAs<GH_Vector>(in1)
          (inp.Name, c.Value.ToString().Replace("(", "").Replace(")", "").Replace(",", " "))
      | _ ->
          (inp.Name.ToLowerInvariant(), String.Format(nfi, "{0}", in1))

  /// Get data XML representation from given input list
  let GetInputsXml (inputs:List<IGH_Param>, iteration:int) =
    String.Concat([for i in inputs -> 
                    let t = GetDataXml(i, iteration)
                    match (fst t) with
                    | "" -> ""
                    | _ -> (fst t).Replace(" ", "_").ToLowerInvariant() + "=\""+ (snd t) + "\" "
    ])

  let GetNodeXml node name data =
    node + " name=\"" + name + "\" " + data


  let cleanName (nn:string) =
    nn.Replace(" ", "_").Replace("-", "_").Replace(">", "").ToLowerInvariant()
  
  let node_componentmapping = new Dictionary<Type, Guid>()

/// type that signals Grasshopper to continue loading. Here we
/// do necessary initialisation
type Priority() = 
    inherit GH_AssemblyPriority()
    override u.PriorityLoad() =
      u |> ignore
      GH_LoadingInstruction.Proceed

/// Grasshopper plug-in assembly information.
and Info() as it =
    inherit GH_AssemblyInfo()
    let loaded = Instances.ComponentServer.GHAFileLoaded

    let cb (x:GH_GHALoadingEventArgs) =
      if x.Id.Equals(it.Id) then
        printfn "Match: %A %A" System.DateTime.Now x
        printfn "%A" x.FileName
        x.Assembly.ExportedTypes
        |> Seq.filter (
            fun et ->
                try
                  let i = Activator.CreateInstance(et) :?> CyclesNode
                  true
                with
                  | x -> false
          )
        |> Seq.map (fun t ->
                      
                      let i = Activator.CreateInstance(t) :?> CyclesNode
                      i.ShaderNode.GetType(), i.ComponentGuid
                    )
        |> Seq.iter (fun (k, v) ->
                      Utils.node_componentmapping.Add(k, v)
                      printfn "mapped n %A guid %A" k v)

    do
      loaded |> Observable.subscribe cb |> ignore

    override u.Name =
      u |> ignore
      "Shader Nodes"
    override u.Description =
      u |> ignore
      "Create shader graphs for Cycles for Rhino"
    override u.Id =
      u |> ignore
      new Guid("6a051e83-3727-465e-b5ef-74d027a6f73b")
    override u.Icon =
      u |> ignore
      Icons.ShaderGraph
    override u.AuthorName =
      u |> ignore
      "Nathan 'jesterKing' Letwory"
    override u.AuthorContact = 
      u |> ignore
      "nathan@mcneel.com"


and CyclesNode(name, nickname, description, category, subcategory, nodetype : Type) =
  inherit GH_Component (name, nickname, description, category, subcategory)

  let ntype : Type = nodetype
  let mutable intNode : ShaderNode = null

  // our construction section. Create an instance of the nodetype this
  // CyclesNode instance represents. Here we also create a name for
  // the new shader node.
  do
    let p = [ Utils.cleanName nickname ] |> Seq.map (fun x -> x :> obj) |> Seq.toArray
    let t = Utils.castAs<ShaderNode>( Activator.CreateInstance( ntype, p ) )
    intNode <- t
    base.PostConstructor()

  // override constructor, because we want to ensure we can
  // hook into the construction process before it ends.
  // calling the base PostConstructor in our construction
  // section
  override u.PostConstructor() = u |> ignore; ()

  /// Shader node instance this CyclesNode encompasses
  member u.ShaderNode : ShaderNode =  u |> ignore; intNode

  /// Iterate over the ShaderNode inputs and register them with the GH component
  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    match u.ShaderNode.inputs with
    | null -> ()
    | _ ->
      for x in u.ShaderNode.inputs.Sockets do
        match x with
        | :? ccl.ShaderNodes.Sockets.ClosureSocket as socket ->
          mgr.AddColourParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item, Color.Gray) |> ignore
        | :? ccl.ShaderNodes.Sockets.FloatSocket as socket ->
          mgr.AddNumberParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item, 0.0) |> ignore
        | :? ccl.ShaderNodes.Sockets.ColorSocket as socket ->
          mgr.AddColourParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item, Color.Gray) |> ignore
        | :? ccl.ShaderNodes.Sockets.VectorSocket as socket ->
          mgr.AddVectorParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item, Vector3d.Zero) |> ignore
        | :? ccl.ShaderNodes.Sockets.Float4Socket as socket ->
          mgr.AddVectorParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item, Vector3d.Zero) |> ignore
        | :? ccl.ShaderNodes.Sockets.IntSocket as socket ->
          mgr.AddNumberParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item, 0.0) |> ignore
        | :? ccl.ShaderNodes.Sockets.StringSocket as socket ->
          mgr.AddTextParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item, "") |> ignore
        | _ ->
          failwith "unknown socket type"

  /// If ShaderNode has outputs register them here
  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    match u.ShaderNode.outputs with
    | null -> ()
    | _ ->
      for x in u.ShaderNode.outputs.Sockets do
        match x with
        | :? ccl.ShaderNodes.Sockets.ClosureSocket as socket ->
          mgr.AddColourParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item) |> ignore
        | :? ccl.ShaderNodes.Sockets.FloatSocket as socket ->
          mgr.AddNumberParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item) |> ignore
        | :? ccl.ShaderNodes.Sockets.ColorSocket as socket ->
          mgr.AddColourParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item) |> ignore
        | :? ccl.ShaderNodes.Sockets.VectorSocket as socket ->
          mgr.AddVectorParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item) |> ignore
        | :? ccl.ShaderNodes.Sockets.Float4Socket as socket ->
          mgr.AddVectorParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item) |> ignore
        | :? ccl.ShaderNodes.Sockets.IntSocket as socket ->
          mgr.AddNumberParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item) |> ignore
        | :? ccl.ShaderNodes.Sockets.StringSocket as socket ->
          mgr.AddTextParameter(socket.Name, socket.Name, socket.Name, GH_ParamAccess.item) |> ignore
        | _ ->
          failwith "unknown socket type"

  /// Iterate over inputs and outputs and read / set data.
  /// For input sockets we find the input nodes and connect
  /// from the corresponding output sockets on the ShaderNode
  override u.SolveInstance(DA: IGH_DataAccess) =
    u.ShaderNode.Name <- Utils.cleanName u.NickName
    let setdata (i:int) (s:ccl.ShaderNodes.Sockets.ISocket) =
      match s with
      | :? ccl.ShaderNodes.Sockets.ClosureSocket ->
        DA.SetData(i, Color.Gray) |> ignore
      | :? ccl.ShaderNodes.Sockets.ColorSocket ->
        DA.SetData(i, Color.Gray) |> ignore
      | :? ccl.ShaderNodes.Sockets.VectorSocket ->
        DA.SetData(i, Vector3d.Zero) |> ignore
      | :? ccl.ShaderNodes.Sockets.FloatSocket ->
        DA.SetData(i, 0.0) |> ignore
      | :? ccl.ShaderNodes.Sockets.Float4Socket ->
        DA.SetData(i, Vector3d.Zero) |> ignore
      | :? ccl.ShaderNodes.Sockets.IntSocket ->
        DA.SetData(i, 0.0) |> ignore
      | :? ccl.ShaderNodes.Sockets.StringSocket ->
        DA.SetData(i, "-") |> ignore
      | _ ->
        failwith "unknown socket type"

    let getdata (i:int) (s:ccl.ShaderNodes.Sockets.ISocket) =
      match s with
      | :? ccl.ShaderNodes.Sockets.ClosureSocket as closure ->
        let col = Utils.readColor (u, DA, i, "couldn't read closure")
        closure.Value <- Utils.castAs<obj>(col)
      | :? ccl.ShaderNodes.Sockets.ColorSocket as color ->
        let col = Utils.readColor (u, DA, i, "couldn't read color")
        color.Value <- Utils.float4FromColor col
      | :? ccl.ShaderNodes.Sockets.VectorSocket as vector ->
        let vec = Utils.readVector (u, DA, i, "couldn't read vector")
        vector.Value <- Utils.float4FromVector vec
      | :? ccl.ShaderNodes.Sockets.FloatSocket as flt->
        let fl = Utils.readFloat (u, DA, i, "couldn't read float")
        flt.Value <- (float32)fl
      | :? ccl.ShaderNodes.Sockets.Float4Socket as float4Vector ->
        let vec = Utils.readVector (u, DA, i, "couldn't read vector")
        float4Vector.Value <- Utils.float4FromVector vec
      | :? ccl.ShaderNodes.Sockets.IntSocket as intsock ->
        let intval = Utils.readInt (u, DA, i, "couldn't read integer")
        intsock.Value <- intval
      | :? ccl.ShaderNodes.Sockets.StringSocket as stringsock ->
        let strval = Utils.readString (u, DA, i, "couldn't read string")
        stringsock.Value <- strval
      | _ ->
        failwith "unknown socket type"

    let iterSources (idx:int) (item:IGH_Param) =
      match idx < u.ShaderNode.inputs.Sockets.Count() with
      | false -> ()
      | true ->
        let tosocket = u.ShaderNode.inputs.[idx]
        tosocket.ClearConnections()
        match item.SourceCount > 0 with
        | false -> ()
        | true ->
          let isource = item.Sources.[0]
          match isource.Attributes.Parent with
          | null ->
            match isource with
            | ( :? GH_Param<GH_Number> | :? GH_Param<GH_Colour>) as param->
              if param.NickName.Contains(".") then tosocket.SetValueCode <- param.NickName
            | _ -> ()
            ()
          | _ ->
            match isource.Attributes.Parent.DocObject with
            | :? CyclesNode as cn -> 
              let gh = cn :> GH_Component
              let sidx = gh.Params.Output.FindIndex(fun p -> isource.InstanceGuid=p.InstanceGuid)
              match sidx > -1 with
              | false -> ()
              | true ->
                let fromsock = cn.ShaderNode.outputs.[sidx]
                fromsock.Connect(tosocket)
            | :? GH_Component as gh ->
              match gh.NickName.Contains(".") with
              | true ->
                tosocket.SetValueCode <- gh.NickName
              | false -> ()
            | _ -> ()

    let iterRecipients (idx:int) (item:IGH_Param) =
      String.Format("{0}:{1} ({2})", idx, item.Name, item.Recipients.Count)
    let inputs = u.Params.Input |> Seq.mapi iterSources
    let outputs = u.Params.Output |> Seq.mapi iterRecipients

    outputs |> Seq.iter ignore
    inputs |> Seq.iter ignore

    let inputParameters =
      u.ShaderNode.inputs.Sockets 
      |> Seq.cast<ccl.ShaderNodes.Sockets.ISocket>

    inputParameters
    |> Seq.mapi getdata
    |> Seq.iter ignore

    let outputParameters =
      u.ShaderNode.outputs.Sockets 
      |> Seq.cast<ccl.ShaderNodes.Sockets.ISocket>

    outputParameters
    |> Seq.mapi setdata
    |> Seq.iter ignore

  override u.ComponentGuid = u |> ignore; Guid.Empty
  override u.Icon = u|> ignore; Icons.Blend


and Interpolation = None | Linear | Closest | Cubic | Smart with
  member u.ToString = Utils.toString u
  member u.ToStringR = (u.ToString).Replace("_", "-")
  static member FromString s = Utils.fromString<Interpolation> s

and EnvironmentProjection = Equirectangular | Mirror_Ball | Wallpaper with
  member u.ToString = Utils.toString u
  member u.ToStringR = (u.ToString).Replace("_", " ")
  static member FromString s = Utils.fromString<EnvironmentProjection> ((s:string).Replace(" ", "_"))

and TextureProjection = Flat | Box | Sphere | Tube with
  member u.ToString = Utils.toString u
  member u.ToStringR = (u.ToString).Replace("_", "-")
  static member FromString s = Utils.fromString<TextureProjection> s

and TextureExtension = Repeat | Extend | Clip with
  member u.ToString = Utils.toString u
  member u.ToStringR = (u.ToString).Replace("_", "-")
  static member FromString s = Utils.fromString<TextureExtension> s

and ColorSpace = None | Color with
  member u.ToString = Utils.toString u
  member u.ToStringR = (u.ToString).Replace("_", "-")
  static member FromString s = Utils.fromString<ColorSpace> s

/// Distributions used in several nodes: Glass, Glossy, Refraction
and Distribution = Sharp | Beckmann | GGX | Ashihkmin_Shirley | Multiscatter_GGX with
  member u.ToString = Utils.toString u
  member u.ToStringR = (u.ToString).Replace("_", "-")
  static member FromString s = Utils.fromString<Distribution> s

and Falloff = Cubic | Gaussian | Burley with
  member u.ToString = Utils.toString u
  static member FromString s = Utils.fromString<Falloff> s

/// The output node for the shader system. This node is responsible for
/// driving the XML generation of a shader graph.
and OutputNode() =
  inherit CyclesNode(
    "Output", "output",
    "Output node for shader graph",
    "Shader", "Output",
    typeof<ccl.ShaderNodes.OutputNode>)

  let matId = ResizeArray<Guid>()

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    u |> ignore
    mgr.AddTextParameter("Xml", "X", "tree as xml", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid =
    u |> ignore
    new Guid("14df22af-d119-4f69-a536-34a30ddb175e")

  override u.Icon =
    u |> ignore
    Icons.Output

  override u.AppendAdditionalComponentMenuItems(menu:ToolStripDropDown) =
    let rms = Rhino.RhinoDoc.ActiveDoc.RenderMaterials.Where(fun x ->
          not (isNull(Utils.castAs<XmlMaterial>(x)))).Select(fun i -> i.Name, i.Id).Distinct()
    let appendMenu name id =
      let handleMenuClick _ _ =
        match matId.Contains id with
        | false -> matId.Add id |> ignore
        | true -> matId.Remove id |> ignore
        u.ExpireSolution true
      GH_DocumentObject.Menu_AppendItem(menu, name, handleMenuClick, true, matId.Contains id) |> ignore
    rms |> Seq.iter (fun x -> appendMenu (fst x) (snd x)) |> ignore

  member u.IsBackground =
    match u.Params.Input.[0].SourceCount>0 with
    | false -> false
    | true ->
      let rec hasBgNode (n:GH_Component) (acc:bool) =
        [for inp in n.Params.Input ->
          match inp.SourceCount>0 with
          | false -> acc
          | true ->
            let s = inp.Sources.[0]
            match s with
            | :? GH_NumberSlider -> acc
            | :? GH_ColourPickerObject -> acc
            | _ -> 
              let attrp = Utils.castAs<GH_ComponentAttributes>(s.Attributes.Parent)
              match attrp with
              | null -> acc
              | _ ->
                match attrp.Owner.ComponentGuid = new Guid("dd68810b-0a0e-4c54-b08e-f46b41e79f32") with
                | true -> acc
                | false -> hasBgNode (Utils.castAs<GH_Component>(attrp.Owner)) acc
        ].Any(fun x -> x)
        
      hasBgNode u false


  override u.SolveInstance(da : IGH_DataAccess) =

    let theshader = new ccl.CodeShader(ccl.Shader.ShaderType.Material)

    base.SolveInstance(da)

    u.Message <- ""

    let getSource i (p:IGH_Param) =
      match i < p.SourceCount with
      | true -> p.Sources.[i]
      | false -> p.Sources.LastOrDefault()

    let cleancontent (l:string) = l.Trim().Replace("\n", "")
    let linebreaks (l:string) = l.Replace(";", ";\n")
    let xmllinebreaks (l:string) = l.Replace(">", ">\n")


    let usedNodes (n:GH_Component) (iteration:int) =
      let rec colcontags (acc: obj list) (_n:obj) =
        let n = Utils.castAs<GH_Component>(_n)
        match n with
        | null -> acc
        | _ ->
          let diveinto (inp:IGH_Param) =
            let s = getSource iteration inp
            let tst =
              match isNull s with
              | true -> null
              | false ->
                match s with
                | :? GH_NumberSlider -> Utils.castAs<obj>(s)
                | :? GH_ColourPickerObject -> Utils.castAs<obj>(s)
                | s when isNull s -> null
                | _ -> 
                  let attrp = Utils.castAs<GH_ComponentAttributes>(s.Attributes.Parent)
                  match attrp with
                  | null -> null
                  | _ -> Utils.castAs<obj>(attrp.Owner)
            tst
          let dd = n.Params.Input |> Seq.map diveinto
          
          let filteredCompAttrs =
            dd
            |> Seq.filter (fun x -> (isNull >> not) x)

          let deeperCompAttrs =
            filteredCompAttrs
            |> Seq.map (fun x -> colcontags [] x)

          let resCompAttrs =
            deeperCompAttrs
            |> Seq.concat
            |> List.ofSeq

          filteredCompAttrs |> List.ofSeq |> List.append resCompAttrs |> List.append acc

      colcontags [] n

    let usednodes = usedNodes u da.Iteration |> Seq.distinct |> List.ofSeq

    let addtoshader (x:obj) =
      match x with
      | :? CyclesNode as cn -> theshader.AddNode(cn.ShaderNode)
      | _ -> ()

    let rr = usednodes |> List.iter addtoshader
    rr |> ignore
    theshader.FinalizeGraph() |> ignore

    let isBackgroundShader =
      let isBackgroundNode (o:obj) = 
        match o with
        | :? CyclesNode as n ->
          n.ComponentGuid.Equals(new Guid("dd68810b-0a0e-4c54-b08e-f46b41e79f32"))
        | _ -> false
      match List.tryFind isBackgroundNode usednodes with
      | Option.None -> false
      | _ -> true

    let newxmlcode =
      theshader.Xml + u.ShaderNode.CreateConnectXml()
      |> cleancontent
      |> xmllinebreaks
    let csharpcode =
      theshader.Code + u.ShaderNode.CreateConnectCode()
      |> cleancontent
      |> linebreaks

    let xmlcode = newxmlcode + "<!--\n" + csharpcode  + "\n-->"

    match isBackgroundShader with
    | true ->
      let env = Utils.castAs<XmlEnvironment>(Rhino.RhinoDoc.ActiveDoc.CurrentEnvironment.ForBackground)
      match env with
      | null -> u.Message <- "NO BACKGROUND"
      | _ ->
        env.BeginChange(Rhino.Render.RenderContent.ChangeContexts.Ignore)
        env.SetParameter("xmlcode", xmlcode) |> ignore
        env.EndChange()
        Rhino.RhinoDoc.ActiveDoc.CurrentEnvironment.ForBackground <- env
      ()
    | false ->
      let m = 
        match matId.Count with
        | 0 -> null
        | _ ->
          let midx =
            match da.Iteration < matId.Count with
            | true -> da.Iteration
            | false -> matId.Count - 1
          Rhino.RhinoDoc.ActiveDoc.RenderMaterials.Where(fun m -> m.Id = matId.[midx]).FirstOrDefault()
      match m with
      | null ->
        u.Message <- "NO MATERIAL"
      | _ ->
        let m' = m :?> XmlMaterial
        m'.BeginChange(Rhino.Render.RenderContent.ChangeContexts.Ignore)
        m'.SetParameter("xmlcode", xmlcode) |> ignore
        m'.EndChange()
        match matId.Count > 1 with
        | true -> u.Message <- "multiple materials set"
        | _ -> u.Message <- m.Name
        for mm in Rhino.RhinoDoc.ActiveDoc.Materials.Where(fun x -> x.RenderMaterialInstanceId = m.Id) do
          mm.DiffuseColor <- Utils.randomColor
          mm.CommitChanges() |> ignore

    da.SetData(0, xmlcode ) |> ignore

and ReverseGraph() =
  inherit GH_Component("Reverser", "reverser", "Create graph from shader", "Shader", "Utilities")

  override u.ComponentGuid =
    u |> ignore
    new Guid("c832908b-1742-4d61-afb1-51d3467ab3c0")

  override u.Icon =
    u |> ignore
    Icons.Output

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    u |> ignore

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddTextParameter("Source", "source", "Source code C#", GH_ParamAccess.item, "") |> ignore
    mgr.AddBooleanParameter("Generate", "generate", "set to true to generate shader tree", GH_ParamAccess.item, false) |> ignore
    u |> ignore
  


  override u.SolveInstance(da : IGH_DataAccess) =
    u |> ignore

    let sicb =
      fun (doc:GH_Document) ->
        // get the source input on Source
        // note that this isn't used yet. For now
        // we work only with RhinoFullNxt
        let mutable txt = new GH_String("")
        let ran = System.Random(13)
        let r2 = da.GetData(0, &txt)
        if r2 then printfn "%A" txt.Value
        let cyclesshader = new RhinoCyclesCore.CyclesShader((uint32)0)
        cyclesshader.SetupShaderShim()
        let codesh = new ccl.CodeShader(ccl.Shader.ShaderType.Material)
        let rfn = new RhinoCyclesCore.Shaders.RhinoFullNxt(null, cyclesshader, codesh)
        let sh = rfn.GetShader() :?> ccl.CodeShader
        // get vector nodes from shader. These need to be retrieved separately, as they have a different
        // base class
        let vectornodes =
          sh.Nodes
          |> Seq.filter (fun n -> n.GetType()=typeof<ccl.ShaderNodes.VectorMathNode>)
          |> Seq.map ( fun n -> n :?> ccl.ShaderNodes.VectorMathNode )
          |> Seq.map ( fun vmn ->
                        vmn :> ShaderNode, match vmn.Operation with
                                               | VectorMathNode.Operations.Add -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.VectorAdd>]
                                               | VectorMathNode.Operations.Subtract -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.VectorSubtract>]
                                               | VectorMathNode.Operations.Average -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.VectorAverage>]
                                               | VectorMathNode.Operations.Cross_Product -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.VectorCross_Product>]
                                               | VectorMathNode.Operations.Dot_Product -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.VectorDot_Product>]
                                               | VectorMathNode.Operations.Normalize -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.VectorNormalize>]
                                               | _ -> failwith "unknown vector math operation"
                     )
        // get math nodes from shader. These need to be retrieved separately, as they have a different
        // base class
        let othernodes =
          sh.Nodes
          |> Seq.filter (fun n -> n.GetType()=typeof<ccl.ShaderNodes.MathNode>)
          |> Seq.map ( fun n -> n :?> ccl.ShaderNodes.MathNode )
          |> Seq.map ( fun mn ->
                          mn :> ShaderNode , match mn.Operation with
                                                | MathNode.Operations.Add -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathAdd>]
                                                | MathNode.Operations.Subtract -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathSubtract>]
                                                | MathNode.Operations.Multiply -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathMultiply>]
                                                | MathNode.Operations.Divide -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathDivide>]
                                                | MathNode.Operations.Sine -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathSine>]
                                                | MathNode.Operations.Cosine -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathCosine>]
                                                | MathNode.Operations.Tangent -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathTangent>]
                                                | MathNode.Operations.Arcsine -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathArcsine>]
                                                | MathNode.Operations.Arccosine -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathArccosine>]
                                                | MathNode.Operations.Arctangent -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathArctangent>]
                                                | MathNode.Operations.Power -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathPower>]
                                                | MathNode.Operations.Logarithm -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathLogarithm>]
                                                | MathNode.Operations.Minimum -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathMinimum>]
                                                | MathNode.Operations.Maximum -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathMaximum>]
                                                | MathNode.Operations.Round -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathRound>]
                                                | MathNode.Operations.Modulo -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathModulo>]
                                                | MathNode.Operations.Less_Than -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathLess_Than>]
                                                | MathNode.Operations.Greater_Than -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathGreater_Than>]
                                                | MathNode.Operations.Absolute -> Utils.node_componentmapping.[typeof<ccl.ShaderNodes.MathAbsolute>]
                                                | _ -> failwith "uknown math operation"
                      )

        // Get nodes nodes from shader
        let simplenodes = 
          sh.Nodes
          |> Seq.filter (fun n -> Utils.node_componentmapping.ContainsKey(n.GetType()))
          |> Seq.map (fun n ->
                        printfn "%A" n
                        n, Utils.node_componentmapping.[n.GetType()]
                      )
        
        // concatenate al sequences
        let allnodes = othernodes.Concat(simplenodes).Concat(vectornodes)

        // map all shader nodes to instances of corresponding GH nodes
        let instances = 
          allnodes
          |> Seq.map (fun (node, guid) ->
                        printfn "%A" (node, guid) 
                        node, Instances.ComponentServer.EmitObject(guid)
                      )
          |> Seq.toList


        // Add all GH instances to the object through mapping the instances list
        // to the success and original tuple
        let addedinstances =
          instances
          |> List.map (
                        fun (node, ghobj) ->
                          ghobj.CreateAttributes()
                          ghobj.Name <- node.Name
                          ghobj.NickName <- node.Name
                          ghobj.Attributes.Pivot <- new PointF(float32(ran.NextDouble() * 800.0), float32(ran.NextDouble() * 800.0))
                          doc.AddObject((ghobj), false, Int32.MaxValue), node, ghobj
                      )
        
        /// helper function to test if a socket is connected to
        let connected (sock:Sockets.ISocket) = 
          not (sock.ConnectionFrom = null)
        

        // Now iterate over all added instances and wire the ump.
        addedinstances
          |> List.iter (
                        fun (addedsuccessfully, shadernode, grasshoppernode) ->
                          shadernode.inputs.Sockets
                          |> Seq.iteri (
                            fun inputindex inputsocket ->
                              printfn "%b socket %i %A of component %A" addedsuccessfully inputindex inputsocket shadernode
                              match connected inputsocket with
                              | true ->
                                let addsuccess, fromshadernode, fromgrasshoppernode = addedinstances |> List.find (fun (_, j,_) -> inputsocket.ConnectionFrom.Parent = j)
                                let connectionindex =
                                  fromshadernode.outputs.Sockets
                                  |> Seq.findIndex (fun tst -> tst = inputsocket.ConnectionFrom)
                                let fromc = fromgrasshoppernode :?> IGH_Component
                                let toc = grasshoppernode :?> IGH_Component
                                let tocinp = toc.Params.Input.[inputindex]
                                let fromcoutp = fromc.Params.Output.[connectionindex]
                                printfn "%A making connection from %A %A to %A %A" addsuccess fromc tocinp toc tocinp
                                tocinp.AddSource(fromcoutp)
                              | _ -> ()
                          ) 
                          ()
                      )

    Utils.node_componentmapping :> seq<_> |> Seq.map (|KeyValue|) |> Seq.iter (fun (k,v) -> printfn "n %A c %A" k v)

    // get the bool input on Generate
    let mutable bool = new GH_Boolean()
    let r = da.GetData(1, &bool)

    // we can't directly calculate a solution here, so we need to schedule the actual
    // calculation. If we don't do that the wiring up of the solution goes completely
    // wrong.
    match bool.Value with
    | false -> ()
    | _ ->
      Instances.ActiveCanvas.Document.ScheduleSolution(500, new GH_Document.GH_ScheduleDelegate(sicb))