using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Globalization;

namespace DepRos
{
    public abstract class DependencyPropertyAttributeBase : Attribute {
        protected DependencyPropertyAttributeBase(bool inherits, BindingMode bindingMode) {
            Inherits = inherits;
            BindingMode = bindingMode;
        }

        public bool Inherits { get; }
        public BindingMode BindingMode { get; }

        public static bool IsSupported(string name) {
            if (string.IsNullOrEmpty(name))
                return false;
            if (!name.EndsWith("Attribute"))
                name += "Attribute";
            return name == nameof(DependencyPropertyAttribute) || name == nameof(AttachedPropertyAttribute);
        }
    }

    internal interface IDependencyPropertyAttribute
    {
        public bool Inherits { get; }
        public BindingMode BindingMode { get; }
        object? DefaultValue { get; }
        string Name { get; }
        bool ReadOnly { get; }
    }

    /// <summary>
    /// Marks the property as a DependencyProperty (or like).
    /// <see cref="DepRosGenerator"/> looks for this attribute and creates actual dependency property implementation then.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DependencyPropertyAttribute : DependencyPropertyAttributeBase
    {
        public DependencyPropertyAttribute(bool inherits = false, BindingMode bindingMode = BindingMode.OneWay) : base(inherits, bindingMode) { }
        public DependencyPropertyAttribute(BindingMode bindingMode) : base(false, bindingMode) { }
    }

    /// <summary>
    /// Should be applied to fields (constants) containing default value for attached dependency property.
    /// <see cref="DepRosGenerator"/> looks for this attribute and creates actual dependency property implementation then.
    /// </summary>
    /// <remarks>
    /// The field should have name in the form "defaultFor{PropertyName}".
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class AttachedPropertyAttribute : DependencyPropertyAttributeBase
    {
        public AttachedPropertyAttribute(bool inherits = false, bool readOnly = false, BindingMode bindingMode = BindingMode.OneWay) : base(inherits, bindingMode) {
            ReadOnly = readOnly;
        }
        public AttachedPropertyAttribute(BindingMode bindingMode) : base(false, bindingMode) { }

        public bool ReadOnly { get; }
    }

    internal class AttachedPropertyInfo : IDependencyPropertyAttribute
    {
        public string Name { get; set; }
        public bool Inherits { get; set; }
        public BindingMode BindingMode { get; set; }
        internal TypeInfo? TypeInfo { get; set; }
        public object? DefaultValue { get; set; }
        public Location? DefaultValueLocation { get; set; }
        public bool ReadOnly { get; set; }

        public bool IsValid => !string.IsNullOrEmpty(Name) && TypeInfo is not null;

        public AttachedPropertyInfo(AttributeSyntax node, GeneratorSyntaxContext context) {
            var model = context.SemanticModel;

            bool parseAndSetArg(string argName, ExpressionSyntax expr) {
                if (string.IsNullOrEmpty(argName))
                    return false;
                argName = CultureInfo.InvariantCulture.TextInfo.ToUpper(argName[0]) + argName.Substring(1);
                switch (argName) {
                case nameof(Name):
                    Name = model.GetConstantValue(expr).Value?.ToString() ?? "";
                    break;
                case nameof(Type):
                    TypeInfo = model.GetTypeInfo(expr);
                    break;
                case nameof(DefaultValue):
                    DefaultValue = model.GetConstantValue(expr).Value;
                    DefaultValueLocation = expr.GetLocation();
                    break;
                case nameof(Inherits):
                    Inherits = (bool)(model.GetConstantValue(expr).Value ?? false);
                    break;
                case nameof(ReadOnly):
                    ReadOnly = (bool)(model.GetConstantValue(expr).Value ?? false);
                    break;
                default:
                    return false;
                }
                return true;
            }

            if (node.ArgumentList != null) {
                var args = node.ArgumentList.Arguments;
                for (int argIdx = 0, unnamedArgsSeen = 0; argIdx < args.Count; argIdx++) {
                    var argNode = args[argIdx];
                    if (argNode.NameColon != null) {

                    }
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class WithAttachedPropertyAttribute<T> : DependencyPropertyAttributeBase, IDependencyPropertyAttribute
    {
        public WithAttachedPropertyAttribute(string name, object defaultValue, bool inherits = false, BindingMode bindingMode = BindingMode.OneWay, bool readOnly = false) : base(inherits, bindingMode) {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.DefaultValue = defaultValue;
            this.ReadOnly = readOnly;
        }

        public string Name { get; }
        public object DefaultValue { get; }
        public bool ReadOnly { get; }
    }
}
