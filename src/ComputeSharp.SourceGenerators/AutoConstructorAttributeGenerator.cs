﻿using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.SymbolDisplayTypeQualificationStyle;

namespace ComputeSharp.SourceGenerators
{
    [Generator]
    public class AutoConstructorAttributeGenerator : ISourceGenerator
    {
        /// <inheritdoc/>
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        /// <inheritdoc/>
        public void Execute(GeneratorExecutionContext context)
        {
            const string attributeText = @"
            using System;

            namespace ComputeSharp
            {
                /// <summary>
                /// A shader that indicates that a target shader type should get an automatic constructor for all fields.
                /// </summary>
                [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
                public sealed class AutoConstructorAttribute : Attribute
                {
                }
            }";

            var attributeSource = SourceText.From(ParseCompilationUnit(attributeText).NormalizeWhitespace().ToFullString(), Encoding.UTF8);

            // Add the [AutoConstructor] attribute
            context.AddSource("__ComputeSharp_AutoConstructorAttribute", attributeSource);

            // Find all the [AutoConstructor] usages
            ImmutableArray<AttributeSyntax> attributes = (
                from tree in context.Compilation.SyntaxTrees
                from attribute in tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>()
                where attribute.Name.ToString() == "AutoConstructor"
                select attribute).ToImmutableArray();

            foreach (AttributeSyntax attribute in attributes)
            {
                StructDeclarationSyntax structDeclaration = attribute.FirstAncestorOrSelf<StructDeclarationSyntax>()!;
                SemanticModel semanticModel = context.Compilation.GetSemanticModel(structDeclaration.SyntaxTree);
                INamedTypeSymbol structDeclarationSymbol = semanticModel.GetDeclaredSymbol(structDeclaration)!;

                // Extract the info on the type to process
                var namespaceName = structDeclarationSymbol.ContainingNamespace.ToDisplayString(new(typeQualificationStyle: NameAndContainingTypesAndNamespaces));
                var structName = structDeclaration.Identifier.Text;
                var structModifiers = structDeclaration.Modifiers;
                var fields = (
                    from fieldDeclaration in structDeclaration.Members.OfType<FieldDeclarationSyntax>()
                    let fieldType = fieldDeclaration.Declaration.Type
                    let typeName = semanticModel.GetTypeInfo(fieldType).Type!.ToDisplayString()
                    let fieldFullType = ParseTypeName(typeName)
                    from fieldVariable in fieldDeclaration.Declaration.Variables
                    select (Type: fieldFullType, fieldVariable.Identifier)).ToImmutableArray();

                // Create the constructor declaration for the type. This will
                // produce a constructor with simple initialization of all variables:
                //
                // public MyType(Foo a, Bar b, Baz c, ...)
                // {
                //     this.a = a;
                //     this.b = b;
                //     this.c = c;
                //     ...
                // }
                var structDeclarationSyntax =
                    StructDeclaration(structName).WithModifiers(structModifiers).AddMembers(
                    ConstructorDeclaration(structName)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword))
                    .AddParameterListParameters(fields.Select(field => Parameter(field.Identifier).WithType(field.Type)).ToArray())
                    .AddBodyStatements(fields.Select(field => ParseStatement($"this.{field.Identifier} = {field.Identifier};")).ToArray()));

                TypeDeclarationSyntax typeDeclarationSyntax = structDeclarationSyntax;

                // Add all parent types in ascending order, if any
                foreach (var parentType in structDeclaration.Ancestors().OfType<TypeDeclarationSyntax>())
                {
                    typeDeclarationSyntax = parentType
                        .WithMembers(SingletonList<MemberDeclarationSyntax>(typeDeclarationSyntax))
                        .WithConstraintClauses(List<TypeParameterConstraintClauseSyntax>())
                        .WithBaseList(null)
                        .WithoutTrivia();
                }

                // Create the compilation unit with the namespace and target member.
                // From this, we can finally generate the source code to output.
                var source =
                    CompilationUnit().AddMembers(
                    NamespaceDeclaration(IdentifierName(namespaceName)).AddMembers(typeDeclarationSyntax))
                    .NormalizeWhitespace()
                    .ToFullString();

                // Add the partial type
                context.AddSource($"__ComputeSharp_AutoConstructorAttribute_{structDeclarationSymbol.Name}", SourceText.From(source, Encoding.UTF8));
            }
        }
    }
}

