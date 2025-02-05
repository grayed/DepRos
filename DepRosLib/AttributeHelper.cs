using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace DepRos
{
    internal static class AttributeHelper
    {
        public static AttributeSyntax? Find<AttributeT>(this MemberDeclarationSyntax of) {
            var expectedName = typeof(AttributeT).Name;

            foreach (var attrsList in of.AttributeLists)
                foreach (var attr in attrsList.Attributes) {
                    var name = attr.Name.ToString();
                    if (!name.EndsWith("Attribute"))
                        name += "Attribute";
                    if (name == expectedName)
                        return attr;
                }
            return null;
        }

        public static IEnumerable<AttributeSyntax> FindAll<AttributeT>(this MemberDeclarationSyntax of) where AttributeT : Attribute {
            return of.FindAll(typeof(AttributeT).Name);     // TODO pass fully-qualified names here
        }

        public static IEnumerable<AttributeSyntax> FindAll(this MemberDeclarationSyntax of, string attrName) {
            var isTemplated = attrName.EndsWith("`1");
            var expectedEnd = isTemplated ? "Attribute`1" : "Attribute";
            if (!attrName.EndsWith(expectedEnd))
                throw new ArgumentException($"invalid attribute name '{attrName}'", nameof(attrName));

            // add support for fully qualified names
            foreach (var attrsList in of.AttributeLists)
                foreach (var attr in attrsList.Attributes) {
                    var name = attr.Name.ToString();
                    if (!name.EndsWith(expectedEnd))
                        name += "Attribute";
                    if (name == attrName)
                        yield return attr;
                }
        }

        public static AttributeSyntax? Find<AttributeT>(this CompilationUnitSyntax of) {
            // exactly same as above
            var expectedName = typeof(AttributeT).Name;

            foreach (var attrsList in of.AttributeLists)
                foreach (var attr in attrsList.Attributes) {
                    var name = attr.Name.ToString();
                    if (!name.EndsWith("Attribute"))
                        name += "Attribute";
                    if (name == expectedName)
                        return attr;
                }
            return null;
        }

        public static IEnumerable<AttributeSyntax> FindAll<AttributeT>(this CompilationUnitSyntax of) {
            var expectedName = typeof(AttributeT).Name;

            foreach (var attrsList in of.AttributeLists)
                foreach (var attr in attrsList.Attributes) {
                    var name = attr.Name.ToString();
                    if (!name.EndsWith("Attribute"))
                        name += "Attribute";
                    if (name == expectedName)
                        yield return attr;
                }
        }
    }
}
