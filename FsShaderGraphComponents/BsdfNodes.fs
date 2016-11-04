﻿namespace BsdfNodes

open System
open System.Drawing
open System.Windows.Forms

open Grasshopper.Kernel

open Rhino.Geometry

open FsShaderGraphComponents

open ShaderGraphResources

type BlendNode() =
  inherit GH_Component("Blend", "blend", "Blend two BSDF nodes", "Shader", "Operation")

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Closure1", "1", "First closure input", GH_ParamAccess.item, Color.Aquamarine) |> ignore
    mgr.AddColourParameter("Closure2", "2", "Second closure input", GH_ParamAccess.item, Color.DeepPink) |> ignore
    mgr.AddNumberParameter("Fac", "F", "0.0 full first closure, 1.0 full second", GH_ParamAccess.item, 0.5) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("Closure", "C", "Blend of Closure1 and Closure2", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("133f2f95-926f-4ab4-bc8b-5f96e106d3e4")

  override u.Icon = Icons.Blend

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c1 = Utils.readColor(u, DA, 0, "Couldn't read Closure 1")
    let c2 = Utils.readColor(u, DA, 1, "Couldn't read Closure 2")
    let f = Utils.readFloat(u, DA, 2, "Couldn't read Fac")
    let nsc = Utils.WeightColors c1 c2 f
    DA.SetData(0, Utils.createColor nsc) |> ignore

  interface ICyclesNode with
    member u.NodeName = "mix_closure"

    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type BackgroundNode() =
  inherit GH_Component("Background", "background", "Background two BSDF nodes", "Shader", "Operation")

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "Color input", GH_ParamAccess.item, Color.Aquamarine) |> ignore
    mgr.AddNumberParameter("Strength", "S", "Background strength", GH_ParamAccess.item, 1.0) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("Background", "B", "Background output", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("dd68810b-0a0e-4c54-b08e-f46b41e79f32")

  override u.Icon = Icons.Output

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c1 = Utils.readColor(u, DA, 0, "Couldn't read input color")
    let f = Utils.readFloat(u, DA, 1, "Couldn't read strength")
    DA.SetData(0, Utils.createColor c1) |> ignore

  interface ICyclesNode with
    member u.NodeName = "background"

    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type AddClosureNode() =
  inherit GH_Component("Add", "add", "Add two BSDF nodes", "Shader", "Operation")

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Closure1", "1", "First closure input", GH_ParamAccess.item, Color.Coral) |> ignore
    mgr.AddColourParameter("Closure2", "2", "Second closure input", GH_ParamAccess.item, Color.Chocolate) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("Closure", "C", "Add of Closure1 and Closure2", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("f7929217-6fbb-4bd5-b74f-9763816dc38c")

  override u.Icon = Icons.Add

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c1 = Utils.readColor(u, DA, 0, "Couldn't read Closure 1")
    let c2 = Utils.readColor(u, DA, 1, "Couldn't read Closure 2")
    DA.SetData(0, Utils.createColor c1) |> ignore

  interface ICyclesNode with
    member u.NodeName = "add_closure"

    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type TransparentBsdf() =
  inherit GH_Component("Transparent BSDF", "transparent", "Transparent BSDF node for shader graph", "Shader", "BSDF")

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "transparent color", GH_ParamAccess.item, Color.Gray) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSDF", "BSDF", "Transparent BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("15f77ebf-ae59-4c49-80b1-362a7168f85f")

  override u.Icon = Icons.Diffuse

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read transparent color")

    DA.SetData(0, Utils.createColor c) |> ignore

  interface ICyclesNode with
    member u.NodeName = "transparent_bsdf"
    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type DiffuseBsdf() =
  inherit GH_Component("Diffuse BSDF", "diffuse", "Diffuse BSDF node for shader graph", "Shader", "BSDF")

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "diffuse color", GH_ParamAccess.item, Color.Gray) |> ignore
    mgr.AddNumberParameter("Roughness", "R", "Roughness of diffuse bsdf", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddVectorParameter("Normal", "N", "Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSDF", "BSDF", "Diffuse BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("e79bd4ac-1aa0-450d-aa4a-495cfeb8cb13")

  override u.Icon = Icons.Diffuse

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read diffuse color")

    DA.SetData(0, Utils.createColor c) |> ignore

  interface ICyclesNode with
    member u.NodeName = "diffuse_bsdf"
    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type VelvetBsdf() =
  inherit GH_Component("Velvet BSDF", "velvet", "Velvet BSDF node for shader graph", "Shader", "BSDF")

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "velvet color", GH_ParamAccess.item, Color.Gray) |> ignore
    mgr.AddNumberParameter("Sigma", "S", "Sigma of velvet bsdf", GH_ParamAccess.item, 1.0) |> ignore
    mgr.AddVectorParameter("Normal", "N", "Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSDF", "BSDF", "Velvet BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("d85aeb1d-e42f-43b6-86d6-ddf9cae5a633")

  override u.Icon = Icons.Diffuse

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read velvet color")

    DA.SetData(0, Utils.createColor c) |> ignore

  interface ICyclesNode with
    member u.NodeName = "velvet_bsdf"
    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type AnisotropicBsdf() =
  inherit GH_Component("Anisotropic BSDF", "anisotropic", "Anisotropic BSDF node for shader graph", "Shader", "BSDF")

  member val Distribution = Multiscatter_GGX with get, set

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "anisotropic color", GH_ParamAccess.item, Color.DarkBlue) |> ignore
    mgr.AddNumberParameter("Roughness", "Rgh", "Roughness of anisotropic bsdf", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Rotation", "R", "Rotation", GH_ParamAccess.item, 1.4) |> ignore
    mgr.AddVectorParameter("Normal", "N", "Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore
    mgr.AddVectorParameter("Tangent", "T", "Tangent", GH_ParamAccess.item, Vector3d.Zero) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSDF", "BSDF", "Anisotropic BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("bab1082a-4c74-4d07-9c63-d3f40a178c6a")

  override u.Icon = Icons.Glossy

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read anisotropic color")

    u.Message <- u.Distribution.ToString

    DA.SetData(0, Utils.createColor c) |> ignore

  override u.Write(writer:GH_IO.Serialization.GH_IWriter) =
    writer.SetString("Distribution", u.Distribution.ToString) |> ignore
    base.Write(writer)

  override u.Read(reader:GH_IO.Serialization.GH_IReader) =
    if reader.ItemExists("Distribution") then
      u.Distribution <-
        let d = Distribution.FromString (reader.GetString "Distribution")
        match d with | Option.None -> GGX | _ -> d.Value

    base.Read(reader)

  override u.AppendAdditionalComponentMenuItems(menu:ToolStripDropDown) =
    let appendMenu (it:Distribution) =
      GH_DocumentObject.Menu_AppendItem(menu, it.ToStringR, (fun _ _ -> u.Distribution <- it; u.ExpireSolution true),
        true, u.Distribution = it) |> ignore
    appendMenu Beckmann
    appendMenu GGX
    appendMenu Multiscatter_GGX
    appendMenu Ashihkmin_Shirley

  interface ICyclesNode with
    member u.NodeName = "anisotropic_bsdf"
    member u.GetXml node nickname inputs iteration =
      let x = (Utils.GetInputsXml (inputs, iteration)) + String.Format(" distribution=\"{0}\"", u.Distribution.ToString)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type RefractionBsdf() =
  inherit GH_Component("Refraction BSDF", "refraction", "Refraction BSDF node for shader graph", "Shader", "BSDF")

  member val Distribution = Sharp with get, set

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "refraction color", GH_ParamAccess.item, Color.DarkBlue) |> ignore
    mgr.AddNumberParameter("Roughness", "R", "Roughness of refraction bsdf", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("IOR", "I", "IOR of refraction bsdf", GH_ParamAccess.item, 1.4) |> ignore
    mgr.AddVectorParameter("Normal", "N", "Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSDF", "BSDF", "Refraction BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("e32dffe3-e31a-45a6-9d13-4fb0eefe4ff5")

  override u.Icon = Icons.Glossy

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read refraction color")

    u.Message <- u.Distribution.ToString

    DA.SetData(0, Utils.createColor c) |> ignore

  override u.Write(writer:GH_IO.Serialization.GH_IWriter) =
    writer.SetString("Distribution", u.Distribution.ToString) |> ignore
    base.Write(writer)

  override u.Read(reader:GH_IO.Serialization.GH_IReader) =
    if reader.ItemExists("Distribution") then
      u.Distribution <-
        let d = Distribution.FromString (reader.GetString "Distribution")
        match d with | Option.None -> Sharp | _ -> d.Value

    base.Read(reader)

  override u.AppendAdditionalComponentMenuItems(menu:ToolStripDropDown) =
    let appendMenu (it:Distribution) =
      GH_DocumentObject.Menu_AppendItem(menu, it.ToStringR, (fun _ _ -> u.Distribution <- it; u.ExpireSolution true),
        true, u.Distribution = it) |> ignore
    appendMenu Sharp
    appendMenu Beckmann
    appendMenu GGX

  interface ICyclesNode with
    member u.NodeName = "refraction_bsdf"
    member u.GetXml node nickname inputs iteration =
      let x = (Utils.GetInputsXml (inputs, iteration)) + String.Format(" distribution=\"{0}\"", u.Distribution.ToString)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type GlassBsdf() =
  inherit GH_Component("Glass BSDF", "glass", "Glass BSDF node for shader graph", "Shader", "BSDF")

  member val Distribution = Multiscatter_GGX with get, set

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "glass color", GH_ParamAccess.item, Color.DarkBlue) |> ignore
    mgr.AddNumberParameter("Roughness", "R", "Roughness of glass bsdf", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("IOR", "I", "IOR of glass bsdf", GH_ParamAccess.item, 1.4) |> ignore
    mgr.AddVectorParameter("Normal", "N", "Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSDF", "BSDF", "Glass BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("4db00f7b-fa70-4130-813d-a9f7cd193795")

  override u.Icon = Icons.Glossy

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read glass color")
    let f = Utils.readFloat(u, DA, 1, "Couldn't read glass roughness")

    u.Message <- u.Distribution.ToStringR

    DA.SetData(0, Utils.createColor c) |> ignore

  override u.Write(writer:GH_IO.Serialization.GH_IWriter) =
    writer.SetString("Distribution", u.Distribution.ToString) |> ignore
    base.Write(writer)

  override u.Read(reader:GH_IO.Serialization.GH_IReader) =
    if reader.ItemExists("Distribution") then
      u.Distribution <-
        let d = Distribution.FromString (reader.GetString "Distribution")
        match d with | Option.None -> Sharp | _ -> d.Value

    base.Read(reader)

  override u.AppendAdditionalComponentMenuItems(menu:ToolStripDropDown) =
    let appendMenu (it:Distribution) =
      GH_DocumentObject.Menu_AppendItem(menu, it.ToStringR, (fun _ _ -> u.Distribution <- it; u.ExpireSolution true),
        true, u.Distribution = it) |> ignore
    appendMenu Sharp
    appendMenu Beckmann
    appendMenu Multiscatter_GGX
    appendMenu GGX

  interface ICyclesNode with
    member u.NodeName = "glass_bsdf"
    member u.GetXml node nickname inputs iteration =
      let x = (Utils.GetInputsXml (inputs, iteration)) + String.Format(" distribution=\"{0}\"", u.Distribution.ToString)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type GlossyBsdf() =
  inherit GH_Component("Glossy BSDF", "glossy", "Glossy BSDF node for shader graph", "Shader", "BSDF")

  member val Distribution = Multiscatter_GGX with get, set

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "glossy color", GH_ParamAccess.item, Color.DarkBlue) |> ignore
    mgr.AddNumberParameter("Roughness", "R", "Roughness of glossy bsdf", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddVectorParameter("Normal", "N", "Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSDF", "BSDF", "Glossy BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("84e014b7-a76a-4b4f-8d37-25696cbebc04")

  override u.Icon = Icons.Glossy

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read glossy color")
    let f = Utils.readFloat(u, DA, 1, "Couldn't read glossy roughness")

    u.Message <- u.Distribution.ToStringR

    DA.SetData(0, Utils.createColor c) |> ignore

  override u.Write(writer:GH_IO.Serialization.GH_IWriter) =
    writer.SetString("Distribution", u.Distribution.ToString) |> ignore
    base.Write(writer)

  override u.Read(reader:GH_IO.Serialization.GH_IReader) =
    if reader.ItemExists("Distribution") then
      u.Distribution <-
        let d = Distribution.FromString (reader.GetString "Distribution")
        match d with | Option.None -> Sharp | _ -> d.Value

    base.Read(reader)

  override u.AppendAdditionalComponentMenuItems(menu:ToolStripDropDown) =
    let appendMenu (it:Distribution) =
      GH_DocumentObject.Menu_AppendItem(menu, it.ToStringR, (fun _ _ -> u.Distribution <- it; u.ExpireSolution true),
        true, u.Distribution = it) |> ignore
    appendMenu Sharp
    appendMenu Beckmann
    appendMenu GGX
    appendMenu Multiscatter_GGX
    appendMenu Ashihkmin_Shirley

  interface ICyclesNode with
    member u.NodeName = "glossy_bsdf"
    member u.GetXml node nickname inputs iteration =
      let x = (Utils.GetInputsXml (inputs, iteration)) + String.Format(" distribution=\"{0}\"", u.Distribution.ToString)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type EmissionBsdf() =
  inherit GH_Component("Emission BSDF", "emission",
    "Emission BSDF node for shader graph", "Shader", "BSDF")

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "emission color", GH_ParamAccess.item, Color.NavajoWhite) |> ignore
    mgr.AddNumberParameter("Strength", "S", "Roughness of emission bsdf", GH_ParamAccess.item, 0.0) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("Emission", "E", "Emission BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("aa365407-8e36-4400-b1a7-46cde5b21de6")

  override u.Icon = Icons.Emission

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read emission color")
    let f = Utils.readFloat(u, DA, 1, "Couldn't read emission strength")

    DA.SetData(0, Utils.createColor c) |> ignore

  interface ICyclesNode with
    member u.NodeName = "emission"
    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      "<" + (Utils.GetNodeXml node nickname x) + " />"

type SubsurfaceScatteringBsdf() =
  inherit GH_Component("Subsurface Scattering BSDF", "subsurface scattering",
    "Subsurface Scattering BSDF node for shader graph", "Shader", "BSDF")

  member val Falloff = Cubic with get, set

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Color", "C", "Color", GH_ParamAccess.item, Color.NavajoWhite) |> ignore
    mgr.AddNumberParameter("Scale", "Sc", "Scale", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Sharpness", "Sh", "Sharpness", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Texture Blur", "Tb", "Amount of blur", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddVectorParameter("Radius", "R", "Radius", GH_ParamAccess.item, new Vector3d(1.0, 1.0, 1.0)) |> ignore
    mgr.AddVectorParameter("Normal", "N", "Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore

  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSSRDF", "BSSRDF", "BSSRDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("8b3abb10-4593-4f34-b96f-cb4ed0f64ae7")

  override u.Icon = Icons.Emission

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read subsurface scattering color")

    DA.SetData(0, Utils.createColor c) |> ignore

  interface ICyclesNode with
    member u.NodeName = "subsurface_scattering"
    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      let ft = String.Format(" falloff=\"{0}\" ", u.Falloff.ToString)
      "<" + Utils.GetNodeXml node nickname (x + ft) + " />"

type DisneyBsdf() =
  inherit GH_Component("Disney BSDF", "disney bsdf",
    "Disney BSDF node for shader graph", "Shader", "BSDF")

  member val Distribution = Multiscatter_GGX with get, set

  override u.RegisterInputParams(mgr : GH_Component.GH_InputParamManager) =
    mgr.AddColourParameter("Base Color", "BC", "Color", GH_ParamAccess.item, Color.NavajoWhite) |> ignore
    mgr.AddColourParameter("Subsurface Color", "SC", "Color", GH_ParamAccess.item, Color.NavajoWhite) |> ignore
    mgr.AddNumberParameter("Metallic", "M", "Metallic", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Subsurface", "SS", "Subsurface", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddVectorParameter("Subsurface Radius", "SSR", "Radius", GH_ParamAccess.item, new Vector3d(1.0, 1.0, 1.0)) |> ignore
    mgr.AddNumberParameter("Specular", "Spec", "Specular", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Roughness", "Rgh", "Roughness", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Specular Tint", "SpecTint", "Specular Tint", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Anisotropic", "Ani", "Anisotropic", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Sheen", "Sh", "Sheen", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Sheen Tint", "ShT", "Sheen Tint", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Clearcoat", "CC", "Clearcoat", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Clearcoat Gloss", "CC gl", "Clearcoat Gloss", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("IOR", "IOR", "IOR", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Transparency", "Tra", "Transparency", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Refraction Roughness", "Refr Rgh", "Refraction Roughness", GH_ParamAccess.item, 0.0) |> ignore
    mgr.AddNumberParameter("Anisotropic Rotation", "AniRot", "Anisotropic Rotation", GH_ParamAccess.item, 0.0) |> ignore

    mgr.AddVectorParameter("Normal", "N", "Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore
    mgr.AddVectorParameter("Clearcoat Normal", "CN", "Clearcoat Normal", GH_ParamAccess.item, Vector3d.Zero) |> ignore
    mgr.AddVectorParameter("Tangent", "Tng", "Tangent", GH_ParamAccess.item, Vector3d.Zero) |> ignore


  override u.RegisterOutputParams(mgr : GH_Component.GH_OutputParamManager) =
    mgr.AddColourParameter("BSDF", "BSDF", "BSDF", GH_ParamAccess.item) |> ignore

  override u.ComponentGuid = new Guid("1480e1aa-7ad4-42e3-b626-62af011917a9")

  override u.Icon = Icons.Emission

  override u.SolveInstance(DA: IGH_DataAccess) =
    u.Message <- ""
    let c = Utils.readColor(u, DA, 0, "Couldn't read base color")

    DA.SetData(0, Utils.createColor c) |> ignore

  interface ICyclesNode with
    member u.NodeName = "uber_bsdf"
    member u.GetXml node nickname inputs iteration =
      let x = Utils.GetInputsXml (inputs, iteration)
      let ft = String.Format(" distribution=\"{0}\" ", u.Distribution.ToString)
      "<" + Utils.GetNodeXml node nickname (x + ft) + " />"

