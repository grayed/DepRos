using System;

namespace DepRos
{
    // Our, portable variant
    public enum BindingMode {
        OneWay = 0,
        OneTime,
        TwoWay,
    }

    // Avalonia.Data.BindingMode
    public enum AvaloniaBindingMode
    {
        Default = 0,    // used when merging StyledPropertyMetadata: if 'base' is not Default and 'descendant' is, then value from 'base' is used
        OneWay = 1,     // actual default
        TwoWay = 2,
        OneTime = 3,
        OneWayToSource = 4,
    }

    // System.Windows.Data.BindingMode
    public enum WpfBindingMode {
        TwoWay = 0,
        OneWay = 1,
        OneTime = 2,
        OneWayToSource = 3,
        Default = 4,    // actual value depends on FrameworkPropertyMetadata.BindsTwoWayByDefault
    }

    // Microsoft.UI.Xaml.Data.BindingMode
    // Windows.UI.Xaml.Data.BindingMode
    public enum WinUIBindingMode {
        OneWay = 1,     // Default for all bindings
        OneTime = 2,
        TwoWay = 3,
    }

    public static class BindingModeExtensions {
        /// <summary>
        /// Ineffective but portable way of converting binding mode for the locally used XAML framework,
        /// e.g., to Avalonia.Data.BindingMode.
        /// </summary>
        /// <typeparam name="BindingModeT">Destination BindingMode type</typeparam>
        /// <param name="portableMode">Value to be converted</param>
        /// <returns>Value from<typeparamref name="BindingModeT"/> enumeration having the same logic</returns>
        public static BindingModeT ToLocal<BindingModeT>(this BindingMode portableMode) where BindingModeT : Enum {
            var text = portableMode.ToString();
            return (BindingModeT)Enum.Parse(typeof(BindingModeT), text);
        }
    }
}
