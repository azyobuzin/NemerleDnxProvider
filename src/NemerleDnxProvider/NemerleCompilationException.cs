using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.PlatformAbstractions;

namespace NemerleDnxProvider
{
    public class NemerleCompilationException : Exception, ICompilationException
    {
        public NemerleCompilationException(IEnumerable<DiagnosticMessage> messages)
            : base(CreateMessage(messages))
        {
            this.CompilationFailures = messages
                .GroupBy(x => x.SourceFilePath, StringComparer.OrdinalIgnoreCase)
                .Select(x => new CompilationFailure(x.Key, x))
                .ToArray();
        }

        public IEnumerable<CompilationFailure> CompilationFailures { get; }

        private static string CreateMessage(IEnumerable<DiagnosticMessage> messages)
        {
            return "Nemerle Compilation Error\n" + string.Join("\n",
                messages.Where(x => x.Severity == DiagnosticMessageSeverity.Error)
                .Select(x => x.FormattedMessage));
        }
    }
}
