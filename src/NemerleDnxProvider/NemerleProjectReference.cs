using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Runtime;

namespace NemerleDnxProvider
{
    public class NemerleProjectReference : IMetadataProjectReference
    {
        public NemerleProjectReference(
            string name,
            string projectPath,
            DirectoryInfo outputDir,
            DiagnosticResult diagnostics,
            IList<ISourceReference> sources)
        {
            this.Name = name;
            this.ProjectPath = projectPath;
            this.outputDir = outputDir;
            this.diagnostics = diagnostics;
            this.sources = sources;
        }

        private readonly DirectoryInfo outputDir;
        private readonly DiagnosticResult diagnostics;
        private readonly IList<ISourceReference> sources;

        public string Name { get; }

        public string ProjectPath { get; }

        public DiagnosticResult EmitAssembly(string outputPath)
        {
            Directory.CreateDirectory(outputPath);
            foreach (var f in this.outputDir.EnumerateFiles())
            {
                if (f.Name.StartsWith(this.Name, StringComparison.Ordinal))
                    f.CopyTo(Path.Combine(outputPath, f.Name), true);
            }
            return this.diagnostics;
        }

        private string GetAssemblyFileName()
        {
            return Path.Combine(this.outputDir.FullName, this.Name + ".dll");
        }

        public void EmitReferenceAssembly(Stream stream)
        {
            if (!this.diagnostics.Success)
                throw new NemerleCompilationException(this.diagnostics.Diagnostics);

            using (var s = File.OpenRead(this.GetAssemblyFileName()))
                s.CopyTo(stream);
        }

        public DiagnosticResult GetDiagnostics() => this.diagnostics;

        public IList<ISourceReference> GetSources() => this.sources;

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            if (!this.diagnostics.Success)
                throw new NemerleCompilationException(this.diagnostics.Diagnostics);

            return loadContext.LoadFile(this.GetAssemblyFileName());
        }
    }
}
