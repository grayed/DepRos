using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DepRos
{
    internal class Decoration {
        public string Prefix { get; }
        public string Suffix { get; }

        public Decoration(string prefix, string suffix = "") {
            Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
            Suffix = suffix ?? throw new ArgumentNullException(nameof(suffix));
            if (Prefix.Length == 0 && Suffix.Length == 0)
                throw new ArgumentException("Both prefix and suffix cannot be empty");
        }

        public string? Strip(string s) {
            if (s is null || !s.Matches(this))
                return null;
            return s.Substring(Prefix.Length, s.Length - (Prefix.Length + Suffix.Length));
        }

        public string ApplyTo(string s) {
            return Prefix + s + Suffix;
        }
    }

    internal enum DecorationAttributeArgumentDecodeResult {
        Unknown = 0,
        Prefix,     // must be same as in Decoration
        Suffix
    }

    internal static class DecorationExtensions
    {
        public static bool Matches(this string s, Decoration match) {
            if (string.IsNullOrEmpty(s))
                return false;
            return s.StartsWith(match.Prefix) && s.EndsWith(match.Suffix);
        }

        /// <summary>
        /// Checks if the given attribute argument represents either prefix or suffix, and returns it if so.
        /// </summary>
        /// <param name="arg">Attribute argument to parse</param>
        /// <param name="semanticModel">Semantic model used for parsing</param>
        /// <returns>Prefix or suffix value, and knowledge what it was</returns>
        private static (DecorationAttributeArgumentDecodeResult, string?) DecodePrefixOrSuffixUsing(this AttributeArgumentSyntax arg, SemanticModel semanticModel) {
            DecorationAttributeArgumentDecodeResult decodeResult = DecorationAttributeArgumentDecodeResult.Unknown;
            string? strResult = null;
            if (arg.NameColon != null) {
                if (!Enum.TryParse(arg.NameColon.Name.ToString(), true, out decodeResult) || decodeResult == DecorationAttributeArgumentDecodeResult.Unknown)
                    return (DecorationAttributeArgumentDecodeResult.Unknown, null);
            } else if (arg.NameEquals != null) {
                if (!Enum.TryParse(arg.NameEquals.Name.ToString(), false, out decodeResult) || decodeResult == DecorationAttributeArgumentDecodeResult.Unknown)
                    return (DecorationAttributeArgumentDecodeResult.Unknown, null);
            }
            if (semanticModel.GetConstantValue(arg.Expression).Value is string s)
                strResult = s;
            return (decodeResult, strResult);
        }

        /// <summary>
        /// Lookup a <see cref="DecorationAttributeBase"/> child among syntax node's attributes.
        /// </summary>
        /// <typeparam name="AttributeT">Type of attribute to search for</typeparam>
        /// <param name="attrLists">Where to search</param>
        /// <param name="semanticModel">Semantic model used for parsing</param>
        /// <returns>Decoration constructed from the attribute found, or <c>null</c></returns>
        /// <remarks>Lookup happens by attribute name only, not by (fully) qualified name.</remarks>
        private static Decoration? FindIn<AttributeT>(IReadOnlyList<AttributeListSyntax> attrLists, SemanticModel semanticModel)
            where AttributeT : DecorationAttributeBase, new()       // new() gets out DecorationAttributeBase itself
        {
            foreach (var attrsList in attrLists) {
                var attr = FindIn<AttributeT>(attrsList.Attributes, semanticModel);
                if (attr != null)
                    return attr;
            }
            return null;
        }

        /// <summary>
        /// Lookup a <see cref="DecorationAttributeBase"/> child among syntax node's attributes.
        /// </summary>
        /// <typeparam name="AttributeT">Type of attribute to search for</typeparam>
        /// <param name="attrList">Where to search</param>
        /// <param name="semanticModel">Semantic model used for parsing</param>
        /// <returns>Decoration constructed from the attribute found, or <c>null</c></returns>
        /// <remarks>Lookup happens by attribute name only, not by (fully) qualified name.</remarks>
        private static Decoration? FindIn<AttributeT>(IReadOnlyList<AttributeSyntax> attrList, SemanticModel semanticModel)
            where AttributeT : DecorationAttributeBase, new()       // new() gets out the DecorationAttributeBase itself
        {
            // cache
            var expectedName = typeof(AttributeT).Name;

            foreach (var attr in attrList) {
                var name = attr.Name.ToString();
                if (!name.EndsWith("Attribute"))
                    name += "Attribute";
                if (name != expectedName)
                    continue;

                string? prefix = null, suffix = null;
                if (attr.ArgumentList != null) {
                    var args = attr.ArgumentList.Arguments;
                    for (int argIdx = 0, unnamedArgsSeen = 0; argIdx < args.Count; argIdx++) {
                        var (decodeResult, str) = args[argIdx].DecodePrefixOrSuffixUsing(semanticModel);
                        if (str is null)
                            throw new InvalidOperationException("Decoration attribute argument is not a string");

                        switch (decodeResult) {
                        case DecorationAttributeArgumentDecodeResult.Prefix:
                            prefix = str;
                            break;

                        case DecorationAttributeArgumentDecodeResult.Suffix:
                            suffix = str;
                            break;

                        default:
                            if (unnamedArgsSeen == 0)
                                prefix = str;
                            else if (unnamedArgsSeen == 1)
                                suffix = str;
                            unnamedArgsSeen++;
                            break;
                        }
                    }
                }

                // Attribute values cannot be null, since attributes construct Decoration internally,
                // and the latter doesn't allow null prefix or suffix. Thus, null means 'not set'.
                // In case we got data from wrong attributes, it's not our fault.)
                if (prefix is null || suffix is null) {
                    AttributeT defaultAttr = new();
                    prefix ??= defaultAttr.Prefix;
                    suffix ??= defaultAttr.Suffix;
                }
                return new Decoration(prefix, suffix);
            }

            return null;
        }

        public static Decoration GetDecorationFrom<AttributeT>(this SyntaxNode node, SemanticModel semanticModel)
            where AttributeT : DecorationAttributeBase, new() {

            Decoration? decoration;
            for (SyntaxNode? n = node; n != null; n = n.Parent) {
                switch (n) {
                case MemberDeclarationSyntax memberNode:
                    decoration = FindIn<AttributeT>(memberNode.AttributeLists, semanticModel);
                    break;
                case CompilationUnitSyntax cu:
                    decoration = FindIn<AttributeT>(cu.AttributeLists, semanticModel);
                    break;
                default:
                    continue;
                }
                if (decoration != null)
                    return decoration;
            }

            // TODO: lookup assembly definitions somehow?

            return new AttributeT().Decoration;
        }
    }
}
