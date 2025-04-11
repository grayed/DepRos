using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DepRos
{
    [Generator]
    public class DepRosGenerator : ISourceGenerator
    {
        //private static ReadWriteMode GetReadWriteMode(GeneratorSyntaxContext context, PropertyDeclarationSyntax propNode) {
        //    foreach (var alist in propNode.AttributeLists)
        //        foreach (var attr in alist.Attributes.Where(a => a.Name.ToString() == nameof(DependencyPropertyAttribute) ||
        //                                                         a.Name.ToString() == nameof(DependencyPropertyAttribute).Substring(0, nameof(DependencyPropertyAttribute).Length - "Attribute".Length))) {
        //            var arg = attr.ArgumentList?.Arguments[0];
        //            if (arg == null)
        //                return default;
        //            var v = context.SemanticModel.GetConstantValue(arg.Expression).Value;
        //            if (Enum.TryParse<ReadWriteMode>(v.ToString(), out var mode))
        //                return mode;
        //            return default;
        //        }
        //    return default;
        //}

        /// <summary>
        /// Directory where to save generated code, useful for debugging purposes.
        /// </summary>
        public static string? OutputDirectory { get; set; } = @"C:\source\depros";

        public void Initialize(GeneratorInitializationContext context) {
            context.RegisterForSyntaxNotifications(() => new DepRosSyntaxContextReciever());
        }

        public void Execute(GeneratorExecutionContext context) {
            if (context.SyntaxContextReceiver is not DepRosSyntaxContextReciever reciever)
                return;

            foreach (var classData in reciever.ClassesToProceed) {
                List<PropertyData> validProperties = new List<PropertyData>();
                bool hasPropErrors = false;
                foreach (var prop in classData.Properties) {
                    if (prop.HasMultipleAttributes) {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.TooManyAttributes, prop.SourceLocation, prop.Name, prop.Owner.FullName));
                        hasPropErrors = true;
                    }

                    if (!prop.ShouldBeAttached && (!prop.IsAuto || !prop.IsPartial)) {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidProperty, prop.SourceLocation, prop.Name, prop.Owner.FullName));
                        hasPropErrors = true;
                    }

                    if (prop.IsUnreadable) {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.IsUnreadable, prop.SourceLocation, prop.Name, prop.Owner.FullName));
                        hasPropErrors = true;
                    }

                    if (prop.Inherits && classData.Toolkit != Toolkit.Avalonia && classData.Toolkit != Toolkit.Wpf) {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InheritanceIsUnsupported, prop.SourceLocation, prop.Name, prop.Owner.FullName));
                        hasPropErrors = true;
                    }

                    if (prop.Inherits && classData.Toolkit == Toolkit.Wpf && !classData.IsFrameworkElement) {
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InheritanceRequiresFrameworkElement, prop.SourceLocation, prop.Name, prop.Owner.FullName));
                        hasPropErrors = true;
                    }

                    if (prop.IsDefaultValueIsWriteable)
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.DefaultValueIsWriteable, prop.DefaultValueLocation, prop.Name, prop.Owner.Name));

                    if (prop.HasCoerceCallback && classData.Toolkit != Toolkit.Avalonia && classData.Toolkit != Toolkit.Wpf)
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.CoercionIsUnsupported, prop.CoerceCallbackLocation, prop.Name, prop.Owner.FullName));

                    if (prop.HasValidateCallback && classData.Toolkit != Toolkit.Wpf)
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ValidationIsUnsupported, prop.ValidateCallbackLocation, prop.Name, prop.Owner.FullName));

                    if (prop.Inherits && !prop.ShouldBeAttached)
                        context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NonAttachedButInheritable, prop.SourceLocation, prop.Name, prop.Owner.FullName));

                    if (hasPropErrors)
                        continue;
                    validProperties.Add(prop);
                }

                if (!classData.IsPartial || classData.IsInner || !classData.IsClass) {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.InvalidPropOwner, classData.Node.Identifier.GetLocation(), classData.FullName));
                    hasPropErrors = true;
                }

                if (classData.Toolkit == Toolkit.Unknown) {
                    context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnknownToolkit, classData.Node.Identifier.GetLocation(), classData.FullName));
                    hasPropErrors = true;
                }

                if (validProperties.Count == 0 || hasPropErrors)
                    continue;

                using (var ms = new MemoryStream(300 + classData.Properties.Count * 300)) {
                    using (var writer = new StreamWriter(ms, Encoding.UTF8)) {
                        classData.GenerateSupplementalCode(writer);
                        writer.Flush();

                        var text = Encoding.UTF8.GetString(ms.ToArray());
                        var fileName = $"DepRos-{classData.Namespace}-{classData.Name}.g.cs";
#if DEBUG
                        if (!string.IsNullOrEmpty(OutputDirectory))
                            using (var w = new StreamWriter(Path.Combine(OutputDirectory, fileName))) {
                                w.WriteLine(text);
                                w.Flush();
                            }
#endif
                        context.AddSource(fileName, text);
                    }
                }
            }
        }
    }
}
