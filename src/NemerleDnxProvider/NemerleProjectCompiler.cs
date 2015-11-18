using System;
using System.Collections.Generic;
using Microsoft.Extensions.CompilationAbstractions;

namespace NemerleDnxProvider
{
    public class NemerleProjectCompiler : IProjectCompiler
    {
        public IMetadataProjectReference CompileProject(CompilationProjectContext projectContext, Func<LibraryExport> referenceResolver, Func<IList<ResourceDescriptor>> resourcesResolver)
        {
            var export = referenceResolver();
            if (export == null)
            {
                return null;
            }

            var incomingReferences = export.MetadataReferences;
            var incomingSourceReferences = export.SourceReferences;

            try
            {
                return new NemerleCompiler().CompileProject(projectContext, incomingReferences, incomingSourceReferences);
            }
            catch (Exception ex)
            {
                // make easy to debug
                Console.WriteLine(ex);
                throw;
            }
        }
    }
}
