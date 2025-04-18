﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ProtoBuf.BuildTools.Internal;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace BuildToolsUnitTests
{
    public abstract class GeneratorTestBase<TGenerator> where TGenerator : ISourceGenerator
    {
        protected static AdditionalText[] Text(string path, string content) => new[] { new InMemoryAdditionalText(path, content) };
        protected static AdditionalText[] Texts(params (string path, string content)[] pairs) => pairs.Select(pair => new InMemoryAdditionalText(pair.path, pair.content)).ToArray();

        protected static AdditionalText[] Texts(params (string path, string content, (string key, string value)[]? options)[] pairs) => pairs.Select(pair => new InMemoryAdditionalText(pair.path, pair.content, pair.options)).ToArray();

        protected static ImmutableDictionary<string, string> Options(params (string key, string value)[] pairs) => pairs.ToImmutableDictionary(pair => pair.key, pair => pair.value);

        // utility anaylzer data, with thanks to Samo Prelog
        private readonly ITestOutputHelper? _testOutputHelper;
        protected GeneratorTestBase(ITestOutputHelper? testOutputHelper = null) => _testOutputHelper = testOutputHelper;

        private TGenerator? generator;
        protected virtual TGenerator Generator {
            get {
                if (generator is null) {
                    generator = Activator.CreateInstance<TGenerator>();
                    if (generator is ILoggingAnalyzer logging && _testOutputHelper is not null) {
                        logging.Log += s => _testOutputHelper.WriteLine(s);
                    }
                }
                return generator;
            }
        }

        protected async Task<(GeneratorDriverRunResult Result, ImmutableArray<Diagnostic> Diagnostics)> GenerateAsync(AdditionalText[]? additionalTexts = null, ImmutableDictionary<string, string>? globalOptions = null,
            Func<Project, Project>? projectModifier = null, [CallerMemberName] string? callerMemberName = null, bool debugLog = true) {
            if (!typeof(TGenerator).IsDefined(typeof(GeneratorAttribute))) {
                throw new InvalidOperationException($"Type is not marked [Generator]: {typeof(TGenerator)} in {callerMemberName}");
            }

            var parseOptions = new CSharpParseOptions(kind: SourceCodeKind.Regular, documentationMode: DocumentationMode.Parse);

            globalOptions ??= ImmutableDictionary<string, string>.Empty;
            if (debugLog)
                globalOptions = globalOptions.SetItem("pbn_debug_log", "true");

            var optionsProvider = TestAnalyzeConfigOptionsProvider.Empty.WithGlobalOptions(new TestAnalyzerConfigOptions(globalOptions));
            if (additionalTexts is not null && additionalTexts.Length != 0) {
                var map = ImmutableDictionary.CreateBuilder<object, AnalyzerConfigOptions>();
                foreach (var text in additionalTexts) {
                    if (text is InMemoryAdditionalText mem) {
                        map.Add(text, mem.GetOptions());
                    }
                }
                optionsProvider = optionsProvider.WithAdditionalTreeOptions(map.ToImmutable());
            }

            GeneratorDriver driver = CSharpGeneratorDriver.Create([ Generator ], additionalTexts, parseOptions: parseOptions, optionsProvider: optionsProvider);
            (var project, var compilation) = await ObtainProjectAndCompilationAsync(projectModifier);
            var result = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
            if (_testOutputHelper is object) {
                foreach (var d in diagnostics) {
                    _testOutputHelper.WriteLine(d.ToString());
                }
            }
            return (result.GetRunResult(), diagnostics);
        }

        protected virtual bool ReferenceDepRos => true;

        protected async Task<(Project Project, Compilation Compilation)> ObtainProjectAndCompilationAsync(Func<Project, Project>? projectModifier = null, [CallerMemberName] string? callerMemberName = null) {
            _ = callerMemberName;
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("protobuf-net.BuildTools.GeneratorTests", LanguageNames.CSharp);
            project = project
                .WithCompilationOptions(project.CompilationOptions!.WithOutputKind(OutputKind.DynamicallyLinkedLibrary))
                //.WithParseOptions(new CSharpParseOptions(LanguageVersion.CSharp12))
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location));

            project = SetupProject(project);

            if (ReferenceDepRos) {
                project = project.TryAddMetadataReference(MetadataReferenceHelpers.DepRosReferences);
            }

            project = projectModifier?.Invoke(project) ?? project;

            var compilation = await project.GetCompilationAsync();
            return (project, compilation!);
        }

        protected virtual Project SetupProject(Project project) => project;
    }
}
