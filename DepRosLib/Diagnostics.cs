using Microsoft.CodeAnalysis;

namespace DepRos
{
    internal static class Diagnostics
    {
        public static readonly DiagnosticDescriptor InvalidProperty = new DiagnosticDescriptor("DR0001",
            "[DependencyProperty] attribute must be applied to partial auto properties only",
            "The {0} property in {1} was annotated with [DependencyProperty] attribute, but it's not partial auto property",
            "DepRos", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor InvalidPropOwner = new DiagnosticDescriptor("DR0002",
            "DependencyProperty owner must be a top-level partial class",
            "The [DependencyProperty] was used in {0}, which is not a partial top-level class",
            "DepRos", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor IsUnreadable = new DiagnosticDescriptor("DR0003",
            "Missing public getter for the property annotated with [DependencyProperty]",
            "The {0} property in {1} has no public getter, required by [DependencyProperty] attribute",
            "DepRos", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor UnknownToolkit = new DiagnosticDescriptor("DR0004",
            "[DependencyProperty] supports only Avalonia and WPF controls",
            "The {0} class owning a [DependencyProperty]-marked property is not a System.Windows.Contol or Avalonia.Controls.Control descendant",
            "DepRos", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor DefaultValueIsWriteable = new DiagnosticDescriptor("DR0005",
            "Default property value can be modified",
            "The {0} field in {1} is static but not readonly, this could result in unexpected behaviour; consider adding readonly modifier",
            "DepRos", DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor InheritanceIsUnsupported = new DiagnosticDescriptor("DR0006",
            "Dependency property inheritance is supported by AvaloniaUI and WPF only",
            "The {0} property in {1} cannot be marked as inheritable, since inheritance is supported only by AvaloniaUI and WPF",
            "DepRos", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor TooManyAttributes = new DiagnosticDescriptor("DR0007",
            "Too many DependencyProperty and/or AttachedProperty attributes",
            "The {0} property in {1} must have either DependencyProperty attribute or AttachedProperty attribute, only one of them",
            "DepRos", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor NonAttachedButInheritable = new DiagnosticDescriptor("DR0008",
            "Inheritable properties better be attached",
            "The {0} property in {1} is marked as inherited but not attached, this could cause run-time issues",
            "DepRos", DiagnosticSeverity.Warning, true, helpLinkUri: @"https://learn.microsoft.com/en-us/dotnet/api/system.windows.dependencyproperty.registerattached#use-registerattached-for-value-inheriting-dependency-properties");

        public static readonly DiagnosticDescriptor ValidationIsUnsupported = new DiagnosticDescriptor("DR0009",
            "Dependency property validation callback is supported by WPF only",
            "The validation callback is ignored or {0} property in {1}, since inheritance is supported only by WPF; for AvaloniaUI coerce callback should be used instead",
            "DepRos", DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor CoercionIsUnsupported = new DiagnosticDescriptor("DR0010",
            "Dependency property value coercion is supported by AvaloniaUI and WPF only",
            "The coerce value callback is ignored or {0} property in {1}, since value coercion is supported only by AvaloniaUI and WPF",
            "DepRos", DiagnosticSeverity.Warning, true);

        public static readonly DiagnosticDescriptor InheritanceRequiresFrameworkElement = new DiagnosticDescriptor("DR0011",
            "Inheritable properties requires FrameworkElement owner",
            "The {0} property is marked as inherited in {1} which is not a FrameworkElement descendant",
            "DepRos", DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor UnsupportedPropertyChangedHandler = new DiagnosticDescriptor("DR0012",
            "Ignoring unsupported property changed handler prototype",
            "The {0} method at {1} must accept either a single property type parameter, two such parameters, or a PropertyChangedEventArgs<{3}> object",
            "DepRos", DiagnosticSeverity.Warning, true);
    }
}
