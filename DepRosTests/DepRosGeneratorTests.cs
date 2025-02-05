using BuildToolsUnitTests;
using DepRos;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit.Abstractions;

namespace DepRosTests;

public class DepRosGeneratorTests : GeneratorTestBase<DepRosGenerator>
{
    public DepRosGeneratorTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    private const string testSource = """
        using System;
        using DepRos;

        namespace DepRosTester {
            public partial class @MainWindow : System.Windows.Window
            {
                [DependencyProperty]
                protected internal virtual partial int @MyProperty { get; internal set; }

                [AttachedProperty]
                private const int defaultForAnotherProperty = 42;
            }
        }
        """;

    private const string SmallCode = @"using System; namespace Foo { class @Bar { int @foo; } }";

    protected override Project SetupProject(Project project) {
        return project.AddDocument("main.cs", SourceText.From(testSource)).Project;
    }

    [Fact]
    public async Task BasicGenerateWorks() {
        (var result, var diagnostics) = await GenerateAsync([]);

        Assert.Empty(diagnostics);
        Assert.Single(result.GeneratedTrees);
    }
}
