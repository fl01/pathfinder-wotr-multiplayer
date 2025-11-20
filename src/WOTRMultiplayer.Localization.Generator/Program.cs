using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WOTRMultiplayer.Localization.Generator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var currentContext = Path.GetFullPath("./");
            var repositoryRoot = "..\\..\\";
            const string OutputProject = "WOTRMultiplayer.Localization";
            // kinda lazy way to deal with multiple launch context here
            // this should work to differentiate between pre-build event and regular launch via vs studio
            if (currentContext.Contains(Assembly.GetExecutingAssembly().GetName().Name))
            {
                repositoryRoot = "..\\..\\..\\..\\..\\";
            }

            var localizationPath = repositoryRoot + "localization\\enGB.json";
            var fullLocalizationPath = Path.GetFullPath(localizationPath);
            if (!File.Exists(fullLocalizationPath))
            {
                Console.WriteLine($"Missing localization file. Path={fullLocalizationPath}");
                return;
            }
            var outputFile = repositoryRoot + $"src\\{OutputProject}\\WellKnownKeys.cs";
            var fullOutputFilePath = Path.GetFullPath(outputFile);
            Console.WriteLine($"LocalizationPath={fullLocalizationPath}{Environment.NewLine}OutputPath={fullOutputFilePath}");

            var json = File.ReadAllText(fullLocalizationPath);
            var root = JsonConvert.DeserializeObject<JObject>(json);
            var localizationKeys = GetLocalizationKeys(root);
            GenerateTree(localizationKeys, OutputProject, outputFile);
            Console.WriteLine(fullLocalizationPath);
        }

        private static void GenerateTree(List<string> localizationKeys, string @namespace, string outputFile)
        {
            var attributeUsing = SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName("System.ComponentModel"));
            var unit = SyntaxFactory.CompilationUnit().WithUsings([attributeUsing]);
            var tree = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(@namespace));

            var classDeclarations = new ConcurrentDictionary<string, ClassDeclarationSyntax>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in localizationKeys)
            {
                var keyParts = key.Split('.');
                var root = classDeclarations.GetOrAdd(keyParts[0], k => SyntaxFactory.ClassDeclaration(keyParts[0]));
                for (int i = 1; i < keyParts.Length; i++)
                {
                    var className = Char.ToUpper(keyParts[i][0]) + keyParts[i].Substring(1);
                    var currentClassPath = string.Join(".", keyParts.Take(i + 1));
                    if (classDeclarations.ContainsKey(currentClassPath))
                    {
                        continue;
                    }

                    var declaration = SyntaxFactory.ClassDeclaration(className)
                        .WithModifiers(SyntaxFactory.TokenList([SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)]))
                        .WithAttributeLists(CreateDescriptionAttribute(keyParts[i]));

                    if (i == keyParts.Length - 1)
                    {
                        var getter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                        var setter = SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                        declaration = declaration.AddMembers(SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "Key")
                            .WithModifiers(SyntaxFactory.TokenList([SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)]))
                            .AddAccessorListAccessors(getter, setter));
                    }

                    classDeclarations.TryAdd(currentClassPath, declaration);
                }
            }

            tree = tree.AddMembers([.. classDeclarations.Values]);
            unit = unit.AddMembers(tree);

            using var writer = new StreamWriter(outputFile, append: false);
            var sourceCode = unit.NormalizeWhitespace().ToFullString();
            writer.Write(sourceCode);
        }

        private static SyntaxList<AttributeListSyntax> CreateDescriptionAttribute(string value)
        {
            var attributeArgument = SyntaxFactory.AttributeArgument(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(value))
                );

            var argumentList = SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(attributeArgument));
            var attribute = SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Description"), argumentList);
            var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));
            return [attributeList];
        }

        private static List<string> GetLocalizationKeys(JObject root)
        {
            var tokensToProcess = new Stack<JToken>();
            tokensToProcess.Push(root.Root);
            List<string> keys = [];
            while (tokensToProcess.Count > 0)
            {
                var current = tokensToProcess.Pop();
                var children = current.Children();
                if (children.Any())
                {
                    foreach (var child in children)
                    {
                        tokensToProcess.Push(child);
                    }
                    continue;
                }

                keys.Add(current.Path);
            }

            keys.Reverse();
            return keys;
        }
    }
}
