using System;

namespace DepRos
{
    // NOTE: The "Prefix" and "Suffix" property names in DecorationAttributeBase must be same as in
    // the DecorationAttributeArgumentDecodeResult enumeration. Also, all descendants of DecorationAttributeBase
    // must have "prefix" and "suffix" constructor parameters named exactly the same,
    // see the DecorationExtensions.DecodeString() for details.

    public abstract class DecorationAttributeBase : Attribute
    {
        protected DecorationAttributeBase(string prefix, string suffix) {
            this.Decoration = new Decoration(prefix, suffix);
        }

        internal Decoration Decoration { get; }
        public string Prefix => Decoration.Prefix;
        public string Suffix => Decoration.Suffix;
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Module | AttributeTargets.Assembly)]
    public sealed class DefaultValueNameDecorationAttribute : DecorationAttributeBase
    {
        public const string DefaultPrefix = "defaultFor";
        public const string DefaultSuffix = "";
        internal static readonly Decoration Default = new Decoration(DefaultPrefix, DefaultSuffix);

        public DefaultValueNameDecorationAttribute() : base(DefaultPrefix, DefaultSuffix) { }
        public DefaultValueNameDecorationAttribute(string prefix) : base(prefix, DefaultSuffix) { }
        public DefaultValueNameDecorationAttribute(string prefix, string suffix) : base(prefix, suffix) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Module | AttributeTargets.Assembly)]
    public sealed class CoerceCallbackNameDecorationAttribute : DecorationAttributeBase
    {
        public const string DefaultPrefix = "Coerce";
        public const string DefaultSuffix = "";
        internal static readonly Decoration Default = new Decoration(DefaultPrefix, DefaultSuffix);

        public CoerceCallbackNameDecorationAttribute() : base(DefaultPrefix, DefaultSuffix) { }
        public CoerceCallbackNameDecorationAttribute(string prefix) : base(prefix, DefaultSuffix) { }
        public CoerceCallbackNameDecorationAttribute(string prefix, string suffix) : base(prefix, suffix) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Module | AttributeTargets.Assembly)]
    public sealed class ValidateCallbackNameDecorationAttribute : DecorationAttributeBase
    {
        public const string DefaultPrefix = "Validate";
        public const string DefaultSuffix = "";
        internal static readonly Decoration Default = new Decoration(DefaultPrefix, DefaultSuffix);

        public ValidateCallbackNameDecorationAttribute() : base(DefaultPrefix, DefaultSuffix) { }
        public ValidateCallbackNameDecorationAttribute(string prefix) : base(prefix, DefaultSuffix) { }
        public ValidateCallbackNameDecorationAttribute(string prefix, string suffix) : base(prefix, suffix) { }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Module | AttributeTargets.Assembly)]
    public sealed class PropertyChangedHandlerNameDecorationAttribute : DecorationAttributeBase
    {
        public const string DefaultPrefix = "On";
        public const string DefaultSuffix = "Changed";
        internal static readonly Decoration Default = new Decoration(DefaultPrefix, DefaultSuffix);

        public PropertyChangedHandlerNameDecorationAttribute() : base(DefaultPrefix, DefaultSuffix) { }
        public PropertyChangedHandlerNameDecorationAttribute(string prefix) : base(prefix, DefaultSuffix) { }
        public PropertyChangedHandlerNameDecorationAttribute(string prefix, string suffix) : base(prefix, suffix) { }
    }
}
