#load "Embed.fsx"

open System.Text.Json
open System.Net
open System.Net.Http
open System.Text
open System.IO

let getArgs () : string list =

#if COMPILED

    System.Environment.GetCommandLineArgs() |> Array.skip 1 |> Array.toList
#else
    fsi.CommandLineArgs |> Array.skip 1 |> Array.toList
#endif

let version = "DEV"
let defaultTemplate = "template.html"
let defaultOutput = "conversion.html"

type Config =
    {
        InputFile: string
        OutputFile: string
        TemplateFile: string
        ShowHelp: bool
        ShowVersion: bool
    }

    static member Default =
        {
            InputFile = ""
            OutputFile = defaultOutput
            TemplateFile = ""
            ShowHelp = false
            ShowVersion = false
        }

let normalizeFlag (arg: string) =
    match arg with
    | "-i"
    | "--input" -> Some "input"
    | "-o"
    | "--output" -> Some "output"
    | "-t"
    | "--template" -> Some "template"
    | "-h"
    | "--help" -> Some "help"
    | _ -> None

let rec parseConfig (argv: string list, parsed: Config) =
    match argv with
    | [] -> parsed
    | flag :: value :: rest ->
        match normalizeFlag flag with

        | Some "input" -> parseConfig (rest, { parsed with InputFile = value })
        | Some "output" -> parseConfig (rest, { parsed with OutputFile = value })
        | Some "template" -> parseConfig (rest, { parsed with TemplateFile = value })
        | Some "help" -> parseConfig (rest, { parsed with ShowHelp = true })
        | _ -> parseConfig (value :: rest, parsed)
    | flag :: rest ->
        match normalizeFlag flag with
        | Some "help" -> { parsed with ShowHelp = true }
        | _ -> parsed

let private httpClient =
    let c = new HttpClient()

    c.DefaultRequestHeaders.Accept.ParseAdd "application/vnd.github.v3+json"
    |> ignore

    c.DefaultRequestHeaders.UserAgent.ParseAdd "MDCAT-FS/1.0"
    c

let buildMarkdownPayload (markdown: string) : byte array =
    use stream = new MemoryStream()
    use writer = new Utf8JsonWriter(stream)
    writer.WriteStartObject()
    writer.WriteString("text", markdown)
    writer.WriteEndObject()
    writer.Flush()

    stream.ToArray()

let convertMarkdownToHTML (markdown: string) =

    task {
        try
            let payload = buildMarkdownPayload markdown
            use req = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/markdown")
            use content = new ByteArrayContent(payload)
            content.Headers.ContentType <- Headers.MediaTypeHeaderValue("application/json")
            req.Content <- content
            use! resp = httpClient.SendAsync req

            let! body = resp.Content.ReadAsByteArrayAsync()

            match resp.StatusCode with
            | HttpStatusCode.OK -> return Ok(Encoding.UTF8.GetString body)
            | _ -> return Error $"api failed, status: {int resp.StatusCode}, body: {Encoding.UTF8.GetString body}"
        with ex ->
            return Error $"calling api, err={ex}"
    }

let convertMarkdown (config: Config) =
    if not (File.Exists config.InputFile) then
        stdout.WriteLine "FILE NOT FOUND"

    let content = File.ReadAllBytes config.InputFile
    let htmlResult = convertMarkdownToHTML (Encoding.UTF8.GetString(content))

    let templateContent =
        match htmlResult.Result with
        | Ok content -> content
        | Error msg ->
            stdout.WriteLine msg
            exit 114514

    let html =
        Embed.TemplateHtml.Replace("{{MD_TITLE}}", config.InputFile).Replace("{{MD_HTML}}", templateContent)

    File.WriteAllText(config.OutputFile, html)
    stdout.WriteLine "Conversion complete"

let config = parseConfig (getArgs (), Config.Default)
convertMarkdown config
