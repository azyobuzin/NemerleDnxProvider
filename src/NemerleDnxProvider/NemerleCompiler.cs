using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime;
using Nemerle.Collections;
using Nemerle.Compiler;

namespace NemerleDnxProvider
{
    class NemerleCompiler
    {
        private readonly List<DirectoryInfo> directoriesToDelete = new List<DirectoryInfo>();

        private DirectoryInfo CreateTempDirectory()
        {
            var tmpDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            this.directoriesToDelete.Add(tmpDir);
            return tmpDir;
        }

        private void Clean()
        {
            foreach (var x in this.directoriesToDelete)
                x.Delete(true);
        }

        public IMetadataProjectReference CompileProject(
            CompilationProjectContext projectContext,
            IEnumerable<IMetadataReference> incomingReferences,
            IEnumerable<ISourceReference> incomingSourceReferences)
        {
            var name = projectContext.Target.Name;

            var outputDir = this.CreateTempDirectory();
            var sw = new StringWriter();
            var options = new CompilationOptions
            {
                TargetIsLibrary = true,
                ReferencedLibraries = incomingReferences.Select(MetadataReferenceToPath).NToList(),
                OutputPath = outputDir.FullName,
                OutputFileName = Path.Combine(outputDir.FullName, name + ".dll"),
                // XmlDocOutputFileName = name + ".xml"
                // XmlDoc occurs NullReferenceException
            };
            var sourceFiles = projectContext.Files.SourceFiles
                .Concat(incomingSourceReferences.OfType<ISourceFileReference>().Select(x => x.Path))
                .ToArray();
            options.Sources = Array.ConvertAll(sourceFiles, x => new FileSource(x, options.Warnings)).AsList<ISource>();

            var manager = new ManagerClass(options);
            manager.InitOutput(sw);

            // Start compile
            manager.Run();

            Console.WriteLine(sw.ToString());
            var diagnostics = CreateDiagnosticMessages(sw.ToString()).Where(x => x != null).ToArray();

            var files = new Dictionary<string, byte[]>();
            foreach (var f in outputDir.EnumerateFiles())
            {
                if (f.Name.StartsWith(name, StringComparison.Ordinal))
                    files.Add(f.Name, File.ReadAllBytes(f.FullName));
            }

            //this.Clean();

            return new NemerleProjectReference(
                projectContext.Target.Name,
                projectContext.ProjectFilePath,
                files,
                new DiagnosticResult(!diagnostics.Any(x => x.Severity == DiagnosticMessageSeverity.Error), diagnostics),
                Array.ConvertAll(sourceFiles, x => new SourceFileReference(x))
            );
        }

        private string MetadataReferenceToPath(IMetadataReference reference)
        {
            var projectRef = reference as IMetadataProjectReference;
            if (projectRef != null)
            {
                var tmpDir = this.CreateTempDirectory();
                projectRef.EmitAssembly(tmpDir.FullName);
                return Path.Combine(tmpDir.FullName, projectRef.Name + ".dll");
            }

            var fileRef = reference as IMetadataFileReference;
            if (fileRef != null)
                return fileRef.Path;

            throw new NotSupportedException($"Unsupported type '{reference.GetType()}'.");
        }

        private sealed class Location
        {
            public string File;
            public int StartLine;
            public int StartPos;
            public int EndLine;
            public int EndPos;

            public static Location Parse(int tagPos, string text)
            {
                var str = text.Substring(0, tagPos);
                if (string.IsNullOrEmpty(str))
                    return new Location();
                // Path can contain ':'. We should skip it...
                var dir = str.StartsWith(":", StringComparison.Ordinal) ? "" : Path.GetDirectoryName(str);
                // Find first location separator (it's a end of path)
                var locIndex = str.IndexOf(':', dir.Length);
                var path = (locIndex <= 0) ? dir : str.Substring(0, locIndex);
                var locStr = str.Substring(locIndex);
                var parts = locStr.Trim().Trim(':').Split(':');
                switch (parts.Length)
                {
                    case 2:
                        var line = int.Parse(parts[0]);
                        var pos = int.Parse(parts[1]);
                        return new Location
                        {
                            File = path,
                            StartLine = line,
                            StartPos = pos,
                            EndLine = line,
                            EndPos = pos + 1
                        };
                    case 4:
                        return new Location
                        {
                            File = path,
                            StartLine = int.Parse(parts[0]),
                            StartPos = int.Parse(parts[1]),
                            EndLine = int.Parse(parts[2]),
                            EndPos = int.Parse(parts[3])
                        };
                    default:
                        return new Location
                        {
                            File = path
                        };
                }
            }
        }

        private static DiagnosticMessage LogEventsFromTextOutput(string singleLine)
        {
            int tagPos;
            string tag;

            if ((tagPos = singleLine.IndexOf(tag = "error:", StringComparison.Ordinal)) >= 0)
            {
                var loc = Location.Parse(tagPos, singleLine);
                var msg = singleLine.Substring(tagPos + tag.Length + 1);
                return new DiagnosticMessage(null, msg, singleLine, loc.File, DiagnosticMessageSeverity.Error, loc.StartLine, loc.StartPos, loc.EndLine, loc.EndPos);
            }
            else if ((tagPos = singleLine.IndexOf(tag = "warning:", StringComparison.Ordinal)) >= 0)
            {
                var loc = Location.Parse(tagPos, singleLine);
                var msg = singleLine.Substring(tagPos + tag.Length + 1);

                return new DiagnosticMessage(null, msg, singleLine, loc.File, DiagnosticMessageSeverity.Warning, loc.StartLine, loc.StartPos, loc.EndLine, loc.EndPos);
            }
            else if ((tagPos = singleLine.IndexOf(tag = "debug:", StringComparison.Ordinal)) >= 0)
            {
                var loc = Location.Parse(tagPos, singleLine);
                var msg = singleLine.Substring(tagPos + tag.Length + 1);

                return new DiagnosticMessage(null, msg, singleLine, loc.File, DiagnosticMessageSeverity.Info, loc.StartLine, loc.StartPos, loc.EndLine, loc.EndPos);
            }
            else if ((tagPos = singleLine.IndexOf(tag = "hint:", StringComparison.Ordinal)) >= 0)
            {
                var loc = Location.Parse(tagPos, singleLine);
                var msg = singleLine.Substring(tagPos);

                return new DiagnosticMessage(null, msg, singleLine, loc.File, DiagnosticMessageSeverity.Info, loc.StartLine, loc.StartPos, loc.EndLine, loc.EndPos);
            }
            else
            {
                // Do nothing
                return null;
            }
        }

        private static IEnumerable<DiagnosticMessage> CreateDiagnosticMessages(string output)
        {
            using (var sr = new StringReader(output))
                yield return LogEventsFromTextOutput(sr.ReadLine());
        }
    }
}
