#load ".paket/load/Suave.fsx"
#r "packages/FSharp.Formatting/lib/netstandard2.1/FSharp.Formatting.Common.dll"
#r "packages/FSharp.Formatting/lib/netstandard2.1/FSharp.Formatting.Markdown.dll"
open System
open System.IO
open FSharp.Formatting.Markdown
open System.Diagnostics
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let cprint clr fmt =
  Printf.kprintf (fun s ->
    let old = Console.ForegroundColor
    Console.ForegroundColor <- clr
    Console.WriteLine(s)
    Console.ForegroundColor <- old) fmt


let splitAt f l =
  let rec loop res acc = function
    | x::xs when f x ->
        if List.isEmpty acc then loop res [x] xs
        else loop (List.rev acc::res) [x] xs
    | x::xs ->
        loop res (x::acc) xs
    | [] ->
        let res = if List.isEmpty acc then res else (List.rev acc::res)
        List.rev res
  loop [] [] l

//splitAt (fun n -> n%3=0) [0..10]

// --------------------------------------------------------------------------------------
// Slides parsing and processing
// --------------------------------------------------------------------------------------

let (|As|) v x = v, x

let parseOptions pars =
  pars
  |> List.map (function
    | [Span([Literal(lit, _)], _)] ->
        let col = lit.IndexOf(':')
        lit.Substring(0, col).Trim(),
        lit.Substring(col+1).Trim()
    | li ->
        failwithf "Option is not a simple literal")
  |> Map.ofSeq

let (|LongHorizontalRule|_|) = function
  | HorizontalRule(c, Some r) when r.EndColumn > 40 -> Some(c)
  | _ -> None

let extractSlides pars =
  let rec loop opts slide group acc pars =
    match pars with
    | LongHorizontalRule('*') ::
      ListBlock(_, items, _) :: pars
    | LongHorizontalRule('*') :: As [] (items, pars) ->
        let group = if slide <> [] then (parseOptions opts, List.rev slide)::group else group
        let acc = if group <> [] then List.rev group::acc else acc
        loop items [] [] acc pars
    | LongHorizontalRule _ ::
      ListBlock(_, items, _) :: pars
    | LongHorizontalRule _ :: As [] (items, pars) ->
        let group = if slide <> [] then (parseOptions opts, List.rev slide)::group else group
        loop items [] group acc pars
    | par :: pars ->
        loop opts (par::slide) group acc pars
    | [] ->
        let group = if slide <> [] then (parseOptions opts, List.rev slide)::group else group
        let acc = if group <> [] then List.rev group::acc else acc
        List.rev acc
  loop [] [] [] [] pars

// --------------------------------------------------------------------------------------
// Template helpers
// --------------------------------------------------------------------------------------

let (|Headings|) = function
  | (Heading(1, _, _) as h1) :: (Heading(2, _, _) as h2) :: body -> ([h1;h2], body)
  | (Heading(1, _, _) as h1) :: body -> ([h1], body)
  | ps -> ([], ps)


let rec replaceIcons = function
  | MarkdownPatterns.ParagraphNested(p, ps) ->
      MarkdownPatterns.ParagraphNested(p, List.map (List.map replaceIcons) ps)
  | MarkdownPatterns.ParagraphSpans(s, ss) ->
      let ss = ss |> List.map (function
        | Emphasis([Literal(fa, _)], _) when fa.StartsWith "fa-"->
            Literal($"<i class='fa {fa}'></i>", None)
        | s -> s)
      MarkdownPatterns.ParagraphSpans(s, ss)
  | p -> p

let fragmentByHr pars =
  let groups =
    pars |> splitAt (function HorizontalRule _ -> true | _ -> false)
         |> List.map (List.filter (function HorizontalRule _ -> false | _ -> true))
  [ yield InlineHtmlBlock("<div class=\"body\">", None, None)
    for i, group in Seq.indexed groups do
      let cls = if i <> 0 then " class=\"fragment\"" else ""
      yield InlineHtmlBlock($"<div{cls}>", None, None)
      yield! group
      yield InlineHtmlBlock("</div>", None, None)
    yield InlineHtmlBlock("</div>", None, None) ]

let wrapCitations = List.collect (function
  | Paragraph([Literal(t, _)], _) as p when t.StartsWith("(") && t.EndsWith(")") ->
      [ yield InlineHtmlBlock("<div class=\"citation\">", None, None)
        yield p
        yield InlineHtmlBlock("</div>", None, None) ]
  | p -> [p])

// --------------------------------------------------------------------------------------
// Templates
// --------------------------------------------------------------------------------------

