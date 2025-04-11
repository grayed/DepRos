using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DepRos
{
    internal class ClassData
    {
        public TypeDeclarationSyntax Node { get; }
        public string Namespace { get; }
        public string Name { get; }
        public string FullName { get; }
        public SyntaxList<UsingDirectiveSyntax> Usings { get; }

        public bool IsUIElement { get; }
        public bool IsFrameworkElement { get; }
        public Toolkit Toolkit { get; }

        public List<PropertyData> Properties { get; }

        public bool IsPartial { get; }
        public bool IsClass { get; }
        public bool IsInner { get; }

        public Decoration DefaultValueNameDecoration { get; }
        public Decoration CoerceCallbackNameDecoration { get; }
        public Decoration ValidateCallbackNameDecoration { get; }
        public Decoration PropertyChangedHandlerNameDecoration { get; }

        public ClassData(GeneratorSyntaxContext context, TypeDeclarationSyntax node) {
            var symbol = context.SemanticModel.GetDeclaredSymbol(node);
            var cu = node.FirstAncestorOrSelf<CompilationUnitSyntax>()!;    // should always succeed

            this.Node = node;
            this.Namespace = symbol!.ContainingNamespace?.ToString() ?? "";
            this.Name = symbol.Name;
            this.FullName = (this.Namespace.Length > 0) ? $"{this.Namespace}.{this.Name}" : this.Name;
            this.Usings = cu.Usings;

            var baseType = symbol.BaseType;
            while (baseType != null) {
                var ns = baseType.ContainingNamespace?.ToString();
                if (string.IsNullOrEmpty(ns))
                    break;
                var fullName = $"{ns}.{baseType.Name}";

                if (fullName == "System.Windows.FrameworkElement")
                    this.IsFrameworkElement = true;
                if (fullName == "System.Windows.UIElement")
                    this.IsUIElement = true;
                if (fullName == "System.Windows.DependencyObject")
                    this.Toolkit = Toolkit.Wpf;

                if (fullName == "Microsoft.UI.Xaml.FrameworkElement")
                    this.IsFrameworkElement = true;
                if (fullName == "Microsoft.UI.Xaml.UIElement")
                    this.IsUIElement = true;
                if (fullName == "Microsoft.UI.Xaml.DependencyObject")
                    this.Toolkit = Toolkit.WinUI;

                if (fullName == "Avalonia.AvaloniaObject")
                    this.Toolkit = Toolkit.Avalonia;
                baseType = baseType.BaseType;
            }

            this.IsPartial = node.Modifiers.Any(SyntaxKind.PartialKeyword);
            this.IsClass = node is ClassDeclarationSyntax;
            this.IsInner = node.Parent?.FirstAncestorOrSelf<TypeDeclarationSyntax>() != null;

            this.DefaultValueNameDecoration = node.GetDecorationFrom<DefaultValueNameDecorationAttribute>(context.SemanticModel);
            this.CoerceCallbackNameDecoration = node.GetDecorationFrom<CoerceCallbackNameDecorationAttribute>(context.SemanticModel);
            this.ValidateCallbackNameDecoration = node.GetDecorationFrom<ValidateCallbackNameDecorationAttribute>(context.SemanticModel);
            this.PropertyChangedHandlerNameDecoration = node.GetDecorationFrom<PropertyChangedHandlerNameDecorationAttribute>(context.SemanticModel);

            this.Properties = AnalyzeProperties(context);
        }

        public void GenerateSupplementalCode(StreamWriter writer) {
            StartClassGeneration(writer);
            foreach (var prop in Properties)
                prop.GenerateDependencyProperty(writer);
            FinishClassGeneration(writer);
        }

        private void StartClassGeneration(StreamWriter writer) {
            writer.WriteLine("#nullable enable");
            writer.WriteLine(Usings.ToFullString());

            if (Namespace.Length > 0) {
                writer.Write($"namespace {Namespace}");
                writer.Write(" { ");
            }

            writer.Write($"partial class {Name}");
            writer.WriteLine(" {");
        }

        private static void FinishClassGeneration(StreamWriter writer) {
            writer.WriteLine("} }");
            writer.WriteLine("#nullable restore");
        }

        private List<PropertyData> AnalyzeProperties(GeneratorSyntaxContext context) {
            var classPropertiesByName = new Dictionary<string, PropertyData>();
            var classProperties = new List<PropertyData>();
            var defaultValuesFoundFor = new List<(string, FieldDeclarationSyntax)>();
            var coerceCallbacksFoundFor = new List<(string, MethodDeclarationSyntax)>();
            var validateCallbacksFoundFor = new List<(string, MethodDeclarationSyntax)>();
            var propertyChangedHandlersFoundFor = new List<(string, MethodDeclarationSyntax)>();

            var withAttachedProperties = Node.FindAll(typeof(WithAttachedPropertyAttribute<string>).GetGenericTypeDefinition().Name);
            foreach (var aaNode in withAttachedProperties) {
                var p = new PropertyData(context, this, aaNode);
            }

            foreach (var node in Node.ChildNodes()) {
                switch (node) {
                case PropertyDeclarationSyntax propertyNode:
                    var propData = new PropertyData(context, this, propertyNode);
                    if (propData.HasAttribute) {
                        if (classPropertiesByName.TryGetValue(propData.Name, out var existing))
                            existing.MarkHavingDuplicate();
                        else {
                            classPropertiesByName.Add(propData.Name, propData);
                            classProperties.Add(propData);
                        }
                    }
                    break;

                case FieldDeclarationSyntax fieldNode:
                    foreach (var declarator in fieldNode.Declaration.ChildNodes().OfType<VariableDeclaratorSyntax>()) {
                        var fieldName =  declarator.Identifier.ValueText;
                        var resPropName = DefaultValueNameDecoration.Strip(fieldName);
                        if (string.IsNullOrEmpty(resPropName))
                            continue;
                        defaultValuesFoundFor.Add((resPropName!, fieldNode));

                        var attPropData = new PropertyData(context, this, fieldNode, declarator);
                        if (attPropData.HasAttribute) {
                            if (classPropertiesByName.TryGetValue(attPropData.Name, out var existing))
                                existing.MarkHavingDuplicate();
                            else {
                                classPropertiesByName.Add(attPropData.Name, attPropData);
                                classProperties.Add(attPropData);
                            }
                        }
                    }
                    break;

                case MethodDeclarationSyntax methodNode:
                    var methodName = methodNode.Identifier.ValueText;
                    string? propName;
                    if ((propName = CoerceCallbackNameDecoration.Strip(methodName)) != null) {
                        coerceCallbacksFoundFor.Add((propName, methodNode));
                    } else if ((propName = ValidateCallbackNameDecoration.Strip(methodName)) != null) {
                        validateCallbacksFoundFor.Add((propName, methodNode));
                    } else if ((propName = PropertyChangedHandlerNameDecoration.Strip(methodName)) != null) {
                        propertyChangedHandlersFoundFor.Add((propName, methodNode));
                    }
                    break;
                }
            }

            foreach (var (name, fieldNode) in defaultValuesFoundFor)
                if (classPropertiesByName.TryGetValue(name, out var propData)) {
                    propData.MarkHavingDefaultValue(fieldNode.GetLocation());
                    if (!fieldNode.Modifiers.Any(SyntaxKind.ConstKeyword) &&
                        !fieldNode.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)) {
                        propData.MarkHavingWriteableDefaultValue();
                    }
                }

            foreach (var (name, method) in coerceCallbacksFoundFor)
                if (classPropertiesByName.TryGetValue(name, out var propData))
                    propData.MarkHavingCoerceCallback(method.GetLocation());

            foreach (var (name, method) in validateCallbacksFoundFor)
                if (classPropertiesByName.TryGetValue(name, out var propData))
                    propData.MarkHavingValidationCallback(method.GetLocation());

            foreach (var (name, method) in propertyChangedHandlersFoundFor)
                if (classPropertiesByName.TryGetValue(name, out var propData))
                    propData.MarkHavingChangedHandler(method.GetLocation());

            // TODO: Warn about unused callbacks and default values?

            return classProperties;
        }
    }
}