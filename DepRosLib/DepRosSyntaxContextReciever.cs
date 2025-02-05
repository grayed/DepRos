using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace DepRos
{
    internal class DepRosSyntaxContextReciever : ISyntaxContextReceiver
    {
        public List<ClassData> ClassesToProceed { get; } = new List<ClassData>();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context) {
            if (context.Node is TypeDeclarationSyntax typeNode) {
                var classData = new ClassData(context, typeNode);
                if (classData.Properties.Count > 0)
                    this.ClassesToProceed.Add(classData);
            }
        }
    }
}
