using DepRos;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Xunit;

namespace BuildToolsUnitTests
{
    internal static class MetadataReferenceHelpers
    {
        public static readonly Dictionary<string, Exception> MetadataLoadErrors = [];

        private static readonly EnumerationOptions enumerationOptions = new() {
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            MatchType = MatchType.Simple
        };

        public static string DotNetRoot { get; } = GetDotNetRoot();
        private static string GetDotNetRoot() {
            var systemAssemblyPath = Assembly.GetAssembly(typeof(String))!.Location;
            var dotNetCurVerSharedPath = Path.GetDirectoryName(systemAssemblyPath)!;
            var dotNetVersionsSharedPath = Path.GetDirectoryName(dotNetCurVerSharedPath)!;
            var dotNetSharedPath = Path.GetDirectoryName(dotNetVersionsSharedPath)!;
            var dotNetPath =  Path.GetDirectoryName(dotNetSharedPath)!;
            return dotNetPath;
        }

        public static readonly bool HasWpf = Directory.Exists(Path.Combine(DotNetRoot, "packs", "Microsoft.WindowsDesktop.App.Ref"));
        public static readonly string WpfPackDir;

        private static readonly ReadOnlyCollection<string> wpfAssemblyNames = new string[] { "PresentationCore.dll", "PresentationFramework.dll", "WindowsBase.dll" }.AsReadOnly();
        private static readonly Dictionary<Version, List<PortableExecutableReference>> wpfPackVersions = [];


        private static IEnumerable<string> GetWpfVersions() {
            var versionsPath = Path.Combine(DotNetRoot, "packs", "Microsoft.WindowsDesktop.App.Ref");
            if (!Directory.Exists(versionsPath))
                yield break;

            foreach (var subDirPath in Directory.GetDirectories(WpfPackDir, "*.*.*", enumerationOptions)) {
                var dotNetVerString = Path.GetFileName(subDirPath);
                if (!Version.TryParse(dotNetVerString, out _))
                    continue;

                var wpfDotNetVersions = new List<string>();
                var wpfDotNetVersionsPath = Path.Combine(subDirPath, "ref");
                if (!Directory.Exists(wpfDotNetVersionsPath))
                    continue;   // warn?

                // TODO lazy loading, to speed up test startup
                foreach (var dotNetPath in Directory.GetDirectories(wpfDotNetVersionsPath, "*", enumerationOptions)) {
                    var filePath = Path.Combine(dotNetPath, wpfAssemblyNames[0]);
                    if (File.Exists(filePath)) {
                        yield return dotNetVerString;
                        break;
                    }
                }
            }
        }

        private static string LatestVersion(IEnumerable<string> versionStrings) => versionStrings.ToImmutableSortedDictionary(Version.Parse, s => s).Last().Value;

        static MetadataReferenceHelpers() {
            var systemAssemblyPath = Assembly.GetAssembly(typeof(String))!.Location;
            var dotNetCurVerSharedPath = Path.GetDirectoryName(systemAssemblyPath)!;
            var dotNetVersionsSharedPath = Path.GetDirectoryName(dotNetCurVerSharedPath)!;
            var dotNetSharedPath = Path.GetDirectoryName(dotNetVersionsSharedPath)!;
            DotNetRoot = Path.GetDirectoryName(dotNetSharedPath)!;

            WpfPackDir = $@"{DotNetRoot}/packs/Microsoft.WindowsDesktop.App.Ref";     // TODO
            HasWpf = true;                                                                      // TODO

            foreach (var subDirPath in Directory.GetDirectories(WpfPackDir, "*.*.*", enumerationOptions)) {
                var subDirName = Path.GetFileName(subDirPath);
                if (!Version.TryParse(subDirName, out var ver))
                    continue;

                var wpfDotNetVersions = new List<string>();
                var wpfDotNetVersionsPath = Path.Combine(subDirPath, "ref");
                if (!Directory.Exists(wpfDotNetVersionsPath))
                    continue;   // warn?

                // TODO lazy loading, to speed up test startup
                foreach (var dotNetPath in Directory.GetDirectories(wpfDotNetVersionsPath, "*", enumerationOptions)) {
                    List<PortableExecutableReference> wpfRefs = [];
                    for (int i = 0; i < wpfAssemblyNames.Count; i++) {
                        var filePath = Path.Combine(dotNetPath, wpfAssemblyNames[i]);
                        if (File.Exists(filePath)) {
                            PortableExecutableReference assRef;
                            try {
                                assRef = MetadataReference.CreateFromFile(filePath);
                            }
                            catch (Exception e) {
                                MetadataLoadErrors.Add(filePath, e);
                                continue;
                            }
                            wpfRefs.Add(assRef);
                        }
                    }
                    if (wpfRefs.Count > 0) {
                        wpfPackVersions.Add(ver, wpfRefs);
                    }
                }
            }

            Wpf = [
                MetadataReference.CreateFromFile(@"c:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\9.0.0\ref\net9.0\PresentationCore.dll"),
                MetadataReference.CreateFromFile(@"c:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\9.0.0\ref\net9.0\PresentationFramework.dll"),
                MetadataReference.CreateFromFile(@"c:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\9.0.0\ref\net9.0\WindowsBase.dll"),
            ];
        }

        private static string GetPackDir(string packName) => Path.Combine(DotNetRoot, "packs", packName);

        /// <summary>
        /// Find all versions of the given pack supported in current <see cref="DotNetRoot"/>.
        /// </summary>
        /// <param name="packName">Name of .NET pack to be examined.</param>
        /// <returns>List of pack versions found.</returns>
        public static IEnumerable<Version> GetVersionsOfPack(string packName) {
            foreach (var subDirPath in Directory.GetDirectories(GetPackDir(packName), "*.*.*", enumerationOptions)) {
                var subDirName = Path.GetFileName(subDirPath);
                if (Version.TryParse(subDirName, out var ver))
                    yield return ver;
            }
        }

        public static IEnumerable<string> GetDotnetVersionsOfPack(string packName) {


            var refsDir = Path.Combine(GetPackDir(packName), "ref");
            foreach (var subDirPath in Directory.GetDirectories(refsDir, "*.*", enumerationOptions)) {
                var subDirName = Path.GetFileName(subDirPath);
                yield return subDirName;
            }
        }


        //public static MetadataReference[] WellKnownReferences = new[]
        //{
        //    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        //    MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        //    MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location),
        //    MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location)
        //};

        public static MetadataReference[] DepRosReferences =
        [
            MetadataReference.CreateFromFile(Assembly.Load("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51").Location),
            //MetadataReference.CreateFromFile(Assembly.Load("PresentationFramework").Location),
            MetadataReference.CreateFromFile(@"c:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\9.0.0\ref\net9.0\PresentationCore.dll"),
            MetadataReference.CreateFromFile(@"c:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\9.0.0\ref\net9.0\PresentationFramework.dll"),
            MetadataReference.CreateFromFile(@"c:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\9.0.0\ref\net9.0\WindowsBase.dll"),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(DepRosGenerator).Assembly.Location)
        ];

        public static readonly MetadataReference[] Wpf;

        public static Project TryAddMetadataReference(this Project project, params MetadataReference[] metadataReferences) {
            if (metadataReferences is null || metadataReferences.Length == 0)
                return project;
            foreach (var metadataReference in metadataReferences) {
                try {
                    project = project.AddMetadataReference(metadataReference);
                }
                catch {
                    // AddMetadataReference throws on attempt to add duplicate references,
                    // so just ignore and continue in that case
                }
            }

            return project;
        }
    }
}