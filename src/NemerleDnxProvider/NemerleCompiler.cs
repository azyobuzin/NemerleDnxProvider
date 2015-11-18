using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.PlatformAbstractions;
using Nemerle.Collections;
using Nemerle.Compiler;

namespace NemerleDnxProvider
{
    class NemerleCompiler
    {
        public IMetadataProjectReference CompileProject(
            CompilationProjectContext projectContext,
            IEnumerable<IMetadataReference> incomingReferences,
            IEnumerable<ISourceReference> incomingSourceReferences)
        {
            var name = projectContext.Target.Name;

            var outputDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
            var options = new CompilationOptions
            {
                TargetIsLibrary = true,
                ThrowOnError = true,
                IgnoreConfusion = true,
                EmitDebug = true,
                // Platform = projectContext.CompilerOptions.Platform,
                ReferencedLibraries = incomingReferences.Select(x => MetadataReferenceToPath(x, outputDir)).NToList(),
                OutputPath = outputDir.FullName,
                OutputFileName = Path.Combine(outputDir.FullName, name + ".dll")
                // XmlDocOutputFileName = Path.Combine(outputDir.FullName, name + ".xml")
            };
            var sourceFiles = projectContext.Files.SourceFiles
                .Concat(incomingSourceReferences.OfType<ISourceFileReference>().Select(x => x.Path))
                .ToArray();
            options.Sources = Array.ConvertAll(sourceFiles, x => new FileSource(x, options.Warnings)).AsList<ISource>();
            foreach (var symbol in projectContext.CompilerOptions.Defines)
                options.DefineConstant(symbol);
            if (projectContext.CompilerOptions.Optimize.GetValueOrDefault())
            {
                options.Optimize = new Hashtable<string, int>
                {
                    ["tuple"] = 1,
                    ["propagate"] = 1,
                    ["unify"] = 1,
                    ["print"] = 0
                };
            }

            var diagnostics = new List<DiagnosticMessage>();
            var manager = new ManagerClass(options);
            manager.ErrorOccured += (loc, msg) => diagnostics.Add(new DiagnosticMessage(
                null, msg, loc.File, DiagnosticMessageSeverity.Error, loc.Line, loc.Column));
            manager.WarningOccured += (loc, msg) => diagnostics.Add(new DiagnosticMessage(
                null, msg, loc.File, DiagnosticMessageSeverity.Warning, loc.Line, loc.Column));
            manager.MessageOccured += (loc, msg) => diagnostics.Add(new DiagnosticMessage(
                 null, msg, loc.File, DiagnosticMessageSeverity.Info, loc.Line, loc.Column));
            manager.InitOutput(new StringWriter());

            // Start compile
            bool success;
            try
            {
                manager.Run();
                success = true;
            }
            catch
            {
                success = false;
            }

            return new NemerleProjectReference(
                projectContext.Target.Name,
                projectContext.ProjectFilePath,
                outputDir,
                new DiagnosticResult(success, diagnostics),
                Array.ConvertAll(sourceFiles, x => new SourceFileReference(x))
            );
        }

        private string MetadataReferenceToPath(IMetadataReference reference, DirectoryInfo outputDir)
        {
            var projectRef = reference as IMetadataProjectReference;
            if (projectRef != null)
            {
                projectRef.EmitAssembly(outputDir.FullName);
                return Path.Combine(outputDir.FullName, projectRef.Name + ".dll");
            }

            var fileRef = reference as IMetadataFileReference;
            if (fileRef != null)
                return fileRef.Path;

            throw new NotSupportedException($"Unsupported type '{reference.GetType()}'.");
        }
    }
}
