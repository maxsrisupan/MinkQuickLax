using System.Text;

namespace ManualBuilder.Tests;

/// <summary>A throwaway manual folder with a minimal template, deleted after the test.</summary>
internal sealed class ManualFixture : IDisposable
{
    public ManualFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), "ManualBuilder.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Path.Combine(Directory, "template"));
        Write("template/index.html", "<html lang=\"{{lang}}\"><style>{{fontFaces}}{{css}}</style><nav>{{toc}}</nav><main>{{content}}</main><script>const I={{index}};const T={{strings}};{{script}}</script><a href=\"{{otherLang}}.html\">{{s:otherLanguage}}</a></html>");
        Write("template/manual.css", "body{}");
        Write("template/search.js", "// search");
        Write("template/strings.json", """{"th":{"otherLanguage":"English","untranslated":"ยังไม่ได้แปล"},"en":{"otherLanguage":"ไทย","untranslated":"Not translated yet"}}""");
        FontsDirectory = Path.Combine(Directory, "fonts");
        System.IO.Directory.CreateDirectory(FontsDirectory);
        foreach (var font in new[] { "IBMPlexSansThai-Regular.ttf", "IBMPlexSansThai-SemiBold.ttf", "ChakraPetch-SemiBold.ttf" })
        {
            File.WriteAllBytes(Path.Combine(FontsDirectory, font), [1, 2, 3]);
        }
    }

    public string Directory { get; }

    public string FontsDirectory { get; }

    public string Write(string relativePath, string content)
    {
        var path = Path.Combine(Directory, relativePath);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    public ManualProject Load(string? topicsFile = null) => ManualProject.Load(Directory, topicsFile);

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is harmless.
        }
    }
}