let iconsTemplate = function
  | Headings(hs, [ListBlock(Unordered, items, _)]) ->
      let items = items |> List.map (function
        | [Paragraph(Emphasis([Literal(fa, _)], _)::body, _)]
        | [Span(Emphasis([Literal(fa, _)], _)::body, _)] ->
            let nbody = body |> List.collect (function
              | HardLineBreak _ ->
                  [ HardLineBreak None
                    Literal("<i class=\"placeholder\"></i>", None) ]
              | span -> [span])
            [Span(Literal($"<i class='fa {fa}'></i>", None)::nbody, None)]
        | item -> failwithf "Icon template item did not start with *fa-xxx*")
      hs @ [ListBlock(Unordered, items, None)]
  | _ -> failwith "Unexpected slide structure in icons template"

let imageOrCodeTemplate anim = function
  | (CodeBlock _ as par)::pars
  | (Paragraph([DirectImage _], _) as par)::pars ->
      let pars = pars |> List.map replaceIcons
      let cls = if anim then "body1 fragment" else "body1"
      [ InlineHtmlBlock("<div class=\"" + cls + "\">", None, None)
        par
        InlineHtmlBlock("</div><div class=\"body2\">", None, None)
        yield! fragmentByHr pars |> wrapCitations
        InlineHtmlBlock("</div>", None, None) ]
  | _ -> failwith "Image template did not start with an image"

let listsTemplate = function
  | Headings(hs, (CodeBlock _ as par)::pars)
  | Headings(hs, (Paragraph([DirectImage _], _) as par)::pars) ->
      [ yield InlineHtmlBlock($"<div class=\"body\">", None, None)
        yield! hs
        match par with
        | Paragraph([DirectImage(_, img, _, _)], _) ->
            yield InlineHtmlBlock($"<img src=\"{img}\"/>", None, None)
        | CodeBlock _ ->
            yield InlineHtmlBlock("<div class=\"decor\">", None, None)
            yield par
            yield InlineHtmlBlock("</div>", None, None)
        | _ -> failwith "Lists template expected code block or image"
        let groups = pars |> splitAt (function Heading _ -> true | _ -> false)
        for i, group in Seq.indexed groups do
          let cls = if i <> 0 then " class=\"fragment\"" else ""
          yield InlineHtmlBlock($"<div{cls}>", None, None)
          yield! group
          yield InlineHtmlBlock("</div>", None, None)
        yield InlineHtmlBlock($"</div>", None, None) ]
  | _ -> failwith "Lists template did not start with code block or an image"

let contentTemplate pars =
  let (Headings(hs, pars)) = pars |> List.map replaceIcons
  hs @ fragmentByHr pars

let subtitleTemplate pars =
  [ InlineHtmlBlock("<div class=\"body\">", None, None)
    yield! pars
    InlineHtmlBlock("</div>", None, None) ]

let titleTemplate pars =
  match pars |> splitAt (function HorizontalRule _ -> true | _ -> false) with
  | [top; _::bot] ->
      [ yield! top
        InlineHtmlBlock("<div class=\"body\">", None, None)
        yield! bot
        InlineHtmlBlock("</div>", None, None) ]
  | _ -> failwith "Failed to split title slide into two parts using HR"

let templates =
  [ "title", titleTemplate >> Some
    "subtitle", subtitleTemplate >> Some
    "icons", iconsTemplate >> Some
    "largeicons", iconsTemplate >> Some
    "lists", listsTemplate >> Some
    "imageanim", imageOrCodeTemplate true >> Some
    "codeanim", imageOrCodeTemplate true >> Some
    "image", imageOrCodeTemplate false >> Some
    "code", imageOrCodeTemplate false >> Some
    "content", contentTemplate >> Some
    "default", Some
    "notes", fun _ -> None ]
  |> dict

// --------------------------------------------------------------------------------------
// File processing
// --------------------------------------------------------------------------------------

// let fn, out = "slides/index.md", "output/index.html"
let logo = """<img src="img/cuni.png" class="mfflogo" />"""

