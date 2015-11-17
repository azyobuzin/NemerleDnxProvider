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
            IReadOnlyDictionary<string, byte[]> files,
            DiagnosticResult diagnostics,
            IList<ISourceReference> sources)
        {
            this.Name = name;
            this.ProjectPath = projectPath;
            this.files = files;
            this.diagnostics = diagnostics;
            this.sources = sources;
        }

        private readonly IReadOnlyDictionary<string, byte[]> files;
        private readonly DiagnosticResult diagnostics;
        private readonly IList<ISourceReference> sources;

        public string Name { get; }

        public string ProjectPath { get; }

        public DiagnosticResult EmitAssembly(string outputPath)
        {
            Directory.CreateDirectory(outputPath);
            foreach (var kvp in this.files)
            {
                File.WriteAllBytes(Path.Combine(outputPath, kvp.Key), kvp.Value);
            }
            return this.diagnostics;
        }

        public void EmitReferenceAssembly(Stream stream)
        {
            var b = this.files[this.Name + ".dll"];
            stream.Write(b, 0, b.Length);
        }

        public DiagnosticResult GetDiagnostics() => this.diagnostics;

        public IList<ISourceReference> GetSources() => this.sources;

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            using (var assemblyStream = new MemoryStream(this.files[this.Name + ".dll"]))
            {
                byte[] pdb;
                if (this.files.TryGetValue(this.Name + ".pdb", out pdb))
                {
                    using (var pdbStream = new MemoryStream(pdb))
                        return loadContext.LoadStream(assemblyStream, pdbStream);
                }

                return loadContext.LoadStream(assemblyStream, null);
            }
        }
    }
}
