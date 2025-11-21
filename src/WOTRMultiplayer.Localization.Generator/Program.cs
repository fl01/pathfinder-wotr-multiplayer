using System;
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public class Program
    {
        public const string OutputProject = "WOTRMultiplayer.Localization";
        public const string OutputClassName = "WellKnownKeys";
        public const string RootName = "wotrmultiplayer";
        public const string KeyPathSeparator = ".";
        public const string KeyPropertyName = "Key";
        public const string DefaultLocalizationFileName = "enGB.json";

        public static void Main(string[] args)
        {
            var currentContext = Path.GetFullPath("./");
            var repositoryRoot = "..\\..\\";

            // kinda lazy way to deal with multiple launch context here
            // this should work to differentiate between pre-build event and regular debug launch
            if (currentContext.Contains(Assembly.GetExecutingAssembly().GetName().Name))
            {
                repositoryRoot = "..\\..\\..\\..\\..\\";
            }

            var localizationPath = repositoryRoot + $"localization\\{DefaultLocalizationFileName}";
            var fullLocalizationPath = Path.GetFullPath(localizationPath);
            if (!File.Exists(fullLocalizationPath))
            {
                Console.WriteLine($"Missing localization file. Path={fullLocalizationPath}");
                return;
            }
            var outputFile = repositoryRoot + $"src\\{OutputProject}\\{OutputClassName}.cs";
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

            bool createRoot = true;
            var classDeclarations = new HashSet<LocalizationEntity>();
            foreach (var key in localizationKeys)
            {
                var keyParts = key.Split('.');
                if (createRoot)
                {
                    var entity = new LocalizationEntity
                    {
                        Declaration = SyntaxFactory.ClassDeclaration(keyParts[0]).WithModifiers(SyntaxFactory.TokenList([SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)])),
                        Name = keyParts[0]
                    };
                    classDeclarations.Add(entity);
                    createRoot = false;
                }

                for (int i = 1; i < keyParts.Length; i++)
                {
                    var className = Char.ToUpper(keyParts[i][0]) + keyParts[i].Substring(1);
                    var currentClassPath = string.Join(KeyPathSeparator, keyParts.Take(i + 1));
                    var localizatioEntity = new LocalizationEntity { Name = currentClassPath };
                    if (classDeclarations.Contains(localizatioEntity))
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
                        declaration = declaration.AddMembers(SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), KeyPropertyName)
                            .WithModifiers(SyntaxFactory.TokenList([SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)]))
                            .AddAccessorListAccessors(getter, setter));
                    }

                    localizatioEntity.Declaration = declaration;
                    classDeclarations.Add(localizatioEntity);

                    var parentEntity = new LocalizationEntity { Name = string.Join(KeyPathSeparator, keyParts.Take(i)) };
                    classDeclarations.TryGetValue(parentEntity, out var parent);
                    parent.Members.Add(localizatioEntity);
                }
            }

            var localizationRoot = classDeclarations.FirstOrDefault(x => string.Equals(x.Name, RootName, StringComparison.OrdinalIgnoreCase));
            var fullTree = BuildTree(localizationRoot, null);
            fullTree = fullTree.WithIdentifier(SyntaxFactory.Identifier(OutputClassName)).WithAttributeLists(CreateDescriptionAttribute(RootName));
            unit = unit.AddMembers(fullTree);

            var comment = SyntaxFactory.Comment($"/* Manual changes will be discarded since this is a compile time generated file based on default localization file ({DefaultLocalizationFileName}).{Environment.NewLine}Refer to {Assembly.GetExecutingAssembly().GetName().Name} project for more details on generation */");
            unit = unit.WithLeadingTrivia(comment);
            using var writer = new StreamWriter(outputFile, append: false);
            var sourceCode = unit.NormalizeWhitespace().ToFullString();
            writer.Write(sourceCode);
        }

        private static ClassDeclarationSyntax BuildTree(LocalizationEntity entity, ClassDeclarationSyntax parent)
        {
            if (entity.Members.Count == 0)
            {
                return parent.AddMembers(entity.Declaration);
            }

            var declaration = entity.Declaration;
            foreach (var member in entity.Members)
            {
                declaration = BuildTree(member, declaration);
            }

            return parent == null ? declaration : parent.AddMembers(declaration);
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

        private class LocalizationEntity
        {
            public string Name { get; set; }

            public ClassDeclarationSyntax Declaration { get; set; }

            public HashSet<LocalizationEntity> Members { get; set; } = [];

            public override int GetHashCode()
            {
                return Name.ToLower().GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return obj is LocalizationEntity another && another.Name != null && another.Name == Name;
            }
        }
    }
}