let processFile fn out =
  let doc = Markdown.Parse(File.ReadAllText(fn))
  let transformSlide (opts, slide) =
    templates.[Map.find "template" opts] slide

  let options, pars =
    match doc.Paragraphs with
    | ListBlock(_, items, _)::pars -> parseOptions items, pars
    | pars -> Map.empty, pars

  let slides =
    extractSlides pars
  let stats =
    slides
    |> Seq.concat |> Seq.countBy (fun (opts, _) -> opts.["template"])
    |> Map.ofSeq

  let counter =
    let mutable n = 0
    (fun () -> n <- n + 1; n)

  let slidesHtml =
    slides
    |> List.map (List.choose (fun (opts, slide) ->
      match transformSlide (opts, slide) with
      | Some nslide ->
          let slideno = counter ()
          let html = Markdown.ToHtml(MarkdownDocument(nslide, doc.DefinedLinks))
          let cls = opts.TryFind("class") |> Option.defaultValue ""
          let bg = opts.TryFind("background-color") |> Option.map (fun clr ->
            $"""data-background-color="{clr}" """)|> Option.defaultValue ""
          let sty = opts.TryFind("style") |> Option.map (fun sty ->
            let reg = System.Text.RegularExpressions.Regex("[^}]+{[^}]*}")
            let sty =
              [ for m in reg.Matches(sty) -> $".slide{slideno} {m.Value}" ]
              |> String.concat " "
            $"<style>{sty}</style>") |> Option.defaultValue ""
          let cls = cls + $" slide{slideno}"
          Some($"""<section class="{opts.["template"]} {cls}"{bg}>{html}{sty}{logo}</section>""")
      | _ -> None))
    |> List.filter (List.isEmpty >> not)
    |> List.map (fun slides -> $"""<section>{String.concat "" slides}</section>""")
    |> String.concat ""

  let templ = File.ReadAllText("slides/template.html")
  let page = templ.Replace("{{BODY}}", slidesHtml).Replace("{{TITLE}}", options.["title"])
  File.WriteAllText(out, page)
  stats

let copyStatic () =
  let rec loop root =
    for dir in Directory.GetDirectories(root) do
      Directory.CreateDirectory(dir.Replace("slides","output")) |> ignore
      loop dir
      for f in Directory.GetFiles(dir) do
        cprint ConsoleColor.Gray "[INFO] Copying file: %s" f
        File.Copy(f, f.Replace("slides", "output"))
  for dir in Directory.GetDirectories("slides") do
    if Directory.Exists(dir.Replace("slides","output")) then
      Directory.Delete(dir.Replace("slides","output"), true)
  loop "slides"

let updateSlides () =
  if not(Directory.Exists("output")) then
    Directory.CreateDirectory("output") |> ignore
  let mutable stats = []
  for fn in Directory.GetFiles("slides", "*.md") do
    cprint ConsoleColor.Gray "[INFO] Processing slide: %s" fn
    let out = $"output/{Path.GetFileNameWithoutExtension(fn)}.html"
    let res = processFile fn out
    stats <- stats @ [Path.GetFileNameWithoutExtension(fn), res]
  copyStatic ()
  for sn, info in stats do
    let info = info |> Map.toList
    let ttls, conts =
      info |> Seq.filter (fun (k, _) -> k.Contains "title") |> Seq.sumBy snd,
      info |> Seq.filter (fun (k, _) -> not (k.Contains "title")) |> Seq.sumBy snd
    cprint ConsoleColor.Cyan "[STATS] Slide: %s (%d + %d)" sn ttls conts
    for k, v in info do
      cprint ConsoleColor.DarkCyan "         - %s (%d)" k v

// --------------------------------------------------------------------------------------
//
// --------------------------------------------------------------------------------------


let refreshEvent = Event<unit>()

let fsw = new FileSystemWatcher("slides")
fsw.NotifyFilter <- NotifyFilters.LastWrite
fsw.Changed.Add(fun e ->
  try
    cprint ConsoleColor.White "[INFO] Updating slides"
    updateSlides()
    refreshEvent.Trigger()
    cprint ConsoleColor.White "[INFO] Update completed"
  with e ->
    cprint ConsoleColor.Red "[ERROR] %s" e.Message
)
fsw.EnableRaisingEvents <- true
//fsw.Dispose()




open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

let ws (webSocket : WebSocket) (context: Suave.Http.HttpContext) = socket {
  while true do
    do! SocketOp.ofAsync (Async.AwaitEvent refreshEvent.Publish)
    let msg = System.Text.Encoding.ASCII.GetBytes("refresh!") |> ByteSegment
    do! webSocket.send Text msg true }

open Suave
open Suave.Filters
open Suave.Operators

let app : WebPart =
  choose [
    path "/websocket" >=> handShake ws
    Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
    >=> Writers.setHeader "Pragma" "no-cache"
    >=> Writers.setHeader "Expires" "0"
    >=> choose [
      path "/" >=> Files.file "./output/index.html"
      Files.browseHome
      RequestErrors.NOT_FOUND "Page not found." ]
  ]

let config =
  { defaultConfig with
      homeFolder = Some (Path.GetFullPath "./output")
      bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" 8082 ]
      compressedFilesFolder = Some(Path.GetTempPath()) }

let _, start = startWebServerAsync config app
//Async.Start(start)
Process.Start(ProcessStartInfo("http://localhost:8082/", UseShellExecute=true))
updateSlides()
Async.RunSynchronously(start)
