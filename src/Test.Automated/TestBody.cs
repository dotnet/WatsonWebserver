namespace Test.Automated
{
    using System;

    /// <summary>
    /// Test request body model for integration tests.
    /// </summary>
    public class TestBody
    {
        /// <summary>
        /// Name field.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value ?? String.Empty;
            }
        }

        private string _Name = String.Empty;
    }
}
