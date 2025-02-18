﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using ProtoBuf.BuildTools.Internal;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace BuildToolsUnitTests
{
    public abstract class AnalyzerTestBase<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer
    {
        // utility anaylzer data, with thanks to Samo Prelog
        private readonly ITestOutputHelper? _testOutputHelper;
        protected AnalyzerTestBase(ITestOutputHelper? testOutputHelper = null) => _testOutputHelper = testOutputHelper;

        protected virtual TAnalyzer Analyzer {
            get {
                var obj = Activator.CreateInstance<TAnalyzer>();
                if (obj is ILoggingAnalyzer logging && _testOutputHelper is not null) {
                    logging.Log += s => _testOutputHelper.WriteLine(s);
                }
                return obj;
            }
        }

        protected virtual bool ReferenceDepRos => true;

        protected virtual Project SetupProject(Project project) => project;

        protected Task<ICollection<Diagnostic>> AnalyzeAsync(string? sourceCode = null, [CallerMemberName] string? callerMemberName = null, bool ignoreCompatibilityLevelAdvice = true, bool ignorePreferAsyncAdvice = true) =>
            AnalyzeAsync(project => string.IsNullOrWhiteSpace(sourceCode) ? project : project.AddDocument(callerMemberName + ".cs", sourceCode).Project, callerMemberName, ignoreCompatibilityLevelAdvice, ignorePreferAsyncAdvice);

        protected async Task<ICollection<Diagnostic>> AnalyzeAsync(Func<Project, Project> projectModifier, [CallerMemberName] string? callerMemberName = null, bool ignoreCompatibilityLevelAdvice = true, bool ignorePreferAsyncAdvice = true) {
            _ = callerMemberName;
            var (project, compilation) = await ObtainProjectAndCompilationAsync(projectModifier);
            var analyzers = project.AnalyzerReferences.SelectMany(x => x.GetAnalyzers(project.Language)).ToImmutableArray();
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers, project.AnalyzerOptions);
            var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
            //if (ignoreCompatibilityLevelAdvice) {
            //    diagnostics = diagnostics.RemoveAll(x => x.Descriptor == DataContractAnalyzer.MissingCompatibilityLevel);
            //}
            //if (ignorePreferAsyncAdvice) {
            //    diagnostics = diagnostics.RemoveAll(x => x.Descriptor == ServiceContractAnalyzer.PreferAsync);
            //}
            if (_testOutputHelper is object) {
                foreach (var d in diagnostics) {
                    _testOutputHelper.WriteLine(d.ToString());
                }
            }
            return diagnostics;
        }

        protected async Task<ICollection<Diagnostic>> AnalyzeMultiFileAsync(List<string> sourceCode, [CallerMemberName] string? callerMemberName = null, bool ignoreCompatibilityLevelAdvice = true, bool ignorePreferAsyncAdvice = true) {
            return await AnalyzeAsync(project => {
                for (int i = 0; i < sourceCode.Count; i++) {
                    project = project.AddDocument($"{callerMemberName}_{i}_.cs", sourceCode[i]).Project;
                }
                return project;
            }, callerMemberName, ignoreCompatibilityLevelAdvice, ignorePreferAsyncAdvice);
        }

        protected async Task<(Project Project, Compilation Compilation)> ObtainProjectAndCompilationAsync(Func<Project, Project>? projectModifier = null, [CallerMemberName] string? callerMemberName = null) {
            _ = callerMemberName;
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("protobuf-net.BuildTools.AnalyzerTests", LanguageNames.CSharp);
            project = project
                .WithCompilationOptions(project.CompilationOptions!.WithOutputKind(OutputKind.DynamicallyLinkedLibrary))
                .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location))
                .AddAnalyzerReference(new AnalyzerImageReference(ImmutableArray.Create<DiagnosticAnalyzer>(Analyzer)));
            project = SetupProject(project);

            if (ReferenceDepRos) {
                project = project.TryAddMetadataReference(MetadataReferenceHelpers.DepRosReferences);
            }

            project = projectModifier?.Invoke(project) ?? project;

            var compilation = await project.GetCompilationAsync();
            return (project, compilation!);
        }
    }
}
