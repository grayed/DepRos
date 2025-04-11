using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace DepRos
{
    internal enum PropertyChangedHandlerPrototype {
        Unsupported = 0,

        /// <summary>
        /// <code>OnSomePropertyChanged() { }</code>
        /// </summary>
        Empty,

        /// <summary>
        /// <code>OnSomePropertyChanged(T value) { }</code>
        /// </summary>
        NewValueOnly,

        /// <summary>
        /// <code>OnSomePropertyChanged(T oldValue, T newValue) { }</code>
        /// </summary>
        OldAndNewValue,

        /// <summary>
        /// <code>
        /// class PropertyChangedEventArgs&lt;T&gt; : EventArgs {
        ///   protected PropertyChangedEventArgs(T oldValue, T newValue) { OldValue = oldValue; NewValue = newValue; }
        ///   T OldValue { get; }
        ///   T NewValue { get; }
        /// }
        /// OnSomePropertyChanged(PropertyChangedEventArgs&lt;T&gt; ea) { }
        /// 
        /// class SomePropertyChangedEventArgs : PropertyChangedEventArgs&lt;Foo&gt; {
        ///   SomePropertyChangedEventArgs(Foo oldSome, Foo newSome) : base(oldSome, newSome) { }
        /// }
        /// OnSomePropertyChanged(SomePropertyChangedEventArgs ea) { }
        /// </code>
        /// </summary>
        EventArgs,
    }

    internal class PropertyData {
        #region Parsing helpers
        private static bool IsAutoProperty(PropertyDeclarationSyntax prop) {
            if (prop.ExpressionBody != null)
                return false;
            if (prop.AccessorList is null)
                return true;
            return prop.AccessorList.Accessors.All(a => a.Body is null && a.ExpressionBody is null);
        }

        private static bool IsUnreadableProperty(PropertyDeclarationSyntax prop) {
            // E.g.:
            //  protected int Foo { get; set; }
            if (prop.Modifiers.Any(SyntaxKind.PrivateKeyword))
                return true;
            if (!prop.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                !prop.Modifiers.Any(SyntaxKind.ProtectedKeyword) &&
                !prop.Modifiers.Any(SyntaxKind.InternalKeyword))
                return true;

            // E.g.:
            //  public int Foo => foo;
            if (prop.AccessorList is null && prop.ExpressionBody != null)
                return false;
            
            // E.g.:
            //  public int Foo { private get; set; }
            return prop.AccessorList?.Accessors.All(accessor =>
                !accessor.IsKind(SyntaxKind.GetAccessorDeclaration) ||
                 accessor.Modifiers.Any(SyntaxKind.ProtectedKeyword) ||
                 accessor.Modifiers.Any(SyntaxKind.PrivateKeyword) ||
                 accessor.Modifiers.Any(SyntaxKind.InternalKeyword)) ?? true;
        }

        private static string GetAccessModifiers(SyntaxTokenList tokens) {
            if (tokens.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                return "public ";
            else if (tokens.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
                return "private ";
            else if (tokens.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))) {
                if (tokens.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
                    return "protected internal ";
                else
                    return "protected ";
            } else if (tokens.Any(m => m.IsKind(SyntaxKind.InternalKeyword)))
                return "internal ";
            return "";
        }

        private static string GetMethodModifiers(SyntaxTokenList tokens) {
            var sb = new StringBuilder();
            if (tokens.Any(SyntaxKind.NewKeyword))
                sb.Append("new ");
            if (tokens.Any(SyntaxKind.OverrideKeyword))
                sb.Append("override ");
            if (tokens.Any(SyntaxKind.SealedKeyword))
                sb.Append("sealed ");
            if (tokens.Any(SyntaxKind.VirtualKeyword))
                sb.Append("virtual ");
            return sb.ToString();
        }
        #endregion

        private static bool AttributeNameMatchesType(NameSyntax nameSyntax, Type type) {
            string name = nameSyntax.ToString();
            if (!name.EndsWith("Attribute"))
                name += "Attribute";
            if (nameSyntax is QualifiedNameSyntax || nameSyntax is AliasQualifiedNameSyntax)
                return name == type.FullName;
            return name == type.Name;
        }
    
        public bool FindPropertyChangedHandlerPrototype(MethodDeclarationSyntax method) {
            var loc = method.GetLocation();

            int paramsCount = method.ParameterList?.Parameters.Count ?? 0;
            switch (paramsCount) {
            case 0:
                PropertyChangedHandlerPrototype = PropertyChangedHandlerPrototype.Empty;
                break;

            case 1:
                if (method.ParameterList!.Parameters[0].Type!.ToString() == this.TypeName)
                    PropertyChangedHandlerPrototype = PropertyChangedHandlerPrototype.NewValueOnly;
                else
                    PropertyChangedHandlerPrototype = PropertyChangedHandlerPrototype.EventArgs;    // XXX no checks for now
                break;

            case 2:
                if (method.ParameterList!.Parameters[0].Type!.ToString() != this.TypeName ||
                    method.ParameterList!.Parameters[1].Type!.ToString() != this.TypeName)
                    return false;
                PropertyChangedHandlerPrototype = PropertyChangedHandlerPrototype.OldAndNewValue;
                break;

            default:
                return false;
            }
            PropertyChangedHandlerLocation = loc;
            return true;
        }

        public void MarkHavingChangedHandler(Location location, PropertyChangedHandlerPrototype handlerPrototype) {
            if (handlerPrototype == PropertyChangedHandlerPrototype.Unsupported)
                PropertyChangedHandlerLocation = Location.None;
            else
                PropertyChangedHandlerLocation = location;
            PropertyChangedHandlerPrototype = handlerPrototype;
        }

#pragma warning disable CS8618      // constructors calling this one take care about Name and other properties
        private PropertyData(GeneratorSyntaxContext context, ClassData owner, MemberDeclarationSyntax memberNode, CSharpSyntaxNode declNode, Type expectedAttrType) {
            this.SourceLocation = declNode.GetLocation();
            this.Owner = owner;

            this.IsStatic = memberNode.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
            this.IsPartial = memberNode.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            this.AccessModifiers = GetAccessModifiers(memberNode.Modifiers);

            foreach (var alist in memberNode.AttributeLists)
                foreach (var attr in alist.Attributes.Where(a => AttributeNameMatchesType(a.Name, expectedAttrType))) {
                    if (HasAttribute)
                        HasMultipleAttributes = true;
                    HasAttribute = true;
                    var arg = attr.ArgumentList?.Arguments[0];
                    if (arg != null) {
                        var optVal = context.SemanticModel.GetConstantValue(arg.Expression);
                        if (optVal.HasValue)
                            if (optVal.Value is true)
                                this.Inherits = true;
                        // TODO
                    }
                }
        }
#pragma warning restore CS8618

        /// <summary>
        /// Willing to generate usual (non-attached) dependency property based on auto property.
        /// </summary>
        /// <param name="context">Context, used for looking up type information.</param>
        /// <param name="owner">The class owning the property being generated.</param>
        /// <param name="propNode">Syntax node of (auto) property being converted to dependency proeprty.</param>
        public PropertyData(GeneratorSyntaxContext context, ClassData owner, PropertyDeclarationSyntax propNode)
            : this(context, owner, propNode, propNode, typeof(DependencyPropertyAttribute)) {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(propNode);
            this.Name = propNode.Identifier.ValueText;
            this.Getter = propNode.ExpressionBody?.Expression;
            this.Getter ??= propNode.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
            this.Setter = propNode.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration));
            this.TypeName = propNode.Type.ToString();

            this.ShouldBeAttached = false;
            this.IsAuto = IsAutoProperty(propNode);
            this.ShouldBeReadOnly = this.Setter is null;
            this.IsUnreadable = IsUnreadableProperty(propNode);

            this.MethodModifiers = GetMethodModifiers(propNode.Modifiers);
            if (this.Setter != null)
                this.SetterAccessModifiers = GetAccessModifiers(this.Setter.Modifiers);
        }

        /// <summary>
        /// Willing to generate attached dependency property based on default value (either field or constant).
        /// </summary>
        /// <param name="context">Context, used for looking up type information.</param>
        /// <param name="owner">The class owning the property being generated.</param>
        /// <param name="fieldNode">Field syntax node, denotes type and modifiers of the generated dependency property.</param>
        /// <param name="declNode">Actual declaration node, denotes name of the generated dependency property.</param>
        public PropertyData(GeneratorSyntaxContext context, ClassData owner, FieldDeclarationSyntax fieldNode, VariableDeclaratorSyntax declNode)
            : this(context, owner, fieldNode, declNode, typeof(AttachedPropertyAttribute)) {
            this.Name = owner.DefaultValueNameDecoration.Strip(declNode.Identifier.ValueText) ?? "";
            this.TypeName = fieldNode.Declaration.Type.ToString();

            this.ShouldBeAttached = true;
            this.ShouldBeReadOnly = false;      // TODO
        }

        /// <summary>
        /// Willing to generate attached dependency property from class-level attribute.
        /// </summary>
        /// <param name="context">Context, used for looking up type information.</param>
        /// <param name="owner">The class owning the property being generated.</param>
        /// <param name="attachedAttrNode"></param>
        public PropertyData(GeneratorSyntaxContext context, ClassData owner, AttributeSyntax attachedAttrNode) {
            this.SourceLocation = attachedAttrNode.GetLocation();
            this.Owner = owner;
            
            var attr = new AttachedPropertyInfo(attachedAttrNode, context);
            this.Name = attr.Name;
            this.TypeName = attr.TypeInfo!.Value.Type!.DeclaringSyntaxReferences.First(sr => sr.GetSyntax() is TypeSyntax).GetSyntax().ToString();
            this.ShouldBeAttached = true;
            this.ShouldBeReadOnly = attr.ReadOnly;
            this.Inherits = attr.Inherits;
            this.DefaultValueLocation = attr.DefaultValueLocation;
        }
        public Location SourceLocation { get; }
        public ClassData Owner { get; }
        
        /// <summary>
        /// Name of the property being generated.
        /// </summary>
        /// <remarks>
        /// This is not neccessary the name of source node, as for field the <see cref="DefaultValuePrefix"/> prefix is removed.
        /// </remarks>
        public string Name { get; }

        #region Input properties
        public CSharpSyntaxNode? Getter { get; }
        public AccessorDeclarationSyntax? Setter { get; }
        public bool IsAuto { get; }
        public bool IsPartial { get; }
        public bool IsStatic { get; }
        public bool IsUnreadable { get; }

        /// <summary>
        /// True when there is at least one <see cref="DependencyPropertyAttribute"/> or
        /// <see cref="AttachedPropertyAttribute"/> attribute found.
        /// </summary>
        public bool HasAttribute { get; }

        /// <summary>
        /// True when there are more than one <see cref="DependencyPropertyAttribute"/> and/or
        /// <see cref="AttachedPropertyAttribute"/> attribute found.
        /// </summary>
        public bool HasMultipleAttributes { get; private set; }

        /// <summary>
        /// True when our class has <c>static</c> or <c>const</c> field named '<c>defaultFor{this.Name}</c>'.
        /// </summary>
        public bool HasDefaultValue => DefaultValueLocation is not null;

        /// <summary>
        /// True when <see cref="HasDefaultValue"/> is <c>true</c> and the field is static but not read-only.
        /// </summary>
        public bool IsDefaultValueIsWriteable { get; private set; }

        /// <summary>
        /// True when our class has '<c>{this.Type} Coerce{this.Name}(providedValue)</c>' method.
        /// </summary>
        public bool HasCoerceCallback => CoerceCallbackLocation is not null;

        /// <summary>
        /// True when our class has '<c>bool Validate{this.Name}(providedValue)</c>' method.
        /// </summary>
        public bool HasValidateCallback => ValidateCallbackLocation is not null;

        /// <summary>
        /// True when our class has '<c>void On{this.Name}Changed(oldValue, newValue)</c>' method.
        /// </summary>
        public bool HasPropertyChangedHandler => PropertyChangedHandlerLocation is not null;

        /// <summary>
        /// Location of default value field in source code.
        /// </summary>
        public Location? DefaultValueLocation { get; private set; }

        /// <summary>
        /// Location of '<c>bool Coerce{this.Name}(providedValue)</c>' method in source code.
        /// </summary>
        public Location? CoerceCallbackLocation { get; private set; }

        /// <summary>
        /// Location of '<c>bool Validate{this.Name}(providedValue)</c>' method in source code.
        /// </summary>
        public Location? ValidateCallbackLocation { get; private set; }

        /// <summary>
        /// Location of '<c>void On{this.Name}Changed(oldValue, newValue)</c>' method in source code.
        /// </summary>
        public Location? PropertyChangedHandlerLocation { get; private set; }

        /// <summary>
        /// Describes how to call property changed handler.
        /// </summary>
        public PropertyChangedHandlerPrototype PropertyChangedHandlerPrototype { get; private set; }
    #endregion

    #region Output properties
    /// <summary>
    /// Type spec to be used for property declaration, e.g., <c>int?</c>.
    /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// When <see cref="TypeName"/> is e.g., <c>int?</c>, this property will be <c>int</c>. Needed for, e.g., <c>typeof()</c>.
        /// </summary>
        public string NonNullTypeName => TypeName.TrimEnd('?');
        
        public string DefaultValueName => Owner.DefaultValueNameDecoration.ApplyTo(Name);
        public string CoerceCallbackName => Owner.CoerceCallbackNameDecoration.ApplyTo(Name);
        public string ValidateCallbackName => Owner.ValidateCallbackNameDecoration.ApplyTo(Name);
        public string PropertyChangedHandlerName => Owner.PropertyChangedHandlerNameDecoration.ApplyTo(Name);

        public bool ShouldBeAttached { get; }
        public bool ShouldBeReadOnly { get; }
        public bool Inherits { get; }

        // either empty, or ends up in space
        public string AccessModifiers { get; } = "";
        public string MethodModifiers { get; } = "";
        public string SetterAccessModifiers { get; } = "";
        #endregion

        public void MarkHavingDuplicate() => HasMultipleAttributes = true;
        public void MarkHavingDefaultValue(Location location) => DefaultValueLocation = location;
        public void MarkHavingWriteableDefaultValue() => IsDefaultValueIsWriteable = true;
        public void MarkHavingCoerceCallback(Location location) => CoerceCallbackLocation = location;
        public void MarkHavingValidationCallback(Location location) => ValidateCallbackLocation = location;

        public void GenerateDependencyProperty(StreamWriter writer) {
            switch (Owner.Toolkit) {
            case Toolkit.Avalonia:
                GenerateDependencyPropertyForAvalonia(writer);
                break;
            case Toolkit.Uwp:
            case Toolkit.WinUI:
            case Toolkit.Wpf:
                GenerateDependencyPropertyForMS(writer);
                break;
            default:
                throw new NotSupportedException();
            }
        }

        private void GenerateDependencyPropertyForAvalonia(StreamWriter writer) {
            // styled property
            writer.WriteLine($"\tpublic static readonly Avalonia.StyledProperty<{TypeName}> {Name}Property =");

            if (ShouldBeAttached) {
                // TODO: support separate host type
                writer.Write($"\t\tAvalonia.AvaloniaProperty.RegisterAttached<{Owner.Name}, {TypeName}>(nameof({Name})");
            } else {
                writer.Write($"\t\tAvalonia.AvaloniaProperty.Register<{Owner.Name}, {TypeName}>(nameof({Name})");
            }
            if (HasDefaultValue)
                writer.Write($", defaultValue: {DefaultValueName}");
            if (Inherits)
                writer.Write(", inherits: true");

            // WPF requires the Func<DependencyObject, object, object> callback as coerce callback,
            // while Avalonia wants a Func<TOwner, TValue, TValue> one.

            if (HasCoerceCallback)
                writer.Write($", coerce: {CoerceCallbackName}");
            writer.WriteLine(");");

            // actual property
            if (ShouldBeAttached) {
                // getter and setter methods
                writer.WriteLine($"\tpublic static {TypeName} Get{Name}(Avalonia.AvaloniaObject obj)");
                writer.Write("\t{ ");
                writer.Write($"return obj.GetValue({Name}Property);");
                writer.WriteLine(" }");

                if (!ShouldBeReadOnly) {
                    writer.WriteLine($"\tpublic static void Set{Name}(Avalonia.AvaloniaObject obj, {TypeName} value)");
                    writer.Write("\t{ ");
                    writer.Write($"obj.SetValue({Name}Property, value);");
                    writer.WriteLine(" }");
                }

            } else {
                writer.Write("\t");
                if (IsStatic)
                    writer.Write("static ");
                writer.Write($"{AccessModifiers}{MethodModifiers}partial {TypeName} {Name}");
                writer.WriteLine(" {");

                writer.Write("\t\tget { ");
                writer.Write($"return GetValue({Name}Property);");
                writer.WriteLine(" }");

                if (!ShouldBeReadOnly) {
                    writer.Write($"\t\t{SetterAccessModifiers}");
                    writer.Write("set { ");
                    writer.Write($"SetValue({Name}Property, value);");
                    writer.WriteLine(" }");
                }

                writer.WriteLine("\t}");
            }
        }

        private void GenerateDependencyPropertyForMS(StreamWriter writer) {
            string ns;

            string attached = ShouldBeAttached ? "Attached" : "";
            string nameOfProp = ShouldBeAttached ? $"\"{Name}\"" : $"nameof({Name})";

            // WPF requires the Func<DependencyObject, object, object> callback as coerce callback,
            // while Avalonia wants a Func<TOwner, TValue, TValue> one.

            void GeneratePropertyMetadata() {
                writer.Write("PropertyMetadata(defaultValue: ");

                if (HasDefaultValue)
                    writer.Write(Owner.DefaultValueNameDecoration.ApplyTo(Name));
                else
                    writer.Write($"default({TypeName})");

                writer.Write($", propertyChangedCallback: {GenerateWpfChangedHandler()}");

                if (HasCoerceCallback && Owner.Toolkit == Toolkit.Wpf)
                    writer.Write($", coerceValueCallback: {CoerceCallbackName}");

                writer.Write(")");
            }

            void GenerateUIPropertyMetadata() {
                writer.Write("UIPropertyMetadata(defaultValue: ");

                if (HasDefaultValue)
                    writer.Write(Owner.DefaultValueNameDecoration.ApplyTo(Name));
                else
                    writer.Write($"default({TypeName})");

                writer.Write($", propertyChangedCallback: {GenerateWpfChangedHandler()}");

                if (HasCoerceCallback)
                    writer.Write($", coerceValueCallback: {CoerceCallbackName}");
                else
                    writer.Write(", coerceValueCallback: null");

                //TODO: isAnimationProhibited

                writer.Write(")");
            }

            void GenerateFrameworkPropertyMetadata() {
                writer.Write("FrameworkPropertyMetadata(defaultValue: ");

                if (HasDefaultValue)
                    writer.Write(Owner.DefaultValueNameDecoration.ApplyTo(Name));
                else
                    writer.Write($"default({TypeName})");

                if (Inherits)
                    writer.Write($", flags: {ns}.FrameworkPropertyMetadataOptions.Inherits");
                else
                    writer.Write($", flags: {ns}.FrameworkPropertyMetadataOptions.None");
                // TODO: other flags

                writer.Write($", propertyChangedCallback: {GenerateWpfChangedHandler()}");

                if (HasCoerceCallback)
                    writer.Write($", coerceValueCallback: {CoerceCallbackName}");
                else
                    writer.Write(", coerceValueCallback: null");

                //TODO: isAnimationProhibited

                writer.Write(")");
            }

            switch (Owner.Toolkit) {
            case Toolkit.Uwp:
                if (HasCoerceCallback)
                    throw new InvalidOperationException($"UWP doesn't support coerce value callback");
                if (Inherits)
                    throw new InvalidOperationException($"The '{nameof(Inherits)}' property is set for non-WPF MS framework");
                ns = "Windows.UI.Xaml";
                break;
            case Toolkit.WinUI:
                if (HasCoerceCallback)
                    throw new InvalidOperationException($"WinUI/Uno doesn't support coerce value callback");
                if (Inherits)
                    throw new InvalidOperationException($"The '{nameof(Inherits)}' property is set for non-WPF MS framework");
                ns = "Microsoft.UI.Xaml";
                break;
            case Toolkit.Wpf:
                ns = "System.Windows";
                break;
            default:
                throw new InvalidOperationException($"Toolkit {Owner.Toolkit} is not supported by this generator method");
            };

            // dependency property
            if (Owner.Toolkit == Toolkit.Wpf && ShouldBeReadOnly) {
                writer.WriteLine($"\tprivate static readonly {ns}.DependencyPropertyKey {Name}PropertyKey = {ns}.DependencyProperty.Register{attached}ReadOnly(");
            } else {
                writer.WriteLine($"\tpublic static readonly {ns}.DependencyProperty {Name}Property = {ns}.DependencyProperty.Register{attached}(");
            }
            writer.Write($"\t\t{nameOfProp}, typeof({NonNullTypeName}), typeof({Owner.Name}), new {ns}.");
            if (Owner.Toolkit == Toolkit.Wpf && Owner.IsFrameworkElement)
                GenerateFrameworkPropertyMetadata();
            else if (Owner.Toolkit == Toolkit.Wpf && Owner.IsUIElement)
                GenerateUIPropertyMetadata();
            else
                GeneratePropertyMetadata();
            if (Owner.Toolkit == Toolkit.Wpf && HasValidateCallback)
                writer.WriteLine($", {ValidateCallbackName}");
            writer.WriteLine(");");

            if (Owner.Toolkit == Toolkit.Wpf && ShouldBeReadOnly)
                writer.WriteLine($"\tpublic static readonly {ns}.DependencyProperty {Name}Property = {Name}PropertyKey.DependencyProperty;");

            if (ShouldBeAttached) {
                // getter and setter methods
                writer.WriteLine($"\tpublic static {TypeName} Get{Name}({ns}.DependencyObject obj)");
                writer.Write("\t{ ");
                writer.Write($"return ({TypeName})obj.GetValue({Name}Property);");
                writer.WriteLine(" }");

                if (!ShouldBeReadOnly) {
                    writer.WriteLine($"\tpublic static void Set{Name}({ns}.DependencyObject obj, {TypeName} value)");
                    writer.Write("\t{ ");
                    writer.Write($"obj.SetValue({Name}Property, value);");
                    writer.WriteLine(" }");
                }
            } else {
                // actual, normal property
                writer.Write("\t");
                if (IsStatic)
                    writer.Write("static ");
                writer.Write($"{AccessModifiers}{MethodModifiers}partial {TypeName} {Name}");
                writer.WriteLine(" {");

                writer.Write("\t\tget { ");
                writer.Write($"return ({TypeName})GetValue({Name}Property);");
                writer.WriteLine(" }");

                if (!ShouldBeReadOnly) {
                    writer.Write($"\t\t{SetterAccessModifiers}");
                    writer.Write("set { ");
                    writer.Write($"SetValue({Name}Property, value);");
                    writer.WriteLine(" }");
                }
                writer.WriteLine("\t}");
            }

            writer.WriteLine();
        }

        private string GenerateWpfChangedHandler() => PropertyChangedHandlerPrototype switch {
            PropertyChangedHandlerPrototype.Unsupported => @"null",
            PropertyChangedHandlerPrototype.Empty => $"(d, e) => (({Owner.Name})d).{PropertyChangedHandlerName}(({TypeName})e.NewValue)",
            PropertyChangedHandlerPrototype.NewValueOnly => $"(d, e) => (({Owner.Name})d).{PropertyChangedHandlerName}(({TypeName})e.NewValue)",
            PropertyChangedHandlerPrototype.OldAndNewValue => $"(d, e) => (({Owner.Name})d).{PropertyChangedHandlerName}(({TypeName})e.OldValue, ({TypeName})e.NewValue)",
            PropertyChangedHandlerPrototype.EventArgs => $"(d, e) => (({Owner.Name})d).{PropertyChangedHandlerName}(new(({TypeName})e.OldValue, ({TypeName})e.NewValue))",
            _ => throw new InvalidOperationException($"Unknown PropertyChangedHandlerPrototype value: {PropertyChangedHandlerPrototype}")
        };
    }
}
