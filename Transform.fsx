open System.IO

let templatePath = "template.html"
let html = File.ReadAllText templatePath

if html.Contains "\"\"\"" then
    failwith "模板包含 \"\"\" ,无法用三引号字符串嵌入"

let output =
    "module Embed\n\n[<Literal>]\nlet TemplateHtml = \"\"\"\n" + html + "\n\"\"\"\n"

File.WriteAllText("Embed.fsx", output)
printfn "已生成 Embed.fsx (%d 字符)" output.Length
