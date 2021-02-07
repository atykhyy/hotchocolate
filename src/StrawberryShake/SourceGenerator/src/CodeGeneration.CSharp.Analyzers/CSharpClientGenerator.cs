﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DotNet.Globbing;
using HotChocolate;
using HotChocolate.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using IOPath = System.IO.Path;

namespace StrawberryShake.CodeGeneration.CSharp.Analyzers
{
    [Generator]
    public class CSharpClientGenerator : ISourceGenerator
    {
        private static string _location = System.IO.Path.GetDirectoryName(
            typeof(CSharpClientGenerator).Assembly.Location)!;

        static CSharpClientGenerator()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
        }

        private static Assembly? CurrentDomainOnAssemblyResolve(
            object sender,
            ResolveEventArgs args)
        {
            try
            {
                var assemblyName = new AssemblyName(args.Name);
                var path = IOPath.Combine(_location, assemblyName.Name + ".dll");
                return Assembly.LoadFrom(path);
            }
            catch
            {
                return null;
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var documents = GetGraphQLFiles(context);

            foreach (var config in GetGraphQLConfigs(context))
            {
                config.Documents ??= IOPath.Combine(
                    IOPath.GetDirectoryName(config.Location)!,
                    "**",
                    "*.graphql");

                var glob = Glob.Parse(config.Documents);
                var configDocuments = documents.Where(t => glob.IsMatch(t)).ToList();
                var result = GenerateClient(documents, config.Extensions.StrawberryShake);

                if (result.HasErrors())
                {
                    CreateDiagnosticErrors(context, result.Errors);
                }

                foreach (CSharpDocument document in result.CSharpDocuments)
                {
                    context.AddSource(
                        document.Name + ".cs",
                        SourceText.From(document.SourceText, Encoding.UTF8));
                }
            }
        }

        private CSharpGeneratorResult GenerateClient(
            IEnumerable<string> documents,
            StrawberryShakeSettings settings)
        {
            try
            {
                var generator = new CSharpGenerator();
                return generator.Generate(documents, settings.Name, settings.Namespace);
            }
            catch (Exception ex)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage(ex.Message)
                        .SetException(ex)
                        .Build());
            }
        }

        private void CreateDiagnosticErrors(
            GeneratorExecutionContext context,
            IReadOnlyList<IError> errors)
        {
            foreach (IError error in errors)
            {
                string title =
                    error.Extensions is not null &&
                    error.Extensions.TryGetValue(
                        CodeGenerationThrowHelper.TitleExtensionKey,
                        out var value) &&
                    value is string s
                        ? s
                        : "Unexpected";

                string code = error.Code ?? "Unexpected";

                if (error.Extensions is not null &&
                    error.Extensions.TryGetValue(
                        CodeGenerationThrowHelper.FileExtensionKey,
                        out value) &&
                    value is string filePath)
                {
                    HotChocolate.Location location =
                        error.Locations?.First() ??
                        throw new ArgumentNullException();

                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: code,
                                title: title,
                                messageFormat: "The .graphql file '{0}' has errors.",
                                category: "StrawberryShakeGenerator",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true,
                                description: error.Message),
                            Microsoft.CodeAnalysis.Location.Create(
                                filePath,
                                TextSpan.FromBounds(
                                    1,
                                    2),
                                new LinePositionSpan(
                                    new LinePosition(
                                        location.Line,
                                        location.Column),
                                    new LinePosition(
                                        location.Line,
                                        location.Column + 1)))));
                }
                else
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                id: code,
                                title: title,
                                messageFormat: $"An error occured during generation: {error.Message}",
                                category: "StrawberryShakeGenerator",
                                DiagnosticSeverity.Error,
                                isEnabledByDefault: true,
                                description: error.Message),
                            Microsoft.CodeAnalysis.Location.None));
                }
            }
        }

        private IReadOnlyList<string> GetGraphQLFiles(
            GeneratorExecutionContext context) =>
            context.AdditionalFiles
                .Select(t => t.Path)
                .Where(t => IOPath.GetExtension(t).EqualsOrdinal(".graphql"))
                .ToList();

        private IReadOnlyList<GraphQLConfig> GetGraphQLConfigs(
            GeneratorExecutionContext context)
        {
            var list = new List<GraphQLConfig>();

            foreach (var configLocation in GetGraphQLConfigFiles(context))
            {
                try
                {
                    var config = JsonConvert.DeserializeObject<GraphQLConfig>(configLocation);
                    config.Location = configLocation;
                    list.Add(config);
                }
                catch (Exception ex)
                {
                    throw new GraphQLException(
                        ErrorBuilder.New()
                            .SetMessage(ex.Message)
                            .SetException(ex)
                            .SetExtension(CodeGenerationThrowHelper.FileExtensionKey, configLocation)
                            .AddLocation(new HotChocolate.Location(1, 1))
                            .Build());
                }
            }

            return list;
        }

        private IReadOnlyList<string> GetGraphQLConfigFiles(
            GeneratorExecutionContext context) =>
            context.AdditionalFiles
                .Select(t => t.Path)
                .Where(t => IOPath.GetFileName(t).EqualsOrdinal(".graphqlrc.json"))
                .ToList();
    }
}
