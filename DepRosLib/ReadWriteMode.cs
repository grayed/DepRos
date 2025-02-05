namespace DepRos
{
    public enum ReadWriteMode {
        /// <summary>
        /// Creates a read-only dependency property when setter is less accessible than getter.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Forces creation of read-only dependency property.
        /// </summary>
        ReadOnly,

        /// <summary>
        /// Forces creation of read-write dependency property.
        /// </summary>
        ReadWrite,
    }
}
