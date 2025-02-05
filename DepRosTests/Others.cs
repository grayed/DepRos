#nullable enable
using System;

internal static class Literals
{
    public const string CategoryUsage = "Usage";

    public const string AdditionalFileMetadataPrefix = "build_metadata.AdditionalFiles.";
}

namespace ProtoBuf.BuildTools.Internal
{
    internal interface ILoggingAnalyzer
    {
        event Action<string>? Log;
    }
}