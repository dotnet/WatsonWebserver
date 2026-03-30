namespace Test.BrowserInterop
{
    using System;
    using System.IO;

    /// <summary>
    /// Temporary browser user-data directory.
    /// </summary>
    internal sealed class BrowserUserDataDirectory : IDisposable
    {
        /// <summary>
        /// Instantiate the directory.
        /// </summary>
        public BrowserUserDataDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "watson-browser-interop-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// Directory path.
        /// </summary>
        public string Path { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                try
                {
                    Directory.Delete(Path, true);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
